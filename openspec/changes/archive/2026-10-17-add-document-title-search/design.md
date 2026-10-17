# Design

## Context

See `proposal.md` for the motivation and the scope, and the two spec deltas for the behaviour.

`Document` is an aggregate with an Inline snapshot: Fisher writes its current state, as JSON in one row of a document table, in the transaction that appends each event. The inbox and an archive's contents are LINQ queries over that table (`FindInboxHandler`, `FindArchiveDocumentsHandler`), filtered on `OwnerId`, `ArchiveId` and `Status`. Deletion is our own `EntityStatus.Deleted`, not Fisher's soft delete, so every query filters it itself.

Fisher 1.13 has full-text search on stored documents, backed by SQLite's FTS5:

- One index per document type, over named members, declared with `opts.Schema.For<T>().FullTextIndex(tokenizer, members...)` or `[FullTextIndex]`. It is an FTS5 virtual table with the document table as its content, kept current by three triggers on that table, so it changes in the same transaction as the snapshot.
- Three tokenizers: `Porter` (English stemming), `Unicode` (words as written) and `Trigram` (three-character windows, matching anywhere inside a word).
- `NgramSearch(term)` is the only operator a `Trigram` index answers; the word operators are refused against it. It quotes its input, so raw user text cannot be read as FTS5 syntax, drops words with no letter or digit, and refuses terms under 3 characters. Fisher documents it as matching the term as one phrase.
- `OrderByRelevance()` orders by FTS5's `bm25()`, which also works over a trigram index, and composes with `ThenBy`, `Take` and ordinary predicates.
- `AdvancedOperations.CheckFullTextIndexAsync<T>` and `RebuildFullTextIndexAsync<T>` check and repair an index against its table.

The workspace page (`Workspace.razor`) takes the user and the selected archive from its route, and renders the inbox or an archive's documents through `Transition` over queries from `DocumentsFacade`, keyed `[kind, id]` through `WorkspaceKeys`. Mutations invalidate by kind.

Constraints from the guideline docs: a service maps, dispatches and maps back (`docs/api.md`); handlers do not log, and titles and file names never appear in a span or a log (`docs/observability.md`); no hardcoded user-facing text (`docs/ui.md`).

## Goals / Non-Goals

**Goals:**

- A search that finds a document by any part of a word in its title or file name, which German compound words need (`rechnung` in `Stromrechnung`).
- An index that cannot drift from the documents: no second write path, no background job.
- Search added to the existing workspace and its existing rows and actions, not a second page.

**Non-Goals:**

- The words inside files. Change 2 adds them, and may move the index to a type of its own (see D1).
- Stemming, accent folding (`Marz` for `März`), typo tolerance, and highlighting the matched part.
- Searching across users, or an index shared with other aggregates.
- A search box on any page other than the workspace.

## Decisions

### D1. A trigram full-text index on `Document.Title` and `Document.FileName`

The index is declared in the shared store configuration, next to the snapshots, in a method that `AddOfficeRkyvApi` calls (so the server, the desktop and `PipelineHost` all get it):

```csharp
options.Schema.For<Document>().FullTextIndex(FullTextTokenizer.Trigram, d => d.Title, d => d.FileName);
```

On `Document` itself, because the snapshot already holds both fields and is already written in the event's transaction. The index's triggers then make "found as soon as the call answered" and "not found by an old title" hold by construction, with no projection or message to keep in step.

Trigram, because the users write German: a compound word holds the word being searched for (`Stromrechnung`, `Mietvertrag`), and only a trigram index can match inside a word. FTS5's trigram tokenizer ignores case by default. The cost is an index about as large as the text it covers, which for titles and file names is small, and no stemming, which German needs less than substring matching.

*Alternatives:*

- `Porter`. Rejected: it stems English, and gets German wrong or not at all (`Rechnungen` stays `rechnungen`).
- `Unicode` with `PrefixSearch`. Rejected: it finds `Rechnung März` for `rech`, but not `Stromrechnung` for `rechnung`.
- A separate search document type, filled by a projection, which change 2 would also fill with text. Rejected for now: in this change it would copy two fields of the snapshot into a second table, for nothing. The real problem of that type is fanning one content's text out to every document with that content, and that problem only exists in change 2. Moving the index there later is a schema change Fisher's migration applies, plus `RebuildFullTextIndexAsync`.
- `LIKE '%term%'` over the JSON. Rejected: a full scan on every keystroke, no relevance order, and it would need its own escaping of `%` and `_`.

*Verified at:* pipeline (every scenario of "A user's documents can be searched by title and file name" and "Search results follow every change to a document").

### D2. The query is split into words, and every word has to match

