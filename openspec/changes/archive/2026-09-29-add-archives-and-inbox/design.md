# Design

## Context

The only aggregate today is `User`: a Fisher event stream with an Inline snapshot (`options.Projections.Snapshot<User>(SnapshotLifecycle.Inline)`). Wolverine handlers drive it. Creates start a stream directly, and updates and deletes are `[DeciderFunction]`s, where Wolverine loads the aggregate, enforces the client's expected `Version`, appends the returned events and commits. A `ValidateAsync` step turns an unknown id into `NotFound` before the version check can misreport it. `ErrorStatusProvider` maps exceptions to gRPC statuses, so new aggregates get the same mapping for free by throwing the existing exception types. Storage is a single SQLite file (one writer at a time), and Wolverine runs in `Solo` durability mode with `AutoApplyTransactions`.

The web side uses one DotNetQuery facade per service (`UsersFacade`): queries keyed by `QueryKey`, and mutations that invalidate keys. It also uses Radzen components and `.resx` localization (see CLAUDE.md).

## Goals / Non-Goals

**Goals:**
- Two new aggregates that follow the `User` patterns exactly, so the only new ideas are the cross-aggregate ones: the owner check when filing, the deletion cascade, and the filing race.
- A first cross-aggregate process, the archive-deletion cascade, built on Wolverine's outbox. It's deliberate event-sourcing practice.
- A tested answer to the filing-vs-deletion race.

**Non-Goals:**
- Changing `User` behavior. Deleting a user leaves their documents and archives untouched and orphaned for now.
- A generic cross-aggregate or saga framework. This is one hand-written cascade.
- Pagination. The lists are unbounded at this stage.

## Decisions

### D1. Membership lives on the Document stream

`Document` holds `ArchiveId : Guid?` (null means inbox). Filing appends `DocumentFiled(ArchiveId)` to the document's stream, including moves from one archive to another. Unfiling appends `DocumentUnfiled`. The "at most one archive" rule then holds by construction, because the snapshot has one slot and a single aggregate owns it.

*Alternative:* the Archive stream records `DocumentAdded` and `DocumentRemoved`. Rejected, because "at most one" would then span N archive streams and need cross-aggregate enforcement. The archive's contents are a query instead: `Query<Document>().Where(ArchiveId == id && Status != Deleted)`.

### D2. The inbox is a query, not an aggregate

The inbox is `Query<Document>().Where(OwnerId == u && ArchiveId == null && Status != Deleted)`. It has no stream, events or id. A per-user Inline snapshot would be a second source of truth for something one indexed query already answers.

### D3. Events

```
Archive:  ArchiveCreated(OwnerId, Name)   ArchiveRenamed(Name)   ArchiveDeleted
          ArchiveMembershipChanged  (no payload; appended on every filing into the archive, see D6)
Document: DocumentCreated(OwnerId, Title) DocumentRenamed(Title) DocumentDeleted
          DocumentFiled(ArchiveId)        DocumentUnfiled
```

`DocumentUnfiled` has no payload. The previous archive is in the stream's history, and the snapshot doesn't need it. `OwnerId` is fixed at creation; no event changes it.

### D4. Commands and handlers

These mirror the User feature (`Features/Archives`, `Features/Documents`):

| Command | Handler style | Notes |
|---|---|---|
| `CreateArchiveCommand(OwnerId, Name)`, `CreateDocumentCommand(OwnerId, Title)` | start stream | validator for field rules; owner loaded and checked (`NotFound` / `user_deleted`) before `StartStream` |
| `RenameArchiveCommand`, `RenameDocumentCommand` (`…Id, Version, Name/Title`) | `[DeciderFunction]` | `ValidateAsync` existence check; deleted -> `InvariantViolationException` |
| `DeleteArchiveCommand(ArchiveId, Version)` | decider + cascade (D5) | idempotent on already-deleted |
| `DeleteDocumentCommand(DocumentId, Version)` | `[DeciderFunction]` | idempotent |
| `FileDocumentCommand(DocumentId, Version, ArchiveId)` | session handler; moves the archive's version (D6) | checks in order: document deleted, archive exists (`NotFound`), archive deleted, owner mismatch; same archive -> no events |
| `UnfileDocumentCommand(DocumentId, Version)` | `[DeciderFunction]` | already in inbox -> no events |
| `ReturnDocumentToInbox(DocumentId, FromArchiveId)` | internal cascade message (D5) | not exposed over gRPC; no expected version |

