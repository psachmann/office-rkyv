# Design

## Context

See `proposal.md` for the motivation and the scope.

A document records four facts about its file (`FileName`, `ContentType`, `Size`, `Sha256`) on `DocumentCreated`; the bytes are in the blob store, a directory of files named by their SHA-256 (`<blobs>/ab/abcdef...`, with `tmp/` for work in progress). `DocumentIngest` is the one way a file becomes a document: it writes the blob and then dispatches `CreateDocumentCommand`. The content type is what the client declared, and `application/octet-stream` when it declared none.

Wolverine is already the message bus, with `DurabilityMode.Solo` and `AutoApplyTransactions`, integrated with Fisher (`IntegrateWithWolverine()`). One process already uses it for work after a request: deleting an archive returns one `ReturnDocumentToInbox` message per document, stored in the outbox in the transaction of the event. No local queue is configured explicitly today.

Three processes host the API through `AddOfficeRkyvApi`: the API server, the desktop app (API and UI in one process) and the pipeline tests' `PipelineHost`. Whatever this change registers there runs in all three.

Every command on an existing document carries the expected version, and a stale one is refused as `ABORTED`. The UI holds the version it got from the upload.

Constraints from the guideline docs: file content is not an event (`docs/api.md`); handlers do not log, and document contents, file names and titles never appear in a span or a log (`docs/observability.md`); the app has no `ActivitySource` of its own.

## Goals / Non-Goals

**Goals:**

- Text for every PDF and image a user uploads, without the upload waiting for it.
- No OCR where a text layer already has the words.
- An app that starts and works when the OCR programs are missing, and that records what it could not read, so that a later change can make up for it.
- A crash of an OCR program that cannot take the API, or on the desktop the window, down with it.
- Nothing about extraction visible to a document's writers: no version change, no new refusal.

**Non-Goals:**

- Search, and any index over the text.
- Showing the text or the outcome in the UI, or adding them to the gRPC contract.
- Extracting again: after the tools were installed, with other languages, or with a newer OCR engine.
- Extracting documents that existed before this change.
- A searchable PDF as output.
- Formats other than PDF, PNG, JPEG and TIFF.
- Shipping `tesseract` and `pdftoppm` with a packaged app.
- Isolating the external programs beyond a separate process (no sandbox).
- Removing a text when its blob is removed; that belongs to the blob collector, which does not exist yet.

## Decisions

### D1. Extraction is a message on a durable, sequential local queue

`ExtractText(Sha256)` is a Wolverine message with its own local queue, configured in `AddOfficeRkyvApi`:

```csharp
options.LocalQueueFor<ExtractText>().UseDurableInbox().Sequential();
```

Durable, so a waiting extraction survives a restart. Sequential, because OCR takes seconds per page and all of a core: one at a time keeps a desktop machine usable, and it makes the handler the only writer of extraction outcomes, which D4 relies on.

`CreateDocumentHandler` publishes the message in the transaction that appends `DocumentCreated`, so a committed document always has its message. The handler saves the session itself today, before it returns; the message has to be enrolled before that save and not cascaded after it, or a crash between the two would lose it. Which call does that with Fisher is settled by the spike in task 1.1.

The message carries only the hash. It names the content, not a document, which is what D4 keys on, and it keeps the file name and title out of Wolverine's spans.

*Alternatives:*

- A hand-written `BackgroundService` reading a `Channel<T>`. Rejected: it loses what is queued on a restart, so it needs a scan at start to find what is missing, and it duplicates what Wolverine already does in this process.
- Extracting inside the upload call. Rejected: a 50-page scan would hold the call for minutes.
- Several extractions in parallel. Rejected for now; `Sequential()` is one line to change when a server host wants more.

*Verified at:* pipeline (the restart scenario, with `PipelineHost` started a second time on the same data and a fake OCR that blocks until the first host is stopped).

*Depends on:* task 1.1, which checks that `WolverineFx.Fisher` persists a durable local queue on SQLite. If it does not, the fallback is a hosted service that, at start, publishes `ExtractText` for every distinct hash among the documents that has no outcome; D4 makes that idempotent. The task records which applies before anything is built on it.

### D2. The kind of file is read from its first bytes

The extractor looks at the leading bytes of the content: `%PDF-` is a PDF, and the PNG, JPEG and TIFF signatures are images. Anything else is `unsupported`. The recorded content type is neither read nor changed.

