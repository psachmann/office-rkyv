# Proposal

## Why

The only way to find a document today is to remember which archive it is in and scroll through that list. Search is the feature the product is named for. The words inside a file have to wait for `add-document-text-extraction`, but a document's title and file name are already stored. Searching them now gives users a way to find a document, and proves out Fisher's full-text index, the RPC and the page before content search builds on them.

## What Changes

- A new RPC, `SearchDocuments`, finds a user's documents whose title or file name contains every word of a query. It searches the inbox and every archive, leaves out deleted documents and other users' documents, puts the best matches first, and returns at most 50 results with a flag saying there were more.
- A word matches anywhere inside a title or a file name, ignoring case, so `rechnung` finds `Stromrechnung März`. Words shorter than 3 characters are ignored. A query left with no word of 3 or more characters is refused as `INVALID_ARGUMENT` with the new violation code `too_short`.
- The index is built and kept up to date by the store, in the transaction that writes the document, so a document can be found as soon as it is created and stops showing up as soon as it is renamed or deleted.
- The workspace gains a search field above the document list. While it holds a query, the list shows the results across the inbox and all archives, each with where it lives; clearing it brings back the selected list. Results update as you type, after a short pause. The query is kept in the page address, so a reload or the back button keeps the search. Under 3 characters, a hint asks for more instead of showing results. A result row offers the same actions as any document row.
- The search query, like a title, never appears in a span or a log.

Not in this change, and kept in `TODOS.md`: searching the words inside files (waits for `add-document-text-extraction`); matching without accents, such as `Marz` for `März`; pagination beyond the first 50 results; highlighting the matched part.

Guideline docs whose conventions this change sets or changes:

- `docs/api.md`: the first full-text index, declared in the shared store configuration, and how a search handler builds its query.
- `docs/ui.md`: page state kept in the address, search as you type with a pause, and the search results refreshed by every document mutation.
- `docs/observability.md`: the search query added to what must never appear in a span or a log.
- `docs/testing.md`, `docs/architecture.md`, `docs/desktop.md`: none.

## Capabilities

### New Capabilities

- `document-search`: finding a user's documents by words in their title or file name: what matches, which documents are searched, the order and the limit of the results, and which queries are refused.

### Modified Capabilities

- `user-workspace`: the workspace gains a search field, the results view, the short-query hint, and keeping the query in the page address.

## Impact

- `src/OfficeRkyv.Contracts`: `SearchDocuments` and its messages in `documents.proto`; `ViolationCodes.TooShort`.
- `src/OfficeRkyv.Api`: the full-text index on `Document` in the shared store configuration, the search query, its validator and handler, the word rule, the mapping, and the service method.
- `src/OfficeRkyv.UI`: a search query on `DocumentsFacade` with its own key kind, refreshed by every document mutation; the search field, results and hint on `Workspace.razor`; the `too_short` case in `WorkspaceErrors`; English and German resources.
- `src/OfficeRkyv.Web`, `src/OfficeRkyv.Desktop`: no code change expected; both host the UI and the desktop hosts the API.
- `tests/OfficeRkyv.UnitTests`: unit tests for the word rule, pipeline tests for every `document-search` scenario and for the query staying out of spans, UI-logic tests for the facade and the error mapping.
- `tests/OfficeRkyv.FunctionalTests`: a search journey on `WorkspacePage`.
- Stored data: one FTS5 virtual table and its triggers over the document table, created on startup. Documents that already exist are indexed too; the design says how.
- No new dependency.
- `TODOS.md`: "Search" is narrowed to the words inside files, and the deferred items above are added.
