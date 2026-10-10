# API

How the API (`src/OfficeRkyv.Api`) and its contract (`src/OfficeRkyv.Contracts`) are built: the shape of a feature, the handler styles, the event-sourcing rules, validation, and how a failure reaches a client. Read it before adding or changing an RPC, a command, an event or an error code. `Features/Archives` is the worked example throughout.

## A feature is one folder

Each aggregate has a folder under `Features/` with the same files:

| File | Holds |
|---|---|
| `Archive.cs` | The aggregate: a record deriving from `EntityBase`, with a static `Create` for the first event and one `Apply` per later event. |
| `ArchiveEvents.cs` | The events, as payload-only records. Fisher supplies the envelope (stream id, version, timestamp). |
| `ArchiveCommands.cs` | The commands. A command on an existing aggregate carries `<Aggregate>Id` and the expected `Version`. |
| `ArchiveQueries.cs` | The queries. |
| `ArchiveMapper.cs` | A Mapperly mapper: requests to commands and queries, aggregates to DTOs and responses. |
| `ArchivesServiceImpl.cs` | The gRPC service. It maps, dispatches on `IMessageBus`, and maps back. No logic. |
| `Handlers/` | One static handler class per command or query, the validators, and the existence check. |

What features share is under `Shared/`: `EntityBase` and `EntityStatus`, the exceptions, the error mapping, the text rules for validators, the snapshot registration, the telemetry subscription, and the blob store for file content (`Shared/Blobs`).

The service names its RPCs after the resource (`ListArchives`, `RenameArchive`); the split into commands and queries is the server's own business and does not show in the contract.

## Handlers

A handler is a public static class with a static `Handle` method; Wolverine finds it by convention and injects the session. There are three shapes, and the command decides which (a fourth shape, a streaming service method, is below):

- **Start a stream** (`CreateArchiveHandler`): takes `IDocumentSession`, checks what the new aggregate depends on (its owner exists and is active), calls `StartStream`, saves, and returns the snapshot.
- **Decider** (`RenameArchiveHandler`, marked `[DeciderFunction]`): takes the command and the current aggregate and returns the events to append. The workflow loads the stream at the command's expected version and appends inside the same transaction. Use it for every command on one existing aggregate. A decider that also sends messages returns `(Events, OutgoingMessages)` (`DeleteArchiveHandler`).
- **Session handler across streams** (`FileDocumentHandler`): when one command writes to two streams, it uses `IDocumentSession` and `FetchForWriting` on both, in a fixed order of checks.

Queries are static handlers over `IQuerySession` (`FindArchivesByOwnerHandler`). A list query leaves out deleted aggregates.

Rules that hold for all of them:

- **Unknown id is "not found", not "stale".** A command on an existing aggregate has a `ValidateAsync` step that calls the feature's existence check (`ArchiveExistence.RequireAsync`). It runs before the stream is loaded at the expected version, so an unknown id throws `NotFoundException` instead of failing the version check.
- **Deleting is soft and idempotent.** Deletion appends an event and sets `EntityStatus.Deleted`. Deleting a deleted aggregate succeeds and appends nothing.
- **No change, no event.** Renaming to the same name or filing into the same archive appends nothing.
- **Handlers throw; they know nothing about gRPC.** A refused business rule is `InvariantViolationException` with a code; a missing resource is `NotFoundException`.
- **Handlers do not log.** See [observability.md](observability.md).

### A streaming service method

`DocumentsServiceImpl.UploadDocument` (client streaming) and `DownloadDocument` (server streaming) are the only service methods that do work of their own, which departs from "a service maps, dispatches and maps back". The reason is in the data: an upload cannot be a command until its content has been read, because the command carries the size and the hash of that content.

