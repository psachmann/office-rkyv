# Proposal

## Why

Every PDF and image a user uploads now has its text extracted, by text layer or by OCR, and kept beside its blob. Nothing reads it: a scanned invoice is found only if its title or file name happens to say what is on it, and the titles are derived from file names such as `scan_0042.pdf`. Searching the words inside files is what makes the extraction worth having, and it is the feature the product is named for.

This is the first of two changes. It makes the text searchable and changes nothing a user sees except what is found. Showing why a document matched (a snippet, a highlight) and judging whether the engine copes with OCR noise are left for a second change that can use real results.

## What Changes

- `SearchDocuments` also finds a document by the words in the text of its file. A word has to be found somewhere: in the title, in the file name or in the text, and different words may be found in different places. The inbox and every archive are searched, deleted documents and other users' documents are left out, as before. The request, the response and the limit of 50 do not change.
- A document found by its title or file name is listed before one found only by its text.
- The extracted text becomes searchable in the transaction that records the extraction: a document whose upload has been extracted is found by its text without a second step. The text is indexed once per file content and not once per document, so two documents with the same file share it.
- Documents whose text was extracted before this change are searchable too: at start the app indexes the text of every extracted content that has none, from the text files that are already there. No OCR runs again.
- The text of a file, like the search query, never appears in a span or a log.

Not in this change, and kept in `TODOS.md`: showing which part of a document matched; ranking by the text's relevance (this change orders only by title and by where the match is); extracting again for documents that have no extraction; whether Lucene.NET should replace FTS5 once real OCR output can be judged; removing the indexed text together with its blob (the blob collector's job).

Guideline docs whose conventions this change sets or changes:

- `docs/api.md`: the second full-text index, on a document type that is not an event-sourced aggregate, and how the search query joins the two indexes; the start-up indexing of existing texts.
- `docs/observability.md`: the extracted text and the indexed text added to what must never appear in a span or a log, and the test that checks it.
- `docs/testing.md`: a helper for waiting until a content's text is searchable, if the design needs one.
- `docs/architecture.md`, `docs/ui.md`, `docs/desktop.md`: none.

## Capabilities

### New Capabilities

- `document-content-index`: keeping the extracted text of a file content searchable: when it is indexed, that one content is indexed once for all documents that share it, that texts extracted before the index existed are indexed at start (the kept text stays the source), and that the text stays out of telemetry.

### Modified Capabilities

- `document-search`: a document is also found by the words in its file's text, a word may match in the title, the file name or the text, and documents matched by title or file name come first.

## Impact

- `src/OfficeRkyv.Api`: a new document type for the indexed text with a full-text index in the shared store configuration; `ExtractTextHandler` stores it in the same transaction as the extraction outcome; a start-up service that indexes the texts already kept; `SearchDocumentsHandler` joins the two indexes.
- `src/OfficeRkyv.Contracts`, `src/OfficeRkyv.UI`, `src/OfficeRkyv.Web`, `src/OfficeRkyv.Desktop`: no change expected. The RPC, its messages, the dialog and its text are the same; the search finds more.
- `tests/OfficeRkyv.UnitTests`: pipeline tests for every new scenario (extraction fake with prepared text), a test for the start-up indexing on a database written before the change, and a telemetry test for the text. `tests/OfficeRkyv.FunctionalTests`: none expected, since a text match is the same result row; the design decides.
- Stored data: one more document table with its FTS5 virtual table and triggers, created on startup. A trigram index is about three times the size of the text it covers, and the text exists twice (the `.txt` beside the blob, which stays the source, and the indexed copy).
- No new dependency.
- `TODOS.md`: "Search the words inside files" is narrowed to what this change leaves; the blob collector item gains the indexed text; the Lucene item keeps its trigger.