A small pure rule, `SearchTerms` in `OfficeRkyv.Contracts` (shared with the UI, like `FileLimits`, so the page can say "type more" with exactly the server's rule), turns the query into the words to search for: trim, split on whitespace, drop words with no letter or digit, drop words of fewer than 3 characters. The handler runs one raw FTS5 query through `IAdvancedSql`, with each word quoted and ANDed, so the words may match in any order and in either field, ordered by FTS5's `bm25()`:

```csharp
// match: "invoice" AND "apr"  (each word in double quotes, a double quote inside a word doubled)
select {fields of d}
from fi_fts_document f
join fi_doc_document d on d.rowid = f.rowid
where fi_fts_document match ?
  and json_extract(d.data, '$.ownerId') = ? and json_extract(d.data, '$.status') <> ?
order by bm25(fi_fts_document), json_extract(d.data, '$.title')
limit 51
```

The columns come from `SelectFieldsFor<Document>()` prefixed with `d.`, as Fisher documents for raw document queries. The JSON names in the filters and the status value are checked against what Fisher stores in task 2.4, and the pipeline tests fail if they drift.

One `NgramSearch` over the whole query would read `invoice april` as a phrase, so `April invoice` would not be found. Dropping short words follows from the index: a term under 3 characters cannot match a trigram index.

Task 1.1 settled the shape. Chained `Where(d => d.NgramSearch(...))` calls, or `&&`, are accepted and ANDed; a multi-word term is a phrase; `ärger` finds `ÄRGER` (case folding beyond ASCII works) but `ARGER` does not (no accent folding). But `OrderByRelevance()` throws a `BadLinqExpressionException` as soon as the query has two full-text predicates ("cannot tell which one to rank by"; it advises one combined FTS5 query), so the LINQ route cannot rank a multi-word search. The raw query above was run against the index and returned the matches best first.

*Alternatives:*

- Chained `NgramSearch` with relevance for one-word queries only and title order for the rest. Rejected: the "ordered by relevance" requirement would hold only for one word.
- Chained `NgramSearch`, then ordering in C# by our own rule. Rejected: it is not `bm25()`, and the 50-result limit would apply before the order.

*Cost:* the handler depends on Fisher's storage names (`fi_fts_document`, `fi_doc_document`, the `rowid` link and the JSON member names), which Fisher does not promise. Every search scenario of the pipeline tests runs this query, so a Fisher upgrade that changes them fails a test and not a user. The names are constants in one place.

*Verified at:* unit (the word rule: whitespace, short words, punctuation-only words, an empty query), pipeline (any order, every word, one word in each field, search syntax taken literally, the order).

### D3. A search is a query with a validator, answered with a page and a "more" flag

`SearchDocuments(owner_id, query)` returns `repeated DocumentDto documents` and `bool has_more`. It is a query (`SearchDocumentsQuery`) with a FluentValidation validator, like a command, because it has input rules a client can break:

- `query` longer than 200 characters after trimming: `too_long`.
- No word left by `SearchTerms`: `too_short`, a new `ViolationCodes` value. An empty query is `too_short` too, not `required`, so that the client has one rule to explain ("type at least 3 characters").

The handler takes 51 results and returns 50 with `has_more` set when the 51st exists. An unknown owner gives an empty result, as `ListInbox` does.

The archive's name is not in the response: the workspace already holds the user's archives, and resolves the id against that list.

*Alternative:* a total count instead of `has_more`. Rejected: FTS5 counts by scanning every match, and the UI only needs to say "there are more".

*Verified at:* pipeline (the limit, the order, the refusals with their field and code, through the gRPC client and `GrpcFailure`).

### D4. Documents that existed before the index are indexed by the migration

An external-content FTS5 table is filled by its triggers, and triggers only see rows written after they exist. A desktop database created before this change holds documents the triggers never saw.

Task 1.1 settled it: Fisher's schema migration fills a newly created index from the existing table. A store started with the index on a database file that a store without it had written (a document `Old Rechnung`) found that document by `NgramSearch`, and `CheckFullTextIndexAsync<Document>()` passed. **No start check or rebuild is needed.** If a later Fisher version stops doing this, `CheckFullTextIndexAsync<Document>()` is the way to ask, since a stale index returns fewer rows and no error.

*Verified at:* pipeline (a `PipelineHost` started on a database written by a store without the index, then searched), by hand (a desktop data directory from before this change).

### D5. The page address holds the query; typing replaces the history entry

The workspace reads the query from the address (`?q=`, `[SupplyParameterFromQuery]`), and the address is the only source of the query: the search field writes to it, and the page reacts to it in `OnParametersSet`, the way it already reacts to `ArchiveId`.

- After a pause of 300 ms in typing, the page navigates to its own address with the new query. The first navigation from no query to a query adds a history entry; every later one while the query is non-empty replaces it (`NavigateTo(..., replace: true)`). Going back from a search therefore returns to the unsearched list, not to the search minus its last character.
- Clearing the field navigates to the address without `q`. Selecting a navigation entry links to that entry's address, which has no `q`, so the search clears without extra code.
- When `SearchTerms.From(query)` finds no word, the page shows the hint and does not set the query's arguments, so the API is not called. It is the same rule the server applies (D2, D3), which stays the authority.

On the desktop the address is the `BlazorWebView`'s internal one. Reload and back are not offered there, but the same code runs.

*Alternative:* the query held only in component state. Rejected: a reload loses it, and a search cannot be bookmarked or sent to oneself.

*Verified at:* functional (reloading keeps the search; opening an address with a search; selecting an entry clears it).

### D6. A search query of its own kind, refreshed by every document change

`DocumentsFacade` gains `SearchDocuments`, an `IQuery<DocumentSearch, SearchDocumentsResult>` with `DocumentSearch(Guid OwnerId, string Query)` as its argument, keyed `[search, ownerId, query]` by `WorkspaceKeys.ForSearch`. The kind `search` is added to `DocumentKinds`, so rename, delete, file, unfile and upload refresh the results. `ArchivesFacade`'s archive deletion adds it to both of its invalidations (the immediate one and the one after `CascadeSettleDelay`), because returning documents to the inbox changes where a result lives.

The results use the page's existing document rows and their actions, with one more cell showing where the document is (the inbox, or the archive's name). The no-results message, the hint, the "more" note and the location cell have no name of their own and get a `data-testid`, as `docs/ui.md` says. The search field is found by its role and accessible name.

