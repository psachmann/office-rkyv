# Tasks

## 1. Spike: Fisher cross-stream guards (design D6)

- [x] 1.1 In a throwaway pipeline test against a temp SQLite store, fetch stream A with `FetchForWriting` without appending, append to stream B, then append to A from a second session and commit it before the first session commits. Verify the first commit fails with `EventStreamUnexpectedMaxEventIdException`, answering spike question 1.
- [x] 1.2 In the same test harness, open a session with `FetchForExclusiveWriting` on stream A, run a query, and check that a concurrent session's commit blocks until the first one commits. This answers spike question 2.
- [x] 1.3 Determine whether both guards can be used inside Wolverine's aggregate-handler workflow or need direct `IDocumentSession` use, answering spike question 3. Record the results of 1.1-1.3 and the chosen approach (primary, or the `ArchiveMembershipChanged` fallback) in design.md D6, and delete the throwaway test.

## 2. Contracts

- [x] 2.1 Add `archives.proto` and `documents.proto` per design D7, and add `FindUserById` to `users.proto`. Verify `dotnet build` generates the client and server types.
- [x] 2.2 Add `ArchiveDeleted`, `DocumentDeleted` and `ArchiveOwnerMismatch` to `InvariantCodes`, using the wire values `archive_deleted`, `document_deleted` and `archive_owner_mismatch`. Verify with the build.

## 3. Archive aggregate (API)

- [x] 3.1 Add the `Archive` record (`OwnerId`, `Name`) with events, `Create` and `Apply` methods, and register its Inline snapshot in `Program.cs`. Verify with the build (a missing `Apply` is a build error).
- [x] 3.2 Add `CreateArchiveCommand` with a validator (name required, max 100, trimmed) and a handler that checks the owner (`NotFound` / `user_deleted`). Verify with pipeline tests for create, empty name, unknown owner, deleted owner and a duplicate name being allowed.
- [x] 3.3 Add `RenameArchiveCommand` and a decider with an existence check. Verify with pipeline tests for rename, a stale version (`ABORTED`) and a deleted archive (`archive_deleted`).
- [x] 3.4 Add `FindArchivesByOwnerQuery` and its handler, returning non-deleted archives only. Verify with a pipeline test that another user's archives and deleted archives are excluded.

## 4. Document aggregate (API)

- [x] 4.1 Add the `Document` record (`OwnerId`, `Title`, `ArchiveId?`) with events, `Create` and `Apply` methods, and register its Inline snapshot. Verify with the build.
- [x] 4.2 Add `CreateDocumentCommand` with a validator (title required, max 200, trimmed) and an owner check. Verify with pipeline tests that a new document is in the inbox, the title is trimmed, and an unknown or deleted owner is refused.
- [x] 4.3 Add the `RenameDocumentCommand` and `DeleteDocumentCommand` deciders. Verify with pipeline tests for rename, a stale version, rename of a deleted document (`document_deleted`), and idempotent delete.
- [x] 4.4 Add `FindInboxQuery(OwnerId)` and `FindArchiveDocumentsQuery(ArchiveId)` with their handlers. Verify with pipeline tests that the inbox holds only the owner's unfiled, non-deleted documents and that an archive lists only its non-deleted members.
- [x] 4.5 Add `FileDocumentCommand` with the archive guard chosen in 1.3. Verify with pipeline tests for filing from the inbox, moving between archives, filing into the current archive (no new events), unknown archive (`NOT_FOUND`), deleted archive (`archive_deleted`), another user's archive (`archive_owner_mismatch`) and a deleted document.
- [x] 4.6 Add the `UnfileDocumentCommand` decider. Verify with pipeline tests that unfiling returns the document to the inbox and that unfiling an inbox document is a no-op.

## 5. Archive deletion cascade

- [x] 5.1 Add the `ReturnDocumentToInbox` handler, which unfiles only if the document is not deleted and still in `FromArchiveId`, plus a retry policy for concurrency failures. Verify with pipeline tests for the unfile case and for the no-op cases (moved elsewhere, deleted).
- [x] 5.2 Add the `DeleteArchiveCommand` handler: take the exclusive guard chosen in 1.3, load member ids, append `ArchiveDeleted`, and cascade `ReturnDocumentToInbox` per member. Verify with a pipeline test that, after the Wolverine tracked session completes, all members are in the inbox, and that deleting an already-deleted archive is a no-op.
- [x] 5.3 Add a pipeline test that runs `FileDocument` and `DeleteArchive` on the same archive concurrently, many times. Verify the document never ends up in the deleted archive, and that each run ends as either filed-then-returned or refused.

## 6. gRPC services

- [x] 6.1 Add `ArchiveMapper`/`DocumentMapper` (Mapperly), `ArchivesServiceImpl` and `DocumentsServiceImpl`, and map both in `Program.cs`. Verify with over-the-wire pipeline tests for a happy path and one `FAILED_PRECONDITION` reason per service.
- [x] 6.2 Add `FindUserById` to `UsersServiceImpl`, returning unset for unknown or deleted users. Verify with pipeline tests for found, unknown and deleted.

## 7. Web: facades and landing page

- [x] 7.1 Register the `ArchivesService` and `DocumentsService` gRPC clients, and add `ArchivesFacade` and `DocumentsFacade` with the query keys and invalidations from design D8, plus `FindUserById` on `UsersFacade`. Verify with facade unit tests following `UsersFacadeTests`.
- [x] 7.2 Add a workspace error mapper (status plus reason to `.resx` key) following `CreateUserErrors`. Verify with unit tests covering `ABORTED`, `NOT_FOUND` and each new reason code.
- [x] 7.3 Make each user on the landing page link to `/users/{id}`. Verify manually that clicking a user navigates there.

## 8. Web: workspace page

- [x] 8.1 Add the `Workspace.razor` page at `/users/{UserId:guid}` and `/users/{UserId:guid}/archives/{ArchiveId:guid}` with a user header, a not-found state, a navigation list (inbox with count, then archives) and a document list with an empty state. All text goes in `Resources/Components/Pages/Users/Workspace.resx`, and elements get `data-testid`s. Verify manually against a running app.
- [x] 8.2 Add the create and rename document dialog and the create and rename archive dialog, with client-side required hints and localized server errors. Verify manually that invalid input is flagged and valid input updates the lists without a reload.
- [x] 8.3 Add the per-row "File into..." menu (other archives, plus the inbox when filed) and confirmed delete actions for documents and archives. The archive confirmation states that documents return to the inbox, and the page selects the inbox and re-invalidates after a delete. Verify manually that filing, unfiling and archive deletion move documents as specified.

## 9. Functional test and wrap-up

- [x] 9.1 Add a workspace page object and a Playwright functional test covering: create archive, create document, file it, delete the archive, and the document back in the inbox. Verify the test passes with `dotnet test` on the functional test project.
- [x] 9.2 Run the full unit test suite and `openspec validate add-archives-and-inbox --strict`. Verify both pass.