The declared type cannot be trusted for this: a browser may send none, and the seed and a later folder watcher choose their own. A wrong guess would either skip a readable PDF or hand a ZIP archive to an OCR program.

This is not the "content inspection" of `TODOS.md`: nothing is refused and the document does not change. That item stays, narrowed to malware scanning and correcting the recorded type.

*Alternative:* trusting the content type and falling back to the file extension. Rejected: two unreliable signals instead of one reliable one, and the handler would need the document, not just the hash.

*Verified at:* unit (the signatures, a file shorter than a signature, an empty read).

### D3. A PDF is decided page by page, with a fixed threshold

PdfPig opens the PDF and gives each page's text layer. A page whose text layer has at least 20 letters or digits contributes that text. Any other page is rendered and read by OCR, and the OCR result is the page's text, replacing the few characters the layer had. The threshold is a constant.

Page by page, because scanners and print drivers produce mixed files: a typed cover letter in front of scanned attachments. A threshold above zero, because a scan often carries a layer that holds only a page number or the scanner's stamp.

A PDF that PdfPig cannot open, or that is encrypted, is `unreadable`. A page PdfPig cannot read is treated as a page without a text layer.

*Alternatives:*

- Decide per document. Rejected: a mixed file would lose its scanned pages or OCR its typed ones.
- OCR every page and ignore text layers. Rejected: slower by orders of magnitude and less accurate than the text that is already there.
- OCR only pages that contain an image. Rejected as a first rule: it misses nothing the threshold catches today, and adds a second condition to explain. It can refine the rule later without changing what is stored.

*Verified at:* unit (the rule on a page's text: 0, 19, 20 letters, whitespace and punctuation not counted), pipeline (the born-digital, scanned and mixed scenarios, with PDFs built in the test by PdfPig's document builder).

### D4. The outcome is an aggregate keyed by the content hash, not an event on the document

A new aggregate, `TextExtraction`, has one stream per distinct file content, with `Sha256` as its `[NaturalKey]`, the way `User.NormalizedEmail` is. Its first and, in this change, only event is:

```csharp
public sealed record TextExtractionCompleted(
    string Sha256,
    TextExtractionOutcome Outcome,   // Extracted, Unsupported, Unreadable
    int PageCount,
    int OcrPageCount,
    int UnreadPageCount,
    int CharacterCount
);
```

The handler of `ExtractText` first looks for an outcome with that hash and does nothing when there is one. Otherwise it extracts, stores the text (D5), and starts the stream. The sequential queue makes it the only writer; the natural key is the store's guarantee behind that.

The outcome is not recorded on the document's stream, for two reasons:

- **It would move the document's version.** The UI holds the version the upload returned. An event appended by the extraction a moment later would make the user's first rename or filing fail as a stale write, after every upload. `ReturnDocumentToInbox` has this effect too, but only after deleting an archive.
- **The text belongs to the content.** Two documents with the same file have the same text. Keyed by the hash, "extracted once" holds by construction, as "stored once" does for the blob.

The counts are recorded because a later change needs them: `UnreadPageCount > 0` is exactly the set of contents worth extracting again once OCR is available.

A query, `FindTextExtractionByHash`, returns the snapshot. Nothing in the gRPC contract uses it yet; the tests do, and search will.

*Alternatives:*

- `DocumentTextExtracted` on the document. Rejected for the two reasons above.
- No record at all, the text file's existence being the state. Rejected: "unsupported" and "unreadable" would have no file, so those contents would be looked at again on every duplicate upload, and nothing could be queried.
- A plain table outside the event store. Rejected: the store is Fisher, the project practises event sourcing on purpose, and a later "extracted again" is naturally a second event on this stream.

*Verified at:* pipeline (the outcome after an upload, one outcome for two uploads of the same file, the document's version unchanged and a rename with the upload's version succeeding).

### D5. The text is a file beside its blob, pages separated by form feed

The text is stored as UTF-8 at `<blobs>/ab/<sha256>.txt`, beside the blob it was read from, written into `tmp/` first and renamed, as a blob is. Pages are separated by a form feed character (U+000C), the convention of `pdftotext`; a page that was not read is an empty page, so the n-th page of the text is the n-th page of the file. No file is written for `unsupported` and `unreadable`.

It is not in the event, for the reason file content is not (`docs/api.md`): Fisher stores event and snapshot bodies as JSON text that is loaded with every query. Beside the blob, a later blob collector removes both with one rule, and a copied or replaced data directory keeps them together.