`WorkspaceErrors` gains the `too_short` case, so a refusal is explained even though the page should never send such a query.

*Verified at:* UI logic (each document mutation and the archive deletion invalidate the search kind; `too_short` maps to its key), functional (the search journey of the `user-workspace` delta).

## Telemetry and logs

- **Traces:** no new source. The search shows up as the existing gRPC request span with Wolverine's handler span under it; a read opens no Fisher span. The query text is not a span attribute in either: gRPC and Wolverine record the method and the message type only (checked in task 3.6). `ApiTelemetryTests` gains a search and asserts that no span carries the searched word.
- **The web host:** the query is in the page address, so a full page load with `?q=` is an HTTP request, and its server span has a `url.query` tag. ASP.NET Core's instrumentation (1.19) redacts the values by default (`?q=Redacted`), and nothing in the app turns that off; `ApiTelemetryTests` asserts it. Checked by hand in task 7.2: the Aspire AppHost sets `OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION=true` on the processes it starts, so under `just dev` the local dashboard shows the query in the clear. That is a development setting of the dashboard, not of the app, and it is accepted.
- **Metrics:** none new.
- **Logs:** none new. The handler does not log. A refused search is a validation failure, which the error mapping does not log.
- **Never in a span or a log:** the search query, added to the list in `docs/observability.md` beside titles and file names.

## Guideline docs to update

- `docs/api.md`: a full-text index is declared in the shared store configuration, on the snapshot that holds the fields, with the tokenizer chosen for German; a search is a query with a validator; a search handler builds one predicate per word and filters deletion itself; existing rows and the index (D4, as task 1.1 settles it). Add the "Decided in" entry.
- `docs/ui.md`: page state that belongs in the address (`?q=`), with the history rule of D5; search as you type, with the pause; the search kind in the invalidation lists. Add the "Decided in" entry.
- `docs/observability.md`: the search query in the never-list.
- `docs/testing.md`, `docs/architecture.md`, `docs/desktop.md`: none, unless task 4.1 adds a `PipelineHost` restart that the extraction change has not already described; then `docs/testing.md` gains it.
- `TODOS.md`: see the proposal.

No decision departs from a guideline doc.

## Risks / Trade-offs

- [The search query uses Fisher's storage names, which Fisher does not promise] → The names are constants in one place and every search scenario of the pipeline tests runs the query, so an upgrade that changes them fails a test. Task 1.1 found that the LINQ route cannot rank a multi-word search (D2).
- [A trigram index has no stemming, so `Rechnungen` does not find `Rechnung`] → The other way round works (`rechnung` finds `Rechnungen`), which is the common case for a search box. Accepted.
- [No accent folding: `Marz` does not find `März`, `ae` does not find `ä`] → Fisher's tokenizer enum does not expose FTS5's `remove_diacritics` for trigram. Recorded in `TODOS.md`; a German user types the umlaut.
- [Every keystroke after the pause is a new cache entry and a new call] → The pause bounds it, the query is an index lookup over a small table, and DotNetQuery drops unused entries after its cache time.
- [`bm25()` over a trigram index favours short titles strongly] → That is the order the spec asks for. Ties fall back to the title.
- [Change 2 needs the index on another type] → A schema change plus a rebuild (D1); this change's specs stay true, since they speak only of titles and file names.

## Migration Plan

Fisher's `ApplyAllDatabaseChangesOnStartup` creates the FTS5 table and its triggers on the first start after the update. Existing documents are indexed as D4 settles it. Rolling back leaves the virtual table and its triggers in the database; the previous version does not know them, and the triggers keep working on its writes. Nothing to clean up.
