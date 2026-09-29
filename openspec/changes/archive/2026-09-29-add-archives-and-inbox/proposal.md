# Proposal

## Why

office-rkyv is meant to be a paperless office, but so far it only knows about users. Nothing represents a document, and there's nowhere to keep one. This change adds the first document-side concepts: documents, archives that group them, and an inbox for documents that haven't been filed yet. Later work such as file upload, tags and search builds on these. Everything stays metadata only, so the archive and inbox model can be proven before file storage is designed.

## What Changes

- **Documents** (metadata only): a user can create, rename and delete documents. A document has a title and belongs to exactly one user. No file content is stored.
- **Archives**: a user can create, rename and delete any number of archives, including none. An archive is a named collection of that user's documents. Archive names are free text with no uniqueness rule.
- **Filing**: a document lives in **at most one** archive. Filing moves it from the inbox into an archive, or from one archive to another. Unfiling returns it to the inbox. A document can only be filed into an active archive that belongs to the same user.
- **Inbox**: a user's inbox is every non-deleted document of theirs that isn't in an archive. It's derived and is never created, named or deleted.
- **Deleting an archive returns its documents to the inbox.** This happens asynchronously, shortly after the deletion, and never loses a document. Filing that races with an archive deletion must never leave a document inside a deleted archive.
- **gRPC API**: two new services, `ArchivesService` (`archives.proto`) and `DocumentsService` (`documents.proto`). `UsersService` gains a find-by-id lookup so a client can resolve the user it's showing.
- **New business-rule refusals** use the existing `FAILED_PRECONDITION` mapping, with new stable reason codes (`archive_deleted`, `document_deleted`, `archive_owner_mismatch`).
- **Web UI**: each user on the landing page links to a per-user workspace at `/users/{id}`. The workspace shows the inbox, the user's archives, and the documents in whichever of those is selected. From there the user can create documents, file them with a "File into..." menu, and create, rename and delete archives and documents. Without authentication, the user in the URL stands in for the signed-in user.

## Capabilities

### New Capabilities

- `document-management`: creating, renaming, deleting and listing a user's documents, filing and unfiling them, the inbox as the set of unfiled documents, and documents returning to the inbox when their archive is deleted.
- `archive-management`: creating, renaming, deleting and listing a user's archives, and listing the documents an archive contains.
- `user-workspace`: the per-user web page for working with the inbox, archives and documents.

### Modified Capabilities

- `user-management`: adds finding a single user by id.
- `user-landing-page`: listed users become links to their workspace.
- `api-error-mapping`: the list of `FAILED_PRECONDITION` reason codes gains the archive and document rules.

## Impact

- `src/OfficeRkyv.Contracts`: new `archives.proto` and `documents.proto`, a `FindUserById` RPC in `users.proto`, and new `InvariantCodes`.
- `src/OfficeRkyv.Api/Features/Archives/*` and `Features/Documents/*` (new): `Archive` and `Document` aggregates with Inline snapshots, events, commands, queries, validators, handlers, mappers and gRPC services. `Program.cs` registers the snapshots and services.
- `src/OfficeRkyv.Api/Features/Users`: a `FindUserById` endpoint on `UsersServiceImpl` (the query handler already exists).
- `src/OfficeRkyv.Web`: new workspace page and dialogs with `.resx` resources, new facades for archives and documents, gRPC client registrations, and landing-page rows that link to the workspace.
- Tests: pipeline tests for the new aggregates, including the archive-deletion cascade and the filing race; unit tests for the web error mapping; a functional test for the workspace.
- Out of scope: file content, upload and storage; tags and other metadata; search; authentication and authorization; what happens to a deleted user's documents and archives; sharing documents between users.
