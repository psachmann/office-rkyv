# TODOs

Postponed features and decisions: things that were deliberately left out, with the reason, so they are not rediscovered from scratch.

- **Add an item** when a change defers something. Say what, why it was postponed, and where that was decided.
- **Remove an item** when it becomes an OpenSpec change (`/opsx:explore` or `/opsx:propose`), or when it is decided against. The archived change keeps the history.
- This is not a task list for work in flight. That lives in the `tasks.md` of the active change.

"Decided in" points at the design or proposal that postponed the item.

## Features

### Files

- [ ] **Blob collector.** Remove file content that no active document refers to.
  - Why postponed: it is the only code that destroys user data, and a blob may be shared by several documents, so "is it still referenced" races with a concurrent upload of the same file.
  - What piles up until then: content of deleted documents, of uploads refused after their content was complete (unknown or deleted owner, invalid file name), and of uploads cut off by a crash.
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D4).
- [ ] **Folder watcher.** Pick up files dropped into a folder and create documents from them.
  - Already in place: `DocumentIngest` is the single path a file takes, and the title is derived on the server, so the watcher is a second caller.
  - Still to decide: which user owns a dropped file (there is no authentication; one folder per user is the simple answer), how to wait for a file that is still being written, what happens to the source file afterwards, and whether duplicates are skipped by hash.
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/proposal.md).
- [ ] **In-app preview.** Show a document's file without saving it first.
  - Why postponed: the browser or WebView needs a URL to load, which means a content endpoint per host, and WebKitGTK's PDF rendering is unproven on the experimental backend.
  - Cheap first step for the desktop: "open with the default application".
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D12).
- [ ] **Duplicate handling.** Today the same file uploaded twice gives two documents sharing one blob.
  - Options: refuse, or warn and ask. The hash is recorded on every document, so either can be added without reading files again.
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D9).
- [ ] **Resumable uploads.** An interrupted upload starts over. Listed as a non-goal in the same design.
- [ ] **Content inspection.** The content type is recorded as the client declared it; nothing sniffs the bytes, scans for malware or extracts text. Text extraction is the prerequisite for search.

### Documents and users

- [ ] **Tags and other metadata.**
- [ ] **Search.**
  - [ ] **Evaluate Lucene.NET as the search engine.** Decide when content search is built on the extracted text, with real OCR output to judge by.
    - What it would add: German stemming (`Rechnungen` finds `Rechnung`), `ae`/`ä` normalisation, fuzzy matching for OCR misreadings, compound-word splitting, highlighting and better ranking of long texts.
    - What it costs: the index leaves the SQLite transaction and can drift, so it needs feeding from the events and a rebuild path; backups become database, blobs and index together; the package is still `4.8.0-beta`; compound splitting needs a German dictionary and hyphenation file, and without them `rechnung` is not found inside `Stromrechnung`, which the trigram index can do.
    - Switch if: OCR text is noisy enough that searches miss documents without fuzzy matching, relevance ranking over long texts is visibly poor, or users miss stemming and `ae`/`ä` more than matching inside words.
    - The engine sits behind one handler and one RPC, so a switch touches the API only. The `document-search` spec's "matches anywhere inside a word" would need rewording. Fisher's hybrid vector search is the other option to weigh at the same time.
    - Decided in: the exploration before [add-document-title-search](openspec/changes/add-document-title-search/design.md), which chose FTS5 with a trigram index (D1).
- [ ] **Authentication and authorization.** Until then the user id in the page address stands in for the signed-in user.
- [ ] **Sharing documents between users.**
- [ ] **What happens to a deleted user's documents and archives.** They are left orphaned; the workspace shows not-found, so nothing is reachable in the UI.
- [ ] **Pagination.** Every list is unbounded.
- [ ] **Push notifications.** After an archive is deleted the UI refreshes the inbox again after a short delay, with the 30-second refetch as the backstop.

All seven were set aside in [add-archives-and-inbox](openspec/changes/archive/2026-09-29-add-archives-and-inbox/proposal.md) and its design.

## Decisions to revisit

- [ ] **Upload as a DotNet Query mutation.** `DocumentsFacade.UploadAsync` is a plain method because a mutation cannot report progress. If the library gains mutation progress, the upload becomes an ordinary mutation and the exception in [docs/ui.md](docs/ui.md#files-upload-and-download) goes away. The proposal for the library is drafted in [dotnet-query-streaming-issue.md](dotnet-query-streaming-issue.md).
- [ ] **Web download through an HTTP endpoint.** The browser download collects the whole file in memory before saving. Move to a streaming endpoint on the Web host if that is a problem near the 100 MB limit. Decided in [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D12).
- [ ] **The client's size check uses the default limit.** The upload dialog refuses files over `FileLimits.DefaultMaxFileSizeBytes`. A server configured with a different `FileStorage:MaxFileSizeBytes` is still the authority, but the quick client-side answer would then be wrong. Fix by exposing the configured limit to the UI.
- [ ] **The owner is checked after the content is stored.** An upload for an unknown or deleted owner is refused, but leaves an orphan blob. The same holds for an invalid file name. Checking the header first would avoid it; the blob collector makes it harmless.
- [ ] **Uploads run one after another.** Chosen because the store has one writer. Revisit if batches feel slow.
- [ ] **Documents from before file upload are not migrated.** A database with title-only documents shows them without a file and fails to download them. Acceptable while the desktop app has no users but its developer.
- [ ] **Backups.** The database and its `blobs` folder must be copied together; nothing enforces or helps with that. A SQLite `BLOB` table would restore "one file", at the cost described in the design (D2).

## Desktop

- [ ] **GSettings schemas outside the dev shell.** GTK's save dialog aborts the process without `org.gtk.gtk4.Settings.FileChooser`. The `desktop` dev shell provides it and `DesktopFileSaver` falls back to the Downloads directory when it is missing, but a packaged app has to ship or locate the schemas itself. See [docs/desktop.md](docs/desktop.md#saving-a-downloaded-file).
- [ ] **The Downloads fallback has never run on a real desktop.** Its naming rules are unit-tested; the path through `DesktopFileSaver` was not exercised by hand because the dialog works here.
- [ ] **The save dialog has no parent window.** It is opened with `null`, so it may not stay on top of the app window on every desktop.

## Development and tests

- [ ] **Temporary databases and blob folders are never cleaned up.** Every `just dev` leaves a `xxxxxxxx.xxx.db` and a matching `.db.blobs` folder in the temp directory.
- [ ] **Flaky functional tests.** Both fail on an untouched checkout:
  - `SettingsTests.ChoosingALanguage_ReloadsTheSettingsPageInThatLanguage`: a 2-second timeout waiting for the language dropdown. Not yet listed in [docs/testing.md](docs/testing.md#known-flakiness).
  - `SettingsTests.Settings_IsReachableFromTheSidebar_AtTheBottom_AndMarkedAsTheCurrentPage`: listed there.
- [ ] **`ApiTelemetryTests` can time out under load.** `Command_ProducesStoreSpanUnderItsHandlerSpan` failed once when both test projects ran at the same time, and passes alone.
- [ ] **`LandingPageTests` is skipped.** Its `[Skip]` says the expectations need to be switched to TUnit assertions.
