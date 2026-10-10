# Proposal

## Why

office-rkyv is paperless office software, but a document is still only a title: nothing can be stored, and nothing can be taken back out. The archive and inbox model was built on metadata first so that it could be proven before file storage was designed. It holds, so this change gives every document its file.

## What Changes

- **BREAKING** A document is created by uploading a file. Every document has exactly one file, which never changes after creation. Creating a document from a title alone is removed.
- A document records facts about its file: the original file name, the declared content type, the size and a SHA-256 hash of the content.
- Upload accepts any file type up to 100 MB. A larger file and an empty file are refused, and nothing is stored for them.
- The title comes from the file name, without its extension, when the client sends none. Renaming works as before and never touches the file.
- A document's file can be downloaded, byte for byte as it was uploaded, under its original file name.
- The workspace's new-document action takes several files at once. Each file is its own upload: it shows its own progress, and it succeeds or fails without affecting the others.
- Each listed document offers a download action. The web app hands the file to the browser as a download; the desktop app asks where to save it.
- The file content is stored outside the event store, in a `blobs` folder beside the database, named by its hash. The same content uploaded twice is stored once, and uploading the same file twice is allowed and creates two documents.
- Deleting a document does not remove its file from disk in this change.
- The development seed creates its three demo documents from sample PDF files, so the demo data can be downloaded.
- Upload and download are the first streaming RPCs in the contract: the client streams the file up, the server streams it down.

Not in this change, and planned as later changes: removing file content that no document refers to any more; picking up files from a watched folder; viewing a file inside the app.

Guideline docs whose conventions this change sets or changes:

- `docs/api.md`: a streaming service method as a new shape beside the three handler styles, the rule that file content is stored beside the event store and written before the event, and the step list for adding an RPC.
- `docs/architecture.md`: the blob folder as part of where a host keeps its data, and the changed request path for an upload and a download.
- `docs/ui.md`: the host-implemented interface for handing a downloaded file to the user, and how an upload with progress sits beside the DotNetQuery mutations.
- `docs/desktop.md`: how the desktop saves a downloaded file, and any workaround it needs.
- `docs/testing.md`: how pipeline tests drive a streaming call and where their blob folder lives.
- `docs/observability.md`: followed, not changed.

## Capabilities

### New Capabilities

None. A document's file is part of what a document is, so it belongs to `document-management`.

### Modified Capabilities

- `document-management`: creation requires a file and records its facts; the title rule changes to "derived from the file name when absent"; size and emptiness limits; download; duplicates; what deletion does to the file. The purpose no longer says "metadata only".
- `user-workspace`: the new-document action takes several files instead of a title; each document offers a download.
- `api-error-mapping`: a new violation code for a file that is too large, and the mapping holding for streaming calls.
- `development-seed-data`: the demo documents carry files.
- `desktop-app`: the user's data directory also holds the file content; a downloaded file is saved through the desktop.

## Impact

- `src/OfficeRkyv.Contracts`: `documents.proto` loses `CreateDocument` and gains an upload and a download RPC, both streaming; `DocumentDto` gains the file facts; a new code in `ViolationCodes`.
- `src/OfficeRkyv.Api`: the `Documents` feature (aggregate, creation event, command, validators, service, handlers), a new blob store under `Shared/`, `AddOfficeRkyvApi` taking the blob location, the development seeder, and the sample PDFs under `Data/` becoming seed content.
- `src/OfficeRkyv.UI`: `DocumentsFacade`, the document dialog (replaced by an upload dialog), the workspace's document rows, a new interface for saving a file, and the `.resx` resources in English and German.
- `src/OfficeRkyv.Web`: its implementation of the file-saving interface, with a small script for the browser download.
- `src/OfficeRkyv.Desktop`: its implementation of the file-saving interface, and the blob folder in `DesktopPaths`.
- `src/OfficeRkyv.AppHost`: none expected; the blob folder follows the database file.
- `tests/OfficeRkyv.UnitTests`: `PipelineHost` gets a blob folder; the document pipeline tests create documents by upload; new tests for the blob store, the limits, the download and the seed.
- `tests/OfficeRkyv.FunctionalTests`: the workspace page object and every test that creates a document; new journeys for upload and download.
- Stored data: `DocumentCreated` gains fields. Existing development databases are throwaway. A desktop database that already holds title-only documents needs a decision, recorded in the design.
- No new dependency is expected.
