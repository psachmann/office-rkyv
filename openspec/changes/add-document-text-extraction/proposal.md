# Proposal

## Why

A document is a file with a title, and the only way to find one again is to remember that title. Search is the feature the product is named for, and it needs the words inside the files. `TODOS.md` lists text extraction as the prerequisite for search, so this change extracts the text and keeps it, without building search yet.

## What Changes

- After a document is created, the text of its file is extracted in the background. The upload answers as before and does not wait for it.
- For a PDF, each page is decided on its own: a page that already has a text layer uses it, and a page without one is read by OCR.
- An image (PNG, JPEG, TIFF) is read by OCR.
- What kind of file it is, is decided from the content itself, because the recorded content type is only what the client declared. The recorded content type does not change.
- Any other kind of file is recorded as unsupported, and a PDF that cannot be opened as unreadable. Neither affects the document.
- OCR is done by two external programs, `pdftoppm` (poppler) and `tesseract`, run as separate processes, with German and English as languages. When they are not installed the app works as before: text layers are still extracted, and pages that needed OCR are counted as not read.
- The text is stored once per distinct file content, beside that content, and the outcome is recorded: how many pages there were, how many were read by OCR and how many could not be read. Two documents with the same file share one extraction.
- Extraction never changes a document. In particular it does not move the document's version, so a rename right after an upload is not refused as stale.
- Work that was waiting when the app stopped is done after it starts again.
- The Nix dev shells gain `tesseract` with German and English language data, and poppler's tools.

Not in this change, and recorded in `TODOS.md` as later work: search itself; showing the extracted text or the extraction outcome in the UI or the gRPC contract; extracting again (after the tools were installed, or with other languages); producing a searchable PDF; other formats such as office documents and plain text; shipping the OCR programs with a packaged app (Nix package, Flatpak); malware scanning and correcting the recorded content type.

Guideline docs whose conventions this change sets or changes:

- `docs/api.md`: background work as a message on a durable local queue, an aggregate keyed by content hash and not by a document, extracted text stored beside the blob and not in an event, and running an external program.
- `docs/architecture.md`: the text beside the file content as part of a host's data, the external programs the API uses at run time, and the dev shell.
- `docs/testing.md`: fakes for the external programs in pipeline tests, and the one test that runs the real programs and is skipped without them.
- `docs/observability.md`: extracted text added to what must never appear in a span or a log, and the new log messages as examples of their levels.
- `docs/desktop.md`: the desktop app also runs extraction, and what it needs installed for OCR.
- `docs/ui.md`: none.

## Capabilities

### New Capabilities

- `document-text-extraction`: extracting and keeping the text of a document's file: when it happens, which files and pages are read and how, what is recorded, and what happens when the text cannot be read.

### Modified Capabilities

None. A document, its file and its upload behave as specified today; the seed and the desktop data directory need no new requirement.

## Impact

- `src/OfficeRkyv.Api`: a new feature folder for text extraction (aggregate, event, message, handler, query), the pieces it uses under `Shared/` (a text store beside the blob store, the PDF reader, the runner for external programs), `CreateDocumentHandler` publishing the extraction message, the queue configuration and registrations in `AddOfficeRkyvApi`, and a snapshot in `AddAggregateSnapshots`.
- `src/OfficeRkyv.Contracts`, `src/OfficeRkyv.UI`, `src/OfficeRkyv.Web`: none.
- `src/OfficeRkyv.Desktop`: no code change expected; it hosts the API, so it extracts too.
- `tests/OfficeRkyv.UnitTests`: unit tests for the content-kind and per-page rules, pipeline tests for every scenario with fake OCR, one pipeline test with the real programs, and `PipelineHost` able to start a second time on the same data.
- `tests/OfficeRkyv.FunctionalTests`: none.
- `flake.nix`: `tesseract` (German and English) and poppler's tools in both dev shells.
- New dependency: the NuGet package `PdfPig` (Apache-2.0, managed code only) for reading a PDF's text layer.
- Stored data: a new event stream per distinct file content, and one text file beside each blob that has text. Documents that exist before this change are not extracted; see the design.
- `TODOS.md`: "Content inspection" is narrowed to what remains, and the deferred items above are added.
