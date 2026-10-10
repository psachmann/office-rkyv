# Tasks

## 1. Spikes

- [x] 1.1 Spike the server side of design D14: add a throwaway client-streaming and a throwaway server-streaming method that throw `ValidationException` and `NotFoundException`, call them in a pipeline test through `PipelineHost.Channel`, and record in `design.md` (under D14) whether `GrpcFailure` reads `INVALID_ARGUMENT` with field violations and `NOT_FOUND`. Remove the throwaway code; done when the result is written down.
- [x] 1.2 Spike the client side of design D14: call a streaming method through a client registered with `AddWolverineGrpcClient` and record in `design.md` whether a refusal surfaces as the same typed exception with the `RpcException` inside as for a unary call. Done when the result is written down.
- [x] 1.3 If 1.1 or 1.2 found a gap, write the fallback chosen (local mapping for the two streaming methods, or unwrapping in the facade) into `design.md` D14 before continuing; if neither did, note "no fallback needed". Done when D14 states which.
- [x] 1.4 Find where the AppHost's `AddSqlite` puts the development database and whether anything removes it, and answer the first open question in `design.md`. Done when the open question is replaced by the finding.

## 2. Contract

- [x] 2.1 In `documents.proto`, remove `CreateDocument` and its messages, add `UploadDocument` (client-streaming, a `oneof` of header and chunk) and `DownloadDocument` (server-streaming chunks), and add the file's name, content type, size and hash to `DocumentDto` (design D5). Verify `OfficeRkyv.Contracts` builds and the generated client has both streaming methods.
- [x] 2.2 Add `TooLarge = "too_large"` to `ViolationCodes` in `ErrorCodes.cs`. Verify `OfficeRkyv.Contracts` builds.

## 3. Blob store

- [x] 3.1 Add the blob store interface and its filesystem implementation under `src/OfficeRkyv.Api/Shared` (design D2): stream into `tmp/<guid>` while hashing and counting, rename to `<2 hex>/<hash>`, delete the temporary file on failure or when the target exists, stop at a given byte limit, open a blob by hash for reading. Cover it with unit tests against a temporary directory: content round trip, same content stored once, nothing left in `tmp` after a failed or over-limit write, an empty stream reported as empty. Verify they pass.
- [x] 3.2 Remove leftover files in `tmp` when the API starts, and log one Warning with their number through a `[LoggerMessage]` method (design, Telemetry and logs). Add a unit test that a leftover file is gone after start and a start with an empty `tmp` logs nothing. Verify it passes.
- [x] 3.3 Give `AddOfficeRkyvApi` the blob directory and the size limit option with its 100 MB default, and register the blob store there (design D3, D7). Verify the solution builds once the hosts in 3.4 to 3.6 pass a directory.
- [x] 3.4 In the API server's `Program.cs`, derive the blob directory from the database file of the connection string, with a configuration value that overrides it. Add a unit test for the derivation (a plain `Data Source=` path, and an override). Verify it passes.
- [x] 3.5 Add the blob directory to `DesktopPaths` (`blobs` in the data directory) and pass it in `DesktopApi`. Extend the unit test `DesktopPathsTests` for it. Verify it passes.
- [x] 3.6 Give `PipelineHost` a blob directory beside its temporary database, deleted on dispose, and an optional size limit for tests. Verify the existing pipeline tests that do not create documents still pass.

## 4. Documents feature in the API