Title and name rules follow `CreateUserValidator`: trimmed value, `ViolationCodes`, and property names pinned to the command field (which Wolverine and the gRPC adapter report as `title` and `name`). Maximum lengths are 200 for titles and 100 for names, as constants on the validators.

### D5. Archive deletion cascades through the outbox

`DeleteArchive` loads the ids of the archive's non-deleted member documents, appends `ArchiveDeleted`, and returns one `ReturnDocumentToInbox(docId, archiveId)` per member as a cascading message. Under `AutoApplyTransactions`, Wolverine persists those messages in its outbox in the same SQLite transaction as the event, so a commit can't lose them. They're delivered locally after commit.

`ReturnDocumentToInbox` is idempotent, and it defers to anything newer. It loads the document, and appends `DocumentUnfiled` only if the document isn't deleted and its `ArchiveId` is still `FromArchiveId`. Otherwise it does nothing. It takes no expected version: the check on `FromArchiveId` is the guard, and a concurrent user write simply wins or retries. Retries on a concurrency failure come from Wolverine's error policy for this message type (retry with a short delay a few times).

*Alternatives:*
- Refusing deletion of non-empty archives: rejected because the user decided documents should return to the inbox.
- One transaction touching every member stream: rejected, because it's the anti-pattern this exercise is meant to avoid.
- Treating a deleted archive as the inbox in queries: rejected because it makes the event log lie.

### D6. The filing race: filing moves the archive's version

The race: `DeleteArchive` queries the members, then a `FileDocument` into that archive commits, then `ArchiveDeleted` commits. The late-filed document is never returned. A filing that only reads the archive doesn't fix this, because it doesn't move the archive's version, so the delete's expected-version check still passes.

**Spike result (task group 1: a throwaway pipeline test against a temp SQLite store, Fisher 1.13.0):**
1. `FetchForWriting<T>` on a stream that is then left alone does *not* guard: the commit succeeds even though another session moved the stream. Setting `IEventStream<T>.AlwaysEnforceConsistency = true` on the fetched stream does: the commit then fails with `EventStreamUnexpectedMaxEventIdException`, and an unmoved stream still commits. So a filing-side guard works, but only with that flag.
2. `FetchForExclusiveWriting` takes no lock on SQLite. Fisher documents it as behaving like `AppendOptimistic` there, because holding `BEGIN IMMEDIATE` from fetch to commit would block every other writer. A concurrent unrelated commit finished in 2 ms while the exclusive fetch was open. The delete-side guard the first plan relied on doesn't exist, so a filing could still commit between the member query and `ArchiveDeleted`.
3. Neither guard is needed once (2) fails. `FileDocument` spans two streams and has its own check order, so it uses `IDocumentSession` directly (`FetchForWriting` on both streams). `DeleteArchive` stays in the decider workflow: the decider takes an `IQuerySession` for the member query and returns `(Events, OutgoingMessages)`, so the archive's expected-version guard and the outbox-persisted cascade messages both come from the workflow (settled in task 5.2).

**Chosen approach: the fallback.** Filing moves the archive's version, by also appending a no-payload `ArchiveMembershipChanged` event to the archive stream on every filing into it. Filing into the archive a document is already in appends nothing. Both handlers then rely only on the ordinary expected-version guard:
- **Filing side:** `FileDocument` fetches the document at the client's expected version and the target archive with `FetchForWriting<Archive>`, checks the rules, then appends `DocumentFiled` to the document and `ArchiveMembershipChanged` to the archive. If `ArchiveDeleted` committed after the archive was read, the archive append fails with `EventStreamUnexpectedMaxEventIdException` (`ABORTED`). The client reloads, sees the archive is gone, and a retry gets `archive_deleted`.
- **Delete side:** `DeleteArchive` loads the archive at the client's expected version, queries the members, then appends `ArchiveDeleted`. A filing that commits after the archive was read and before the delete commits has moved the archive's version, so the delete fails with `ABORTED` instead of missing the document. A filing that committed earlier is already visible to the member query.