The text is written before the event is appended, the order `docs/api.md` sets for content. A crash in between leaves a text file with no outcome; the extraction runs again and overwrites it.

A small store type owns the layout (write, open for reading, by hash). It lives in `Shared/Blobs`, and the file-system blob store gains one internal member that gives the path of a stored blob, since the external programs in D6 need a path and not a stream.

*Alternative:* a separate `texts/` directory. Rejected: a second tree to keep in step with the first, for no gain.

*Verified at:* unit (round trip, page separation, an empty page kept, nothing left in `tmp` after a failed write), pipeline (reading the text back in the scenarios).

### D6. OCR is two external programs, run as processes

A page without a usable text layer is rendered with `pdftoppm` (poppler) at 300 DPI into a PNG in the blob store's `tmp/`, and read with `tesseract`, which writes the text to its standard output. An image file is given to `tesseract` directly. Both are started with an argument list and no shell; the only paths passed are ones the app built from a hash, never a file name a user chose.

They are processes and not libraries because:

- **A native crash stays outside.** On the desktop the API shares its process with the window. A segmentation fault in an OCR library would close the app; a process that dies is an exit code.
- **No binding to keep in step.** The .NET wrappers for Tesseract load the native libraries under fixed names and expect one ABI version; the command line of `tesseract` 5 and of `pdftoppm` is stable across versions.
- **Packaging is "two programs on `PATH`"**, on Nix today and in a Flatpak later.

The cost is one process start per page, which is small beside the seconds OCR takes.

Two interfaces separate the rule from the programs: one renders a PDF page to an image, one reads an image. The real implementations run the processes; the pipeline tests replace them with fakes that return prepared text and count their calls.

Each program run has a time limit (120 seconds by default) and is killed, with its child processes, when it exceeds it or when the host stops. A run that cannot be started, exits with an error or is killed makes that page unread (D7); the extraction continues with the next page.

Settings are bound from a `TextExtraction` configuration section: the languages (`deu+eng` by default), the two program names or paths (`tesseract` and `pdftoppm` by default, found on `PATH`), and the time limit. The paths are settings so that a packaged app can point at the programs it ships.

*Alternatives:*

- The `TesseractOCR` NuGet wrapper. Rejected for the three reasons above.
- OCRmyPDF, which does the rendering and the OCR in one call and returns a searchable PDF. Rejected for now: a Python application with Ghostscript and qpdf behind it, heavy to package, and its extra output is not needed until a preview exists. It stays possible behind the same two interfaces.
- A PDF renderer as a NuGet package (PDFium). Rejected: it brings the native library into the process again.

*Verified at:* pipeline with fakes (every scenario about pages); pipeline with the real programs for one journey (D9); unit for the argument lists and for how an exit code, a timeout and a missing program become "unread".

### D7. A page that cannot be read is counted; missing programs are not an error

Extraction has three outcomes. `Unsupported` and `Unreadable` are about the file. Everything else is `Extracted`, with the number of pages that were not read: because a program is not installed, failed, or ran out of time. A PDF with 49 typed pages and one scan keeps its 49 pages of text when OCR is unavailable.

At start, a hosted service runs each program once (`tesseract --version` and `--list-langs`, `pdftoppm -v`) and logs the result: Information when both are found and the configured languages are installed, Warning naming what is missing otherwise. It never stops the host.

A blob that is missing when its extraction runs is a fault of the data, as it is for a download: an Error is logged with the hash, no outcome is recorded, and the message is consumed. The pipeline tests that create documents by command with file facts for content nobody stored (`TestDocuments.Command`) take this path, so the handler must not throw there, or `InvokeAndSettleAsync` would fail tests that have nothing to do with extraction; the task that adds the handler checks those tests.

*Alternative:* failing the extraction when OCR is unavailable and retrying later. Rejected: it keeps the text that could be had from being stored, and "later" needs the re-extraction this change defers.

