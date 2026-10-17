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
  - It also removes the extracted text (`<hash>.txt` beside the blob) with its blob. The content's extraction outcome stays an event stream; whether the collector forgets it too is that change's to decide. [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (D5) left this to it.
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D4).
- [ ] **Folder watcher.** Pick up files dropped into a folder and create documents from them.
  - Already in place: `DocumentIngest` is the single path a file takes, and the title is derived on the server, so the watcher is a second caller.
  - Which user owns a dropped file is now a matter of naming the user in the command it sends: the bus takes no caller, and `DocumentIngest` is given the user explicitly, as the development seed does. One folder per user is the simple answer.
  - Still to decide: how to wait for a file that is still being written, what happens to the source file afterwards, and whether duplicates are skipped by hash.
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/proposal.md).
- [ ] **In-app preview.** Show a document's file without saving it first.
  - Why postponed: the browser or WebView needs a URL to load, which means a content endpoint per host, and WebKitGTK's PDF rendering is unproven on the experimental backend.
  - Cheap first step for the desktop: "open with the default application".
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D12).
- [ ] **Duplicate handling.** Today the same file uploaded twice gives two documents sharing one blob.
  - Options: refuse, or warn and ask. The hash is recorded on every document, so either can be added without reading files again.
  - Decided in: [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D9).
- [ ] **Resumable uploads.** An interrupted upload starts over. Listed as a non-goal in the same design.
- [ ] **Content inspection.** The content type is recorded as the client declared it; nothing scans the bytes for malware or corrects the recorded type. Text extraction reads the first bytes to decide whether a file is a PDF or an image, but it only decides what to read and changes nothing. Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (D2).

#### Text extraction

The text of every PDF and image is extracted and kept in the background ([add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md)). What was left out of it:

- [ ] **Extracting again.** A content whose extraction needs to be done again: after the OCR programs were installed, with other languages or a newer OCR engine, and for documents created before text extraction existed (they have no outcome at all).
  - Why postponed: it is a feature of its own (what triggers it, whether it replaces a text, what it shows the user), and a backfill at the first start after an update would run OCR over a whole archive unannounced.
  - What is in place: the outcome records `UnreadPageCount`, so the contents worth extracting again are those with unread pages; the text is a second event on the content's stream; the handler is idempotent per content. The query it needs is the same for both cases: contents without a complete outcome.
  - Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (D4, D7, D8).
- [ ] **Showing the text and the outcome.** Neither the extracted text nor the outcome is in the gRPC contract or the UI; a page cannot say "not read" or show what was found.
  - Why postponed: nothing uses it before search or a preview exists. `FindTextExtractionByHash` and `TextStore` are the API side of it.
  - Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (Non-Goals).
- [ ] **A searchable PDF as output.** OCR can produce a copy of a scan with its text layer, so the page can be selected and searched in any viewer.
  - Why postponed: it needs a preview or a download of the derived file to be useful, and OCRmyPDF (a Python application with Ghostscript and qpdf behind it) is heavy to package. It can sit behind the same two interfaces.
  - Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (D6).
- [ ] **Further formats.** Office documents, plain text and other formats are recorded as unsupported.
  - Why postponed: each is a reader of its own, and PDF and images are what a scanner and a phone produce.
  - Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (Non-Goals).
- [ ] **Shipping the OCR programs with a packaged app.** `tesseract` and `pdftoppm` must be on `PATH`; the dev shells provide them. A Nix package or a Flatpak would have to include them and point `TextExtraction:TesseractPath` and `TextExtraction:PdftoppmPath` at them. Same family as the GSettings item under Desktop.
  - Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (Non-Goals, D6).
- [ ] **A sandbox for the OCR programs.** They parse files a user chose and run with the user's rights, as separate processes with fixed arguments and no shell, and nothing more.
  - Why postponed: no sandbox exists for a plain process on every target; worth revisiting when the app is packaged.
  - Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (Risks).