Together these give the two outcomes the spec allows: filed-then-returned, or refused. Two side effects are accepted. A concurrent *rename* of the archive also aborts a filing. And every filing bumps the archive's version, so a client holding an older archive version gets a stale-write refusal on rename or delete until it reloads (see D8). The cost is one extra event per filing.

### D7. gRPC contracts

Separate `archives.proto` (`package archives`, `ArchivesService`) and `documents.proto` (`package documents`, `DocumentsService`), following `users.proto`: resource-oriented RPCs, mutations return the resulting DTO with its `version`, and ids are strings.
- `ArchivesService`: `ListArchives(owner_id)`, `CreateArchive`, `RenameArchive`, `DeleteArchive`.
- `DocumentsService`: `ListInbox(owner_id)`, `ListArchiveDocuments(archive_id)`, `CreateDocument`, `RenameDocument`, `DeleteDocument`, `FileDocument(document_id, version, archive_id)`, `UnfileDocument(document_id, version)`.
- `DocumentDto` carries `optional string archive_id`, where unset means inbox.
- `UsersService` gains `FindUserById`, reusing the existing `FindUserByIdQuery` handler, filtered to non-deleted users at the service or mapper level.

Listing an archive's documents lives on `DocumentsService` because it returns documents. Mappers use Mapperly like `UserMapper`.

### D8. Web: one workspace page, two facades

- `ArchivesFacade` and `DocumentsFacade` follow `UsersFacade`. Query keys are `["archives", ownerId]`, `["inbox", ownerId]` and `["archive-documents", archiveId]`. Every document mutation invalidates the inbox and archive-documents kinds. DotNetQuery's `InvalidateKeys` matches whole keys, so a bare `["inbox"]` would not match `["inbox", ownerId]`; the facades invalidate by kind (the first key part) through the client's predicate overload from `OnSuccess` instead. Archive mutations invalidate the archives key, and so does filing a document, because filing moves the archive's version (D6).
- After `DeleteArchive`, the cascade is asynchronous, so the returned documents may not be in the inbox on the immediate refetch. The page invalidates the inbox again after a short delay (about 1 second). The facade's existing 30-second refetch interval is the backstop. This is enough for a single-process app. Push notifications are out of scope.
- Routes: `/users/{UserId:guid}` selects the inbox, and `/users/{UserId:guid}/archives/{ArchiveId:guid}` selects an archive, so a selection can be bookmarked and survives a reload. A layout of a navigation list plus a document list uses `RadzenStack` / `RadzenDataGrid`. "File into..." is a `RadzenSplitButton` or menu per row. Create and rename use small dialogs modeled on `CreateUserDialog`, and delete uses `DialogService.Confirm`.
- Error mapping follows `CreateUserErrors`: a static mapper from status code plus `ErrorInfo.Reason` to a `.resx` key.
- Landing page: the user grid's display name becomes a link to `/users/{id}`.

## Risks / Trade-offs

- [Filing bumps the archive's version, so archive versions held by clients go stale after every filing] → The facade re-fetches the archives key after filing, and a stale write is already a handled, localized refusal. The cost is one extra event per filing (D6).
- [Unbounded member lists: deleting an archive with many documents emits that many outbox messages in one transaction] → Acceptable at this scale. It's a known limit, and batching can come later.
- [A cascade message fails permanently] → The document stays in a deleted archive, where it's invisible in the UI. Wolverine's dead-letter storage keeps the message for inspection. With the retry policy and the idempotent handler, this shouldn't happen in practice.
- [A user's documents and archives outlive the user's deletion] → Explicitly out of scope. The workspace shows not-found for a deleted user, so nothing is reachable in the UI.
- [There are no authorization checks, so anyone can act on anyone's workspace] → The same as the current users page. The owner check in D6 is a data-integrity rule, not a security boundary.