*Verified at:* pipeline (the three scenarios of the "not fatal" requirement, with a fake that reports OCR as unavailable or fails on a chosen page), unit (the start check's result for each combination, through a fake program runner).

### D8. Documents that exist before this change are not extracted

Only a document created after this change publishes the message. Development databases are thrown away on every AppHost run. A desktop database may hold documents; their text arrives with the re-extraction change that `TODOS.md` gains, which needs the same "find contents without a complete outcome" query either way.

*Alternative:* a one-time backfill at start. Rejected here: it is the re-extraction feature under another name, and it would run OCR over a user's whole archive on the first start after an update, unannounced.

### D9. One test runs the real programs, and is skipped without them

Every scenario is covered with fakes, so the fast test levels need no OCR installed. One pipeline test covers the wiring to the real programs: it builds a PDF with a text page using PdfPig's document builder, renders that page with `pdftoppm`, embeds the image as the only content of a second PDF, uploads it, and checks that the extracted text contains the words. No binary test asset is checked in. The test is skipped, with the reason, when the programs are not on `PATH`; in the dev shell they are, so the pre-push hook runs it.

*Verified at:* pipeline.

## Telemetry and logs

- **Traces and metrics:** no new source or meter. Wolverine, already subscribed, emits the spans and counters for handling `ExtractText`. The message holds only the hash and the event only counts, so no content reaches a span. The time spent in the external programs is inside the handler's span and is not broken down further; the app has no `ActivitySource` of its own, and this change does not add one.
- **Logs**, all through `[LoggerMessage]` methods on the type that emits them:
  - Information, once at start: the OCR programs found, with their versions and languages.
  - Warning, once at start: a program or a configured language is missing; scanned pages will not be read.
  - Warning: an OCR program failed or timed out on a page, with the content hash, the page number and the exit code. Not the program's output.
  - Error: the content of an extraction is missing from the blob store, with the hash.
  - The handler itself does not log; the messages belong to the extractor and the start check.
- **Never in a span or a log:** the extracted text, added to the list in `docs/observability.md`. Content is identified by its hash.
- `ApiTelemetryTests` gains an upload followed by its extraction and asserts that no span carries the text.

## Guideline docs to update

- `docs/api.md`: the text-extraction feature as the example of background work on a durable local queue; an aggregate keyed by content; "extracted text is not an event" beside "file content is not an event"; running an external program (argument list, time limit, no shell).
- `docs/architecture.md`: the text files in the blob directory, the two external programs and what happens without them, the dev shell's new packages, and the request path of an upload continuing into the queue.
- `docs/testing.md`: the fakes for rendering and OCR, building PDFs in a test, starting `PipelineHost` a second time on the same data, and the real-program test with its skip rule.
- `docs/observability.md`: extracted text in the never-list, and the new messages as examples of their levels.
- `docs/desktop.md`: the desktop extracts too, and needs the two programs on `PATH` for OCR; `nix develop .#desktop` provides them.
- `README.md`: the programs as an optional run-time requirement, if it lists requirements.
- `TODOS.md`: see the proposal.

No decision departs from a guideline doc. `docs/api.md` says a feature folder is one aggregate with a gRPC service; this feature has an aggregate and no service yet, which the doc update states.

## Risks / Trade-offs

- [A page with a stamp or header of 20 or more characters over a scanned body is taken as text, and its body is not read] → Accepted as a known limit of the threshold. The counts cannot show it. Refining the rule (D3, the image condition) changes no stored format; it needs the re-extraction change to apply to old files.
- [A very large scan occupies the queue for a long time, and everything uploaded after it waits] → The time limit bounds one page, not one file. Accepted for a single-user app; a page cap or a second queue for large files can be added without changing what is stored.
- [The external programs parse files a user chose, and run with the user's rights] → They run as separate processes with fixed arguments and no shell. A sandbox is a non-goal here and worth revisiting when the app is packaged.
- [OCR quality depends on the scan and on the installed language data; the stored text may be poor] → The original file is untouched, and re-extraction is the planned way to improve a text.
- [Text files accumulate for contents whose documents were deleted] → Same as blobs today; the blob collector item in `TODOS.md` is extended to remove the text with the blob.
- [`WolverineFx.Fisher` may not persist local queues on SQLite] → Task 1.1 checks it first; the fallback in D1 needs no change to the specs.
- [The development seed's three sample PDFs are extracted on every `just dev` run, since each run has a new database] → One page each; negligible when they have text layers. If they turn out to be scans and slow the start, the seed can be switched off (`Seed__DemoData=false`).

## Migration Plan

Nothing to migrate. The new stream type and snapshot table are created by `ApplyAllDatabaseChangesOnStartup`. Existing documents keep working and have no text (D8). Rolling back leaves `.txt` files beside blobs and unused streams, both ignored by the previous version.

## Open Questions

- Whether `tesseract`'s default page segmentation is good enough for invoices and letters, or a different `--psm` should be the default. It changes one argument and no stored format; to be judged on real scans once the feature runs.