- **The service only adapts the stream.** It reads the header message, then hands everything after it, as a sequence of chunks, to `DocumentIngest`. It owns no rule.
- **`DocumentIngest` is the one way a file becomes a document.** The upload RPC, the development seed and a later folder watcher all call it. It writes the content to the blob store, refuses an empty or oversized file as a `ValidationException` (field `content`, codes `required` and `too_large`), and then dispatches the ordinary `CreateDocumentCommand`, from which point validators and the start-stream handler apply as always.
- **Limits are enforced while reading.** The blob store counts bytes and stops at `FileStorage:MaxFileSizeBytes` (100 MB by default), so an oversized upload is never held whole and never stored.
- **A download is a find-by-id and a stream.** It checks the document exists and is not deleted, then copies the blob in `FileLimits.ChunkSize` chunks. A document whose content is missing is a fault: `INTERNAL`, logged at Error with the document id.
- **Errors need nothing special.** A failure thrown while the stream is read or written takes the same path to a status as a unary call (`api-error-mapping`).

A new streaming method follows this shape; anything that is not a stream stays a plain map, dispatch and map back.


## Event sourcing

The store is Fisher on SQLite. Every aggregate has an Inline snapshot, registered in one list (`StoreOptionsExtensions.AddAggregateSnapshots`) that the API and the tests share.

- **Snapshots are written in the transaction that appends the event**, so a read right after a write is current. A service may dispatch a command and then a find-by-id query and return the result.
- **Record the facts that decided something.** A value that a rule depended on goes into the event and is not derived again while folding. `UserCreated` carries `NormalizedEmail`, because deriving it during replay would re-key users if the normalization ever changed.
- **One aggregate owns one fact.** Which archive a document is in is recorded on the document (`DocumentFiled`, `DocumentUnfiled`), so "at most one archive" holds by construction. An archive's contents are a query over documents.
- **A list that decides nothing is a query, not an aggregate.** The inbox is "the owner's documents with no archive". It has no stream.
- **File content is not an event.** Fisher stores every snapshot and event body as JSON text, so bytes would be base64 inside it, loaded with every inbox query and kept forever. The bytes live in the blob store, a directory of files named by their SHA-256 (`Shared/Blobs`); the event (`DocumentCreated`) records only the facts about them: file name, content type, size and hash. A document has exactly one file and no event changes it.
- **Content first, then the event.** An upload writes the blob (into `tmp/`, renamed to its hash when complete) and only then appends the event. A crash in between leaves a blob nothing refers to, which is harmless; the other order would leave a document with no file. Nothing deletes a blob yet: another document may share it, so removal is a separate, later change.
- **Uniqueness is claimed by the store.** A unique value is a `[NaturalKey]` on the aggregate (`User.NormalizedEmail`); the store refuses the second stream inside the append, so there is no check-then-write race.
- **Concurrency is the expected version.** The client echoes the `version` it read; a stale one fails the append and reaches the client as `ABORTED`.
- **Cascades go through the outbox, one stream per message.** Deleting an archive appends `ArchiveDeleted` and returns one `ReturnDocumentToInbox` per member document. Wolverine stores those messages in the transaction of the event and delivers them afterwards, so the documents return shortly after the call has answered, not as part of it. A cascade message is idempotent, takes no expected version, guards on the state it expects (`FromArchiveId`), and retries on a concurrency conflict.
- **A write that must lose a race moves the other stream's version.** Filing a document also appends `ArchiveMembershipChanged` to the archive, so a deletion and a filing that race fail on the ordinary version check, whichever commits second. On SQLite there is no exclusive fetch to rely on.
- **Never touch every member stream in one transaction.** That is what the cascade is for.

Transactions are applied by Wolverine (`AutoApplyTransactions`); a handler calls `SaveChangesAsync` itself only when it needs the result before returning.

## Validation

Input rules are FluentValidation validators next to the handlers (`ArchiveValidators.cs`). Wolverine's middleware finds them and runs them before the handler.