- [ ] **Tuning the reading of real scans.** Three knobs, all changeable without changing what is stored: tesseract's page segmentation (`--psm`) for invoices and letters; the rule for taking a text layer (a page with a stamp or header of 20 letters over a scanned body is taken as text; only OCR-ing pages that contain an image would catch it); and a page cap or a second queue for very large scans, which hold up everything behind them.
  - Why postponed: to be judged on real scans once the feature has run for a while.
  - Decided in: [add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (Risks, Open Questions).

### Documents and users

- [ ] **Tags and other metadata.**
- [ ] **Search the words inside files.** The workspace finds documents by title and file name ([add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/design.md)). The text of a file is now extracted and kept, page by page, beside the blob ([add-document-text-extraction](openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md)), and no search uses it yet; this item puts it into the search.
  - To decide there: whether the text goes into a search type of its own (one extraction is shared by every document with that content, while owner and archive belong to each document) or into `Document`, and whether the `.txt` files stay as the source the index is rebuilt from.
  - Decided in: [add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/design.md) (D1).
  - [ ] **Accent folding.** `Marz` does not find `März`, and `ae` does not find `ä`. Why postponed: Fisher's tokenizer choice does not expose FTS5's diacritics option for a trigram index, and a German user types the umlaut. Revisit with the Lucene.NET item. Decided in: [add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/design.md) (Risks).
  - [ ] **Stemming.** `Rechnungen` does not find `Rechnung`, although `rechnung` finds `Rechnungen`. Why postponed: a trigram index cannot stem, and matching inside a word (`Stromrechnung`) was worth more. Revisit with the Lucene.NET item. Decided in: [add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/design.md) (D1).
  - [ ] **Paging the search results.** A search returns the 50 best matches and says that more matched. Do it with the Pagination item below. Decided in: [add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/design.md) (D3).
  - [ ] **Showing which part matched.** Fisher can highlight a column and cut a snippet, but only through its LINQ operators, and the search is a raw query (D2). Do it when the words inside files are searched, where a snippet matters most. Decided in: [add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/design.md) (D2).
  - [ ] **A search box on every page.** Search is a field of the workspace, because the sidebar and the user are the workspace's. There is now a signed-in user outside the workspace ([add-sign-in](openspec/changes/archive/2026-10-17-add-sign-in/design.md)), so this can be done when a page other than the workspace needs it; the search RPC already acts for the caller. Decided in: [add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/proposal.md).
  - [ ] **Evaluate Lucene.NET as the search engine.** Decide when content search is built on the extracted text, with real OCR output to judge by.
    - What it would add: German stemming (`Rechnungen` finds `Rechnung`), `ae`/`ä` normalisation, fuzzy matching for OCR misreadings, compound-word splitting, highlighting and better ranking of long texts.
    - What it costs: the index leaves the SQLite transaction and can drift, so it needs feeding from the events and a rebuild path; backups become database, blobs and index together; the package is still `4.8.0-beta`; compound splitting needs a German dictionary and hyphenation file, and without them `rechnung` is not found inside `Stromrechnung`, which the trigram index can do.
    - Switch if: OCR text is noisy enough that searches miss documents without fuzzy matching, relevance ranking over long texts is visibly poor, or users miss stemming and `ae`/`ä` more than matching inside words.
    - The engine sits behind one handler and one RPC, so a switch touches the API only. The `document-search` spec's "matches anywhere inside a word" would need rewording. Fisher's hybrid vector search is the other option to weigh at the same time.
    - Decided in: the exploration before [add-document-title-search](openspec/changes/archive/2026-10-17-add-document-title-search/design.md), which chose FTS5 with a trigram index (D1).
- [ ] **Sharing documents between users.** Today a document or archive belongs to one user, and another user's is answered as not found.
- [ ] **What happens to a deleted user's documents and archives.** They are left orphaned: the API no longer accepts the deleted user as a caller, so nobody can reach them.

### Signing in

Signing in is choosing a user from the list, a placeholder on purpose ([add-sign-in](openspec/changes/archive/2026-10-17-add-sign-in/design.md)). What is built is the seam passkeys plug into: `ISignInService`, the `AuthenticationStateProvider` and the caller token.

- [ ] **Passkeys for the web.** Use ASP.NET Core Identity's passkey support with a Fisher-backed store.
  - Why postponed: it needs a credential store of its own and a decision about where the ceremony runs (a Blazor Server circuit cannot answer the browser's WebAuthn calls on a page load), and the placeholder was enough to make the API's isolation between users real first.
  - What stays: the web host would sign the same principal in through the same cookie, and the API would see the same token.
  - Decided in: [add-sign-in](openspec/changes/archive/2026-10-17-add-sign-in/design.md) (D5, D6).
- [ ] **Passkeys or another authenticator for the desktop.** WebKitGTK has no WebAuthn, so the choices are a native authenticator, a password or PIN, or nothing beyond the OS account.
  - Why postponed: all three routes are open, and what the sign-in would protect has to be decided first, since the data is a file the OS account already owns.
  - What stays: `DesktopSignIn` is the one place the desktop's user is set, and the API already requires the caller token on the socket.
  - Decided in: [add-sign-in](openspec/changes/archive/2026-10-17-add-sign-in/design.md) (D7).
- [ ] **Remember the desktop's last user.** Every start of the desktop app is signed out.
  - Why postponed: remembering a user without a secret makes the OS account the only protection, which is the question the passkey item above has to answer.
  - Decided in: [add-sign-in](openspec/changes/archive/2026-10-17-add-sign-in/design.md) (Non-Goals, D7).
- [ ] **Asymmetric caller keys.** The web host signs caller tokens and the API validates them with one shared HMAC key. With more than one issuer, or an API that is reachable from outside, the web should sign with a private key and the API hold only the public one.
  - Why postponed: one AppHost starts both processes and hands them one generated key, and a key pair would need managing for no gain yet.
  - Decided in: [add-sign-in](openspec/changes/archive/2026-10-17-add-sign-in/design.md) (D8).
- [ ] **Pagination.** Every list is unbounded.
- [ ] **Push notifications.** After an archive is deleted the UI refreshes the inbox again after a short delay, with the 30-second refetch as the backstop.

All seven were set aside in [add-archives-and-inbox](openspec/changes/archive/2026-09-29-add-archives-and-inbox/proposal.md) and its design.

## Decisions to revisit

- [ ] **Upload as a DotNet Query mutation.** `DocumentsFacade.UploadAsync` is a plain method because a mutation cannot report progress. If the library gains mutation progress, the upload becomes an ordinary mutation and the exception in [docs/ui.md](docs/ui.md#files-upload-and-download) goes away. The proposal for the library is drafted in [dotnet-query-streaming-issue.md](dotnet-query-streaming-issue.md).
- [ ] **Web download through an HTTP endpoint.** The browser download collects the whole file in memory before saving. Move to a streaming endpoint on the Web host if that is a problem near the 100 MB limit. Decided in [add-document-file-upload](openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D12).
- [ ] **The client's size check uses the default limit.** The upload dialog refuses files over `FileLimits.DefaultMaxFileSizeBytes`. A server configured with a different `FileStorage:MaxFileSizeBytes` is still the authority, but the quick client-side answer would then be wrong. Fix by exposing the configured limit to the UI.
- [ ] **The owner is checked after the content is stored, for callers other than the API.** An upload through the API is refused for an unknown or deleted caller before anything is stored. `DocumentIngest` itself (the seed, a later folder watcher) still checks the owner when the command runs, so a refusal there leaves an orphan blob, as an invalid file name does through the API. Checking the header first would avoid it; the blob collector makes it harmless.
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
