# Design

## Context

See `proposal.md` for the motivation and the scope, and the two spec deltas for the behaviour.

What exists:

- **The search** (`SearchDocumentsHandler`) is one raw FTS5 query over `fi_fts_document`, the trigram index Fisher keeps for `Document.Title` and `Document.FileName`. It joins the index to the document table by `rowid`, filters owner and `EntityStatus.Deleted` itself, orders by `bm25()` and then title, and takes 51 to say whether it cut anything off.
- **The text** of a file content is written by `ExtractTextHandler` to `<blobs>/ab/<sha256>.txt` (`TextStore`, pages separated by a form feed, an unread page kept as an empty one) *before* it starts the `TextExtraction` stream, which records only the outcome and counts. The handler runs inside Wolverine's transaction (`AutoApplyTransactions`), so whatever it stores on the session commits with the event. It already holds the pages in memory when it writes them.
- **`Document.Sha256`** says which content a document has. The text belongs to the content and is shared by every document with that file; owner, archive, title and status belong to the document. That is why the text cannot simply become a third indexed column on `Document`: it is known only after the document exists, and an event on the document to carry it would move the version its writers hold (the reason `TextExtraction` is its own aggregate, `add-document-text-extraction` D4).
- Fisher documents: `session.Store(x)` is an upsert; events, document rows, the FTS triggers and the outbox commit in one transaction; `Schema.For<T>().FullTextIndex(tokenizer, members...)` works on any stored type, not only snapshots. The project stores no plain document yet, only Inline snapshots of aggregates.

Constraints from the guideline docs: a handler does not log, and document contents, the extracted text, file names, titles and search queries never appear in a span or a log (`docs/observability.md`); extracted text is not an event (`docs/api.md`, "Extracted text is not an event either"); the store configuration is shared by the server, the desktop and `PipelineHost` through `AddOfficeRkyvApi`.

## Goals / Non-Goals

**Goals:**

- A document found by the words in its file with no new step for the user, and no window in which an extracted document is not yet searchable.
- The text indexed once per content, whatever the number of documents.
- Existing extractions searchable after the update, without OCR.
- An index that cannot drift from the outcome it belongs to, and can be rebuilt from the kept text.
- The search RPC, its messages and the UI unchanged.

**Non-Goals:**

- Showing why a document matched (snippet, highlight, "found in the text" label). Change 2.
- Ranking by the relevance of the text. Text-only matches are listed by title (D3).
- Extracting documents that have no extraction (created before text extraction existed) or extracting again; that is the "Extracting again" item of `TODOS.md`.
- Stemming, accent folding and typo tolerance for OCR errors, and the decision on Lucene.NET. They need real OCR output to be judged by, which this change makes searchable.
- Removing the indexed text with its blob: there is no blob collector yet.
- Any limit on the size of a text.

## Decisions

### D1. The indexed text is a plain Fisher document keyed by the content hash, stored in the extraction's transaction

A new type, `ContentText`, in `Features/TextExtraction`:

```csharp
public sealed record ContentText(string Id, string Text);   // Id = lowercase hex SHA-256
```

It is not an aggregate and has no events. `ExtractTextHandler` calls `session.Store(new ContentText(sha256, text))` next to `StartStream<TextExtraction>`, only when the outcome is `Extracted`, and Wolverine's transaction commits both. A full-text index sits on it in the shared store configuration, next to the first one:

```csharp
options.Schema.For<ContentText>().FullTextIndex(FullTextTokenizer.Trigram, c => c.Text);
```

The text is the pages in order, empty pages skipped, joined by a line feed. Words in a query contain no whitespace, so a page boundary cannot produce a match, and an unread page contributes nothing. This is a pure function (`ContentText.From(pages)`).

Why this shape:

- **Once per content.** The key is the hash, so a second upload of the same file does not reach the handler's `Store` at all (the handler returns when an outcome exists), and the upsert would be harmless if it did.
- **Together with the outcome.** The row and the event commit in one transaction. There is no window where the outcome is present and the text is not searchable, which the spec states ("as soon as the outcome is present"), and no second message or projection to keep in step. A crash between the `.txt` write and the commit leaves a text with no outcome, as it does today, and the extraction runs again.
- **Documents are not touched.** Owner, archive, title and status stay on `Document`; the join is by hash at query time (D2). A rename, a filing or a deletion needs nothing from the index, and no document version moves.