- A validator is `public` and has no constructor dependencies, or discovery does not find it.
- It reports every violated rule, not the first.
- Each rule has an error code from `ViolationCodes`, set with `WithErrorCode`. `TextRules.RequiredAndAtMost` covers a required, length-limited text field.
- Rules see the value as it will be stored (trimmed), and pin the property name back with `OverridePropertyName`, since the name would otherwise come from the `Trim()` call.
- Maximum lengths are constants on the validator.

Validation is about the shape of the input. Whether the resource exists or its state allows the operation is the handler's job.

What cannot be a validator is refused where it is found: the content of a file is known only while it is read, so `DocumentIngest` throws the same `ValidationException` with the same `ViolationCodes` before any command exists.

## Failures

One place turns failures into gRPC statuses for every service: `UseApiErrorMapping` in `Shared/Errors`, called from `AddOfficeRkyvApi`.

| Thrown | Status | Detail for the client |
|---|---|---|
| `FluentValidation.ValidationException` | `INVALID_ARGUMENT` | `BadRequest`: one field violation per rule, `field` in snake_case as in the `.proto`, `reason` a `ViolationCodes` value |
| `InvariantViolationException` | `FAILED_PRECONDITION` | `ErrorInfo`: `reason` an `InvariantCodes` value, `domain` `ErrorDomain.Value` |
| `DuplicateNaturalKeyException` | `ALREADY_EXISTS` | none |
| `EventStreamUnexpectedMaxEventIdException` | `ABORTED` | none |
| `NotFoundException` | `NOT_FOUND` | none |
| The client cancelled | `CANCELLED` | none, and not logged |
| Anything else | `INTERNAL` | none; logged at Error with the exception |

- **Status messages are our own fixed text.** An exception's message can hold SQL or paths, so it never reaches a client.
- **Codes are stable wire values.** They live in `OfficeRkyv.Contracts` (`ViolationCodes`, `InvariantCodes`) so API and UI share them. Never reword one; clients key their text off it.
- **Clients explain a refusal from the status code and the reason**, never from the message. The UI side is in [ui.md](ui.md).

The required behaviour is the `api-error-mapping` spec in `openspec/specs`.

## Adding an RPC

1. Add the RPC and its messages to the feature's `.proto` in `OfficeRkyv.Contracts`. Ids are strings; a mutation that leaves the resource in place returns its DTO with the new `version`.
2. Add the command or query record, and its mapping in the feature's mapper.
3. Add the handler in `Handlers/`, in the shape that fits, with a validator if the command has input rules and the existence check if it names an existing aggregate.
4. Add new events to the events file and an `Apply` for each on the aggregate.
5. Add the service method: map, dispatch, map back.
6. For a new refusal, add the code to `InvariantCodes` and its text on the UI side.
7. For a new aggregate, add its snapshot to `AddAggregateSnapshots` and its service to `MapOfficeRkyvApi`.
8. For a call that streams a file, follow [the streaming shape](#a-streaming-service-method): the service adapts the stream, and the rules live in the component it hands the stream to.
9. Cover it with pipeline tests; see [testing.md](testing.md).

## Decided in

- [landing-page-user-management](../openspec/changes/archive/2026-09-24-landing-page-user-management/design.md) (D1, D3): facts recorded in events, and existence checked before the version.
- [structured-error-codes](../openspec/changes/archive/2026-09-28-structured-error-codes/design.md) (D1 to D5): the error mapping, the validation adapter, validators through Wolverine's middleware, codes on invariant violations, codes in `Contracts`.
- [add-archives-and-inbox](../openspec/changes/archive/2026-09-29-add-archives-and-inbox/design.md) (D1 to D7): membership on the document, the inbox as a query, the handler styles, the cascade through the outbox, the filing race, the contracts.
- [add-linux-desktop-app](../openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md) (D3): the API composition as extension methods.
- [add-document-file-upload](../openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D1 to D10): file facts on the creation event, the blob store, content first, the streaming RPCs, the ingest component, limits while reading, the download.