- [x] 4.1 Add the four file facts to `DocumentCreated`, to `Document` and to `CreateDocumentCommand`, and map them to `DocumentDto` in `DocumentMapper` (design D1). Verify `OfficeRkyv.Api` builds.
- [x] 4.2 Add the title derivation (design D8) as a pure function and cover it with unit tests: a plain name, several dots, a name longer than 200 characters, a name that is only an extension, surrounding whitespace. Verify they pass.
- [x] 4.3 Extend the create validator with the file-name and content-type rules (design D7): last path segment, required, at most 255 characters; content type at most 255 characters and `application/octet-stream` when absent; the title rules only when a title was sent. Cover the rules with unit tests. Verify they pass.
- [x] 4.4 Add the ingest component in `Features/Documents` (design D6): validate the header's shape, write the content through the blob store with the limit, throw `ValidationException` with `too_large` or `required` on `content`, then dispatch `CreateDocumentCommand` with the title and the file facts. Verify `OfficeRkyv.Api` builds; behaviour is covered in 4.7.
- [x] 4.5 Implement `UploadDocument` in `DocumentsServiceImpl`: read the header, expose the remaining messages as a stream, call the ingest component, return the created document. Remove the `CreateDocument` method. Apply the fallback from task 1.3 if one was chosen. Verify `OfficeRkyv.Api` builds.
- [x] 4.6 Implement `DownloadDocument` (design D10): find the document, refuse an unknown one as not found and a deleted one with `document_deleted`, stream the blob in 64 KB chunks, and log one Error with the document id through a `[LoggerMessage]` method when the blob is missing. Verify `OfficeRkyv.Api` builds.
- [x] 4.7 Add a test helper that uploads content through the generated client (`TestDocuments`), and cover the upload scenarios in the new `DocumentUploadTests`; `DocumentPipelineTests` keeps creating by command, with file facts from `TestDocuments.Command`, because it tests the handlers and not the upload. Add pipeline tests for every scenario of the `document-management` delta spec: the returned file facts, title trimmed and derived, unknown and deleted owner, a file at the limit and one byte over it with a small configured limit, an empty file, a missing file name, a missing content type, rename keeping the file facts, download equal to upload for a file larger than one chunk, download of an unknown and of a deleted document, the same file twice, and deleting one of two documents with the same content. Verify they pass and that a refused upload leaves nothing in `tmp`.
- [x] 4.8 Add pipeline tests for the two streaming scenarios of the `api-error-mapping` delta spec, reading the status and its details with `GrpcFailure`. Verify they pass.
- [x] 4.9 Update the other pipeline tests that create documents (`ArchiveDeletionRaceTests`, `DeleteArchiveTests`, `WorkspaceServicesTests`) to use the helper from 4.7. Verify `dotnet test --project tests/OfficeRkyv.UnitTests` passes for the pipeline level.
- [x] 4.10 Extend the pipeline test `ApiTelemetryTests` with an upload and assert that no span carries the file name or the title. Verify it passes.

## 5. Development seed

- [x] 5.1 Ship `sample_1.pdf` to `sample_3.pdf` from `src/OfficeRkyv.Api/Data` with the API's build output, and make `DemoDataSeeder` create its three documents through the ingest component with the existing titles sent explicitly and the content type `application/pdf` (design D13). Verify the files are in the API's output folder after `just build`.
- [x] 5.2 Update the pipeline tests `DemoDataSeederTests` for the changed creation, and add the scenario "Seeded documents have downloadable files" from the `development-seed-data` delta spec. Verify they pass.
- [x] 5.3 Decide with the user whether `sample_4.pdf` and `sample_5.pdf` stay as test content or are removed, and act on it. Done when the second open question in `design.md` is replaced by the decision.

## 6. UI

- [x] 6.1 Add the file-saving interface to `src/OfficeRkyv.UI` (design D12): a file name, a content type and a stream. Verify `OfficeRkyv.UI` builds.
- [x] 6.2 Replace the create mutation in `DocumentsFacade` with an upload method (header, then 64 KB chunks, a progress callback, the inbox query invalidated when a document was created) and add a download method that returns the content as a stream (design D11). Apply the fallback from task 1.3 if one was chosen. Verify `OfficeRkyv.UI` builds.
- [x] 6.3 Cover the facade with UI-logic tests in `WorkspaceFacadeTests`, with a fake documents client that records the streamed messages: a header then chunks, progress reported, the inbox invalidated after success and not after a refusal, a download delivering the fake's bytes. Verify they pass.
- [x] 6.4 Map `too_large` and `required` on `content`, and the file-name violations, to messages in `WorkspaceErrors`, with their texts in the English and German `.resx` files. Extend the UI-logic test `WorkspaceErrorsTests`. Verify it and `TranslationCompletenessTests` pass.
- [x] 6.5 Replace `DocumentDialog` with an upload dialog: several files, each listed with its state (waiting, uploading with progress, done, refused with its reason), uploaded one after another, a refused file not stopping the next, and an over-limit file flagged before sending. All texts from `.resx` in English and German; remove the resources of the old dialog. Verify `TranslationCompletenessTests` passes and, with `just dev`, that two chosen files appear in the inbox.
- [x] 6.6 Add a download action to each document row in `Workspace.razor` that gets the stream from the facade and hands it to the file-saving interface, with a test id on the action as `docs/ui.md` describes. Verify `OfficeRkyv.UI` builds and `TranslationCompletenessTests` passes.

## 7. Web host