This is the first stored document that is not an event-sourced aggregate, and the project practises event sourcing on purpose. It is accepted here because `ContentText` is *derived data*, not state: the `.txt` file is the source and the row can be deleted and rebuilt at any time (D4). Nothing about it is a fact that happened.

*Alternatives:*

- **The text in `TextExtractionCompleted`, and in the `TextExtraction` snapshot, with the index on the snapshot.** This keeps everything event-sourced and needs no new type. Rejected: it writes every text into the event log for good (events are never removed), twice more in the snapshot, and reverses "extracted text is not an event" (`docs/api.md`) for a read concern. A later "extract again" would append a second copy. If the project would rather have this, it is a localised change to D1: the handler's `Store` becomes event data, and D4 goes away.
- **The text on `Document`, as a third indexed column.** One FTS table and the query stays the one that exists, with column weights for free. Rejected: the text arrives after the document, so every document with that hash would need to be updated, with an event (moves its version) or without one (breaks the snapshot's meaning). A later duplicate upload would need the same fan-out.
- **An FTS5 table of our own made with raw SQL.** Rejected: it would sit outside Fisher's schema management and the pipeline tests' store, and need its own triggers or writes.
- **A Fisher projection from events into `ContentText`.** Rejected: the text is not in any event.
- **A key of `Guid` derived from the hash.** Not needed: Fisher accepts the string (task 1.1).

*Verified at:* unit (the page join: empty pages, one page, form feed in a page), pipeline (every scenario of `document-content-index`, using the extraction fake with prepared text and born-digital PDFs).

*Spike result (task 1.1), on Fisher 1.13.0:*

- **A string `Id` works.** `ContentText` is a record with `string Id` and `string Text` (settable by `init`); Fisher stores `{"id":"aaaa...","text":"..."}` as JSON in `fi_doc_contenttext` (columns `id`, `data`, `dotnet_type`, `last_modified`). No derived `Guid` is needed.
- **The FTS table and its triggers are created for a plain document type**: `fi_fts_contenttext` with `_ai`, `_au` and `_ad` triggers, linked to the document table by `rowid`. `match '"rechnung"'` found a row whose text held `Zahnarztrechnung`. The text of the pages is joined by a line feed and empty pages are skipped, as the sketch above says.
- **One transaction with the event.** A session that stored a `ContentText` and then started a `TextExtraction` stream whose natural key already existed was refused with `DuplicateNaturalKeyException`, and the `ContentText` row of that session was not there afterwards. That is Fisher's unit of work and holds in the Wolverine handler too, since the transaction middleware commits the same session; the pipeline test of task 2.3 checks the real handler.

### D2. The search runs the existing title query first, then a text query, and joins by content hash

Matching "every word, somewhere" across two FTS tables cannot be one `MATCH`. The handler therefore keeps the existing query as it is (the *name* query: every word in the title and file name, `bm25()` order) and adds a second (the *text* query) for documents it did not return:

```
name query:   documents whose index row matches  "w1" AND "w2" ...        order by bm25, title
text query:   documents that are not in the name query's set, where for every word wi:
                 the document's index row matches "wi"
              OR the document's Sha256 is in (ids of the content texts that match "wi")
              order by title
```

Per word, the text index is asked once as an `IN (select ... where fi_fts_contenttext match ?)` subquery, which SQLite evaluates once, not per document. The rest of the shape is unchanged: owner and `EntityStatus.Deleted` filtered in the SQL, each word one quoted FTS5 string (`MatchAll`'s quoting, reused), so no word can be read as search syntax.

The limit works over both: the name query takes 51. If it returns 51 the answer is the first 50 with `has_more` and the text query never runs. Otherwise the text query takes `51 - n` more, and the answer is the combined list, cut to 50, with `has_more` when the total reached 51. The answer is a list in two groups, which the spec states.

Why two queries and not one with a `UNION`: the name group is ranked by `bm25()` of the name index; the text group has no comparable score. A union would need a synthetic rank column anyway, and it would run the text subqueries even when the first query fills the page. Two statements keep each query simple and the existing one untouched.

Task 1.1 ran this shape against a document titled `Lease` whose content text read `Monthly rent of 950 euros`: the query for `lease rent` (the word `lease` in the name index, `rent` in the text index) returned the document, and the name query's set was excluded with `d.rowid not in (select rowid from fi_fts_document where fi_fts_document match ?)`.

The text query uses the storage names of the new table (`fi_doc_contenttext`, `fi_fts_contenttext`, the `id` column and the `rowid` link), which Fisher does not promise, like the existing query: they are constants in the handler, and every search scenario runs both queries, so an upgrade that changes them fails a test.

*Alternatives:*

- One `UNION` query. See above.
- A combined score from both `bm25()`s. Rejected: they are computed over different corpora and are not comparable, and a made-up weighting would be hard to explain and to test.
- Run the text index first and filter documents in C#. Rejected: the 50-result limit and deletion have to be applied in SQL, before the cut.
- Matching all words in the text only (no mixing). Rejected by the spec: `lease rent` has to find a document titled `Lease` that says `rent` inside.

*Verified at:* pipeline (the scenarios of `document-search`, including the order of the two groups, the limit across them and a document listed once), unit for the SQL's word handling is the existing `SearchTerms` rule and needs no new test.

### D3. The text group is ordered by title

Results are grouped (name matches before text-only matches), ranked by `bm25()` in the first and by title in the second. Relevance over long OCR text is exactly what the Lucene.NET item of `TODOS.md` says to decide with real output. Ordering by title is deterministic and testable; a bad ranking is less costly than a wrong one, and the Lucene item keeps its trigger ("relevance ranking over long texts is visibly poor").

*Alternative:* `bm25()` of the text index within the text group (for one word it is a single `ORDER BY`; for several words the per-word subqueries have no score to order by). Rejected for this change, because it is right for one word and arbitrary for several.

*Verified at:* pipeline ("Found by text, in title order", "A match in the title comes before a match in the text").

### D4. Existing extractions are indexed by a service at start, from the kept text

`ContentTextIndexer`, a hosted service registered in `AddOfficeRkyvApi`, does this once per start, in the background so that the start is not held up:

1. Find the `TextExtraction` snapshots with the outcome `Extracted` (LINQ), and the `ContentText` ids that exist.
2. For each hash with an outcome and no row: read the pages with `TextStore.ReadPagesAsync` (no OCR), and `Store` the row. Batches of 100 per `SaveChangesAsync`, so a large archive is not one transaction.
3. A hash whose text is missing (`ReadPagesAsync` returns null) is skipped and counted. A cancellation (`ApplicationStopping`) stops it between batches.

It is idempotent: `Store` is an upsert, a row it finds is not touched, and it can run at the same time as `ExtractTextHandler` for the same hash, because both write the same text under the same key. It covers every case the spec lists with one rule: data from before this change, a row that was deleted, a crash. The `.txt` file stays the source.

It must not run before Fisher has created the new table. The service does not depend on the order in which hosted services start: it calls `IDocumentStore.ApplyAllConfiguredChangesToDatabaseAsync()` first, which task 1.1 showed to be harmless when the schema is already there (it was called on a store that had applied it at start). Documents without any extraction (created before text extraction existed) are not the indexer's: it reads outcomes, not documents.

*Alternatives:*

- A Wolverine message per missing hash, published at start. Rejected: it adds a queue and an outbox write per content for work that is cheap, local and repeatable.
- A check-and-rebuild call like `CheckFullTextIndexAsync`. Rejected: that repairs an *index* against its table. What is missing here is table rows, and Fisher cannot know them.
- Doing it in the migration (a SQL `INSERT ... SELECT`). Rejected: the text is in files, not in the database.

*Verified at:* pipeline (a host started again on data whose `ContentText` rows were deleted while it was stopped, `StopKeepingDataAsync` and `StartAsync(databasePath: ...)`; a second start changing nothing; an outcome whose `.txt` is missing), by hand (D6).

### D5. The extracted text appears in no span and no log

The text moves through `Store` and into the SQL of an insert. `docs/observability.md` already says bound parameter values never appear in a span, and the telemetry subscription is the one that decides. The indexer logs only counts and hashes, through `[LoggerMessage]` methods: one Information line at the end (`{Indexed}` contents, `{Missing}` without text), one Warning per hash with no kept text, named by hash.

`ApiTelemetryTests` gains the scenario "No span carries the text": it uploads a PDF whose text layer holds a distinctive word, waits for the extraction, searches for it, and asserts that no span attribute or event of the extraction or the search contains it. The indexer's logs are checked with `CapturingLogger` in a unit test.

*Verified at:* pipeline (telemetry test), unit (the indexer's log messages carry hashes and counts only).

### D6. By hand: the desktop, with a real data directory

The desktop hosts the API in the same process and starts the indexer. A data directory written before this change is what the indexer is for, and `PipelineHost` has none that a user made. A task checks it by hand: run `just desktop` on a data directory with extracted documents, search for a word that is only in a document's text, and observe the stdout: the Information line of D5 with the number indexed, no Warning or Error, and no line containing the searched word or any of the text.

## Telemetry and logs

- **Traces:** no new source. The extraction is traced as today (`ExtractText` and the content's hash); the search as before (the gRPC span and Wolverine's handler span). The extraction's write adds Fisher's own statement span if its subscription records one; neither carries a bound value (D5).
- **Metrics:** none new.
- **Logs:** the indexer's two messages (D5). No handler logs.
- **Never in a span or a log:** the text of a file, in the extraction and in the index; `docs/observability.md` already lists it for the extraction, and gains the index and the new test.

## Guideline docs to update

- `docs/api.md`: Search section: the second index and why it is on a content-keyed document; the text query and its two groups (D2, D3); the start-up indexer and that the `.txt` stays the source (D4). "Background work"/"Extracted text is not an event either": one sentence that the searchable copy is derived data. Add the "Decided in" entry.
- `docs/observability.md`: the indexed text and the new telemetry test in the never-list.
- `docs/testing.md`: the pipeline helpers this change adds (reading `ContentText` rows, removing them while the host is stopped), if task 1.1 and 4.1 add any. Otherwise "nothing new".
- `docs/architecture.md`, `docs/ui.md`, `docs/desktop.md`: none.
- `TODOS.md`: see the proposal.

Decisions that depart from a guideline doc: D1 stores a document that is not an event-sourced aggregate. `docs/api.md` describes every stored type as an aggregate; the doc gains a line saying that derived data may be a plain document, with the `.txt` as its source and a rule that anything that is a fact stays an event.

## Risks / Trade-offs

- [The index is about three times the size of the text, and the text now exists twice (`.txt` and the database)] → Accepted for a personal archive; measured on real scans before change 2 decides on Lucene. The `.txt` stays because it is the source the index is rebuilt from.
- [A very long scan makes a large `ContentText` row and a large write inside the extraction's transaction] → The extraction already takes seconds a page, and the write is one row. No limit in this change; the blob collector and a size look belong with real data.
- [OCR errors: `Rechnnng` does not match `Rechnung`] → The trigram index matches the parts that are right (`Rech`, `chn`, `nun`); no typo tolerance. This is the trigger of the Lucene item in `TODOS.md`, which this change now makes observable.
- [The text query uses Fisher's storage names] → Constants in one place; every search scenario runs the query (D2).
- [A per-word `IN` subquery against the text index for a query with many words] → A query is at most 200 characters and each word at least 3, so at most 50 words, and each subquery runs once. If a real archive makes it slow, change 2 can reconsider.
- [The service and the handler write the same row at the same time] → Both write the same text under the same key as an upsert; the last write wins and is identical.
- [`has_more` and the order depend on two statements read at different instants] → A single read session per search; no write is needed for consistency, since a document appearing between the two statements would at worst be listed in neither or the later group.

## Migration Plan

Fisher's `ApplyAllDatabaseChangesOnStartup` creates the document table, the FTS5 table and the triggers on the first start after the update. The indexer then fills the table from the kept texts in the background; until it finishes, older documents are found by title and file name only, as before. Rolling back leaves the table and triggers in the database, which the previous version ignores. Nothing to clean up.