- [x] 7.1 Implement the file-saving interface in `src/OfficeRkyv.Web` with Blazor's stream interop and a small script that saves the stream as a browser download, and register it in `Program.cs`. Verify with `just dev` that downloading a seeded document saves a PDF that opens.
- [x] 7.2 Update the functional tests: give `WorkspacePage` an upload action (setting files on the dialog's input) and a download action, and change every test in `WorkspaceTests` that creates a document to upload a small file. Verify the existing workspace journeys pass.
- [x] 7.3 Add functional tests for the scenarios of the `user-workspace` delta spec: one file, several files, a refused empty file beside an accepted one, and a download whose name and bytes equal the uploaded file (Playwright's download event). Verify they pass.
- [x] 7.4 Check that `DevelopmentSeedTests` (functional) still holds with the seed switched off. Verify it passes.

## 8. Desktop host

- [x] 8.1 Implement the file-saving interface in `src/OfficeRkyv.Desktop` with a GTK save dialog that proposes the file name and writes the stream to the chosen path; a cancelled dialog writes nothing and shows no error. Register it in `MauiProgram`. Verify the desktop project builds.
- [x] 8.2 Verify by hand on a real desktop with `just desktop`: upload a PDF, choose its download action, save it, and compare `sha256sum` of the saved file with the original; cancel a second save. Observe in the app's stdout that no error or warning is logged for either, and record the result in `design.md` D12. (Done by hand: dialog opens with the name proposed, the chosen path is used, the success notification appears, the saved bytes equal the upload, and cancelling a second save writes nothing and shows no error.)
- [x] 8.3 Only if 8.2 shows the dialog does not work: replace it with saving into the Downloads directory under a non-clashing name and a notification with the path, add unit tests for the non-clashing name, and repeat the by-hand check of 8.2, observing in stdout that no error is logged. Record in `design.md` D12 which of the two the app uses. (Not needed: the dialog works once the schema is available, see design D12; the fallback stays for desktops without it.)
- [x] 8.4 Verify by hand with `just desktop`: upload a file, close the app, start it again and save the document's file; observe in stdout that the start logs no warning about leftover temporary files, and check that the content is below `~/.local/share/office-rkyv/blobs`.
- [x] 8.5 Delete the desktop database from before this change (design D13) and add to `README.md`, beside where the desktop keeps its data, that the `blobs` folder belongs to the database and both are backed up together. Done when `README.md` says so.

## 9. Guideline docs and specs

- [x] 9.1 Update `docs/api.md`: the streaming service shape and the ingest component, content beside the event store and written before the event, limits enforced while reading, the steps for adding an RPC, and the "Decided in" entry. Done when the doc matches the code.
- [x] 9.2 Update `docs/architecture.md` and the `context` in `openspec/config.yaml`: the blob folder per host, the changed `AddOfficeRkyvApi`, the request path of an upload and a download, the seed's files. Done when both match the code.
- [x] 9.3 Update `docs/ui.md`: the file-saving interface per host, and the facade's upload and download methods beside queries and mutations. Done when the doc matches the code.
- [x] 9.4 Update `docs/desktop.md`: how a file is saved, and any workaround task 8.2 or 8.3 found, with what it answers. Done when the doc matches the code.
- [x] 9.5 Update `docs/testing.md`: `PipelineHost`'s blob folder and size limit, driving a streaming call in a pipeline test, uploading and asserting a download in a functional test. Done when the doc matches the tests.
- [x] 9.6 Update `docs/observability.md`: add the file name and the title to what must never appear in a span or a log, and name the two new log messages. Done when the doc matches the code.
- [x] 9.7 Change the Purpose of `openspec/specs/document-management/spec.md`, which still says documents are metadata only, to say a document is one file with its metadata. Done when the Purpose no longer says "metadata only".

## 10. Whole-change verification

- [x] 10.1 Run `just format`, then `just test Release`, which builds with warnings as errors and runs the unit, pipeline, UI-logic and functional tests. (Result: Release build has 0 warnings; the unit, pipeline and UI-logic tests pass 377 of 377, three runs in a row; the functional tests pass except `SettingsTests.ChoosingALanguage_ReloadsTheSettingsPageInThatLanguage` and, now and then, the documented sidebar test, which fail the same way on an untouched `HEAD`.) Verify everything passes; if `SettingsTests.Settings_IsReachableFromTheSidebar_AtTheBottom_AndMarkedAsTheCurrentPage` is the only failure, run it again before looking for a cause.
- [x] 10.2 Run `openspec validate add-document-file-upload --strict`. Verify it reports no errors.
