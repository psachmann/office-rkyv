# API

How the API (`src/OfficeRkyv.Api`) and its contract (`src/OfficeRkyv.Contracts`) are built: the shape of a feature, the handler styles, the event-sourcing rules, validation, and how a failure reaches a client. Read it before adding or changing an RPC, a command, an event or an error code. `Features/Archives` is the worked example throughout.

## A feature is one folder

Each aggregate has a folder under `Features/` with the same files:

| File | Holds |
|---|---|
| `Archive.cs` | The aggregate: a record deriving from `EntityBase`, with a static `Create` for the first event and one `Apply` per later event. |
| `ArchiveEvents.cs` | The events, as payload-only records. Fisher supplies the envelope (stream id, version, timestamp). |
| `ArchiveCommands.cs` | The commands. A command on an existing aggregate carries `<Aggregate>Id`, the expected `Version` and the `CallerId` that acts. |
| `ArchiveQueries.cs` | The queries. |
| `ArchiveMapper.cs` | A Mapperly mapper: requests to commands and queries, aggregates to DTOs and responses. |
| `ArchivesServiceImpl.cs` | The gRPC service. It resolves the caller, maps, dispatches on `IMessageBus`, and maps back. No logic. |
| `Handlers/` | One static handler class per command or query, the validators, and the existence check. |

What features share is under `Shared/`: `EntityBase` and `EntityStatus`, the exceptions, the error mapping, the caller (`Shared/Auth`), the text rules for validators, the snapshot registration, the telemetry subscription, the blob store for file content and the text beside it (`Shared/Blobs`), and what reads a file's text (`Shared/Extraction`: the extractor, the two OCR programs, the runner for external programs).

A feature does not need a service. `Features/TextExtraction` has an aggregate, its event, a message, a handler and a query, and no gRPC service or mapper: nothing in the contract exposes the extraction yet, and the tests and a later search use the query. It is keyed by a file content's SHA-256 and not by a document, which sets it apart from the other aggregates: see [Background work](#background-work).

The service names its RPCs after the resource (`ListArchives`, `RenameArchive`); the split into commands and queries is the server's own business and does not show in the contract.

## The caller

Every archive and document RPC, and `UpdateUser` and `DeleteUser`, is made by a user, and the API decides which. The UI host sends a signed caller token with each call (`Authorization: Bearer`, see [architecture.md](architecture.md#one-request)); `AddOfficeRkyvApi` registers JwtBearer authentication that checks its signature, issuer, audience and lifetime, and `MapOfficeRkyvApi` reads it. The scoped `CallerAccessor` (`Shared/Auth`) gives a service `RequireAsync(ServerCallContext)`: the user id from the token's `sub`, then a `FindUserByIdQuery`. A missing, invalid or expired token, or a user that is unknown or deleted, throws `UnauthenticatedException`.

- **Not `[Authorize]`.** The authorization middleware answers with an HTTP 401 that the client turns into a status with a message of its own, past the one place that maps failures. A refusal here takes the ordinary path to a status, with our fixed text. The cost is one snapshot read per call.
- **A service calls `RequireAsync` first,** before it reads anything of the request. The upload does it before it reads the header, so a refused upload stores no content. This is the one thing besides mapping a service does, and it owns no business rule.
- **The owner is never a request field.** The service puts the caller into the command or query: `OwnerId` on what creates or lists (`CreateArchiveCommand`, `FindArchivesByOwnerQuery`, `FindInboxQuery`, `SearchDocumentsQuery`, `UploadHeader`), `CallerId` on a command on an existing aggregate, and `OwnerId` on `FindArchiveDocumentsQuery`.
- **The existence check is also the ownership check.** `ArchiveExistence.RequireAsync` and `DocumentExistence.RequireAsync` take the caller, and throw `NotFoundException` when the aggregate is missing or its owner is someone else, before the version is looked at. Another user's resource is answered exactly as an unknown one. `FileDocumentHandler` treats an archive that is not the document's owner's the same way (`archive_owner_mismatch` is no longer produced; the code stays, since codes are never removed).
- **Users are open except for changing one.** `ListUsers`, `FindUserById`, `FindUserByEmail` and `CreateUser` need no caller, because the landing page calls them signed out. `UpdateUser` and `DeleteUser` require one and throw `PermissionDeniedException` unless the caller is the user named, whether or not that user exists.
- **The bus stays caller-free.** Handlers never read an `HttpContext`. The development seed, the archive-deletion cascade and a later folder watcher name the user explicitly in the commands they send.

## Handlers

A handler is a public static class with a static `Handle` method; Wolverine finds it by convention and injects the session. There are three shapes, and the command decides which (a fourth shape, a streaming service method, is below):

- **Start a stream** (`CreateArchiveHandler`): takes `IDocumentSession`, checks what the new aggregate depends on (its owner exists and is active), calls `StartStream`, saves, and returns the snapshot.
- **Decider** (`RenameArchiveHandler`, marked `[DeciderFunction]`): takes the command and the current aggregate and returns the events to append. The workflow loads the stream at the command's expected version and appends inside the same transaction. Use it for every command on one existing aggregate. A decider that also sends messages returns `(Events, OutgoingMessages)` (`DeleteArchiveHandler`).
- **Session handler across streams** (`FileDocumentHandler`): when one command writes to two streams, it uses `IDocumentSession` and `FetchForWriting` on both, in a fixed order of checks.

Queries are static handlers over `IQuerySession` (`FindArchivesByOwnerHandler`). A list query leaves out deleted aggregates.

Rules that hold for all of them:

- **Unknown id is "not found", not "stale".** A command on an existing aggregate has a `ValidateAsync` step that calls the feature's existence check (`ArchiveExistence.RequireAsync`, with the command's `CallerId`). It runs before the stream is loaded at the expected version, so an unknown id, or one that belongs to someone else, throws `NotFoundException` instead of failing the version check.
- **Deleting is soft and idempotent.** Deletion appends an event and sets `EntityStatus.Deleted`. Deleting a deleted aggregate succeeds and appends nothing.
- **No change, no event.** Renaming to the same name or filing into the same archive appends nothing.
- **Handlers throw; they know nothing about gRPC.** A refused business rule is `InvariantViolationException` with a code; a missing resource is `NotFoundException`.
- **Handlers do not log.** See [observability.md](observability.md).

### A streaming service method

`DocumentsServiceImpl.UploadDocument` (client streaming) and `DownloadDocument` (server streaming) are the only service methods that do work of their own beyond resolving the caller, which departs from "a service maps, dispatches and maps back". The reason is in the data: an upload cannot be a command until its content has been read, because the command carries the size and the hash of that content.

- **The service only adapts the stream.** It reads the header message, then hands everything after it, as a sequence of chunks, to `DocumentIngest`. It owns no rule.
- **`DocumentIngest` is the one way a file becomes a document.** The upload RPC, the development seed and a later folder watcher all call it. It writes the content to the blob store, refuses an empty or oversized file as a `ValidationException` (field `content`, codes `required` and `too_large`), and then dispatches the ordinary `CreateDocumentCommand`, from which point validators and the start-stream handler apply as always.
- **Limits are enforced while reading.** The blob store counts bytes and stops at `FileStorage:MaxFileSizeBytes` (100 MB by default), so an oversized upload is never held whole and never stored.
- **A download is a find-by-id and a stream.** It checks the document exists, is the caller's (another user's is not found) and is not deleted, then copies the blob in `FileLimits.ChunkSize` chunks. A document whose content is missing is a fault: `INTERNAL`, logged at Error with the document id.
- **Errors need nothing special.** A failure thrown while the stream is read or written takes the same path to a status as a unary call (`api-error-mapping`).

A new streaming method follows this shape; anything that is not a stream stays a plain map, dispatch and map back.


## Event sourcing

The store is Fisher on SQLite. Every aggregate has an Inline snapshot, registered in one list (`StoreOptionsExtensions.AddAggregateSnapshots`) that the API and the tests share.

- **Snapshots are written in the transaction that appends the event**, so a read right after a write is current. A service may dispatch a command and then a find-by-id query and return the result.
- **Record the facts that decided something.** A value that a rule depended on goes into the event and is not derived again while folding. `UserCreated` carries `NormalizedEmail`, because deriving it during replay would re-key users if the normalization ever changed.
- **One aggregate owns one fact.** Which archive a document is in is recorded on the document (`DocumentFiled`, `DocumentUnfiled`), so "at most one archive" holds by construction. An archive's contents are a query over documents.
- **A list that decides nothing is a query, not an aggregate.** The inbox is "the owner's documents with no archive". It has no stream.
- **File content is not an event.** Fisher stores every snapshot and event body as JSON text, so bytes would be base64 inside it, loaded with every inbox query and kept forever. The bytes live in the blob store, a directory of files named by their SHA-256 (`Shared/Blobs`); the event (`DocumentCreated`) records only the facts about them: file name, content type, size and hash. A document has exactly one file and no event changes it.
- **Content first, then the event.** An upload writes the blob (into `tmp/`, renamed to its hash when complete) and only then appends the event. A crash in between leaves a blob nothing refers to, which is harmless; the other order would leave a document with no file. Nothing deletes a blob yet: another document may share it, so removal is a separate, later change.
- **Extracted text is not an event either.** The text of a file is kept beside its blob as `<blobs>/ab/<sha256>.txt` (`TextStore`, UTF-8, pages separated by a form feed, an unread page kept as an empty one), and `TextExtractionCompleted` records only the outcome and counts. The text is written before the event, the same order as content, so a crash in between leaves a text with no outcome and the extraction runs again over it. A text is removed with its blob, by the blob collector that does not exist yet.
- **Facts about content belong to the content.** What was found in a file is the same for every document with that file, so it is an aggregate keyed by the content's hash (`TextExtraction.Sha256` is its `[NaturalKey]`), not an event on a document. An event on the document would also move the version its writers hold, and the first rename after every upload would fail as stale.
- **Uniqueness is claimed by the store.** A unique value is a `[NaturalKey]` on the aggregate (`User.NormalizedEmail`); the store refuses the second stream inside the append, so there is no check-then-write race.
- **Concurrency is the expected version.** The client echoes the `version` it read; a stale one fails the append and reaches the client as `ABORTED`.
- **Cascades go through the outbox, one stream per message.** Deleting an archive appends `ArchiveDeleted` and returns one `ReturnDocumentToInbox` per member document. Wolverine stores those messages in the transaction of the event and delivers them afterwards, so the documents return shortly after the call has answered, not as part of it. A cascade message is idempotent, takes no expected version, guards on the state it expects (`FromArchiveId`), and retries on a concurrency conflict.
- **A write that must lose a race moves the other stream's version.** Filing a document also appends `ArchiveMembershipChanged` to the archive, so a deletion and a filing that race fail on the ordinary version check, whichever commits second. On SQLite there is no exclusive fetch to rely on.
- **Never touch every member stream in one transaction.** That is what the cascade is for.

Transactions are applied by Wolverine (`AutoApplyTransactions`); a handler calls `SaveChangesAsync` itself only when it needs the result before returning.

## Background work

Work that follows a request and is not part of its answer is a message on a local queue that is durable and sequential. Text extraction (`Features/TextExtraction`) is the example: the upload answers, and `ExtractText(Sha256)` reads the file's text afterwards.

- **The queue is configured where the API is composed.** `options.LocalQueueFor<ExtractText>().UseDurableInbox().Sequential()` in `AddOfficeRkyvApi`, so the server, the desktop and the tests share it. Durable: the message is stored in the database file and handled after a restart if the app stopped first (Wolverine recovers it a few seconds after start). Sequential: one at a time, because OCR takes seconds a page and all of a core, and it makes the handler the only writer of its aggregate.
- **The message names the content, not a document, and holds an id or a hash only.** That is the key of what the handler writes, and it keeps file names and titles out of Wolverine's spans.
- **The message is stored in the transaction of the event that causes it.** A handler that saves the session itself (`CreateDocumentHandler`, which returns the snapshot) does this: it is marked `[NonTransactional]`, takes `IFisherOutbox`, calls `outbox.Enroll(session)`, publishes on the outbox, saves, and then flushes the outbox (`((MessageContext)outbox).FlushOutgoingMessagesAsync()`, since the interface does not expose it). Without the flush the message is stored but only delivered after the next start. The alternatives fail: a publish on the injected `IMessageBus` together with the handler's own save is stored twice by Wolverine's transaction and throws, and a message cascaded from the return value is stored after the event has committed. A decider handler, which does not save, returns the message in `OutgoingMessages`, as the archive deletion does.
- **A handler of such a message is idempotent.** It looks for its result first and does nothing when it exists, so a message delivered twice, or a second upload of the same file, is harmless. A subject that cannot be found (content missing from the blob store) is logged by the component that looked and the message is consumed; the handler does not throw.
- **Stopping the app does not lose the message.** Wolverine lets a running handler finish and cancels it only when the shutdown timeout is over, so a handler with long work also watches `IHostApplicationLifetime.ApplicationStopping`. A cancellation that Wolverine did not issue itself would move the message to the error queue, so the handler is marked `[ScheduleRetry(typeof(OperationCanceledException), 5)]`, which keeps it stored for the next start.
- **No parallel consumers.** `Sequential()` is one line to change when a server host wants more; the natural key on the result is what would then refuse a duplicate.

## Running an external program

Reading a scan runs `pdftoppm` and `tesseract` (`Shared/Extraction`). Any program the API runs follows the same rules, through `IProgramRunner`:

- **A file name and an argument list, no shell.** An argument is never read as shell syntax. The paths passed are built by the app from a content hash, never from a name a user chose.
- **A process of its own.** A program that crashes is an exit code. This matters on the desktop, where the API shares its process with the window; a native OCR library would take the app down with it.
- **A time limit, and the whole process tree is killed** when it passes (`TextExtraction:TimeLimit`, 120 seconds) and when the host stops. Cancelling throws `OperationCanceledException`; the program's own failures never throw.
- **The result is a value:** `Exited` with the exit code, `NotStarted` (not installed or not allowed to run) or `TimedOut`, with what the program wrote to standard output and error.
- **What a program wrote is never logged.** For a scan it is the document's text. A failure is logged with the content hash, the page number and the exit code (Warning).
- **A missing program is not an error.** `OcrProgramsStartCheck` runs each program once at start and logs one Information line (versions and languages) or one Warning (what is missing; scanned pages will not be read), and never stops the host. A page that needs OCR and cannot get it is counted as unread in the outcome, and the pages with a text layer are still kept. `NotStarted` is not logged per page, since the start check has said so.
- **Where the programs are and which languages are read** is the `TextExtraction` configuration section: `Languages` (`deu+eng`), `TesseractPath` and `PdftoppmPath` (`tesseract` and `pdftoppm`, found on `PATH`, or paths so that a packaged app can point at the programs it ships) and `TimeLimit`.
- **The programs sit behind two interfaces,** `IPageRenderer` (a page of a PDF to an image) and `IImageReader` (an image to text), so that the rules of `TextExtractor` (which pages go to OCR, what is counted) are tested with fakes; see [testing.md](testing.md#pipeline-tests).

## Search

A user finds their documents by the words in a title or a file name (`SearchDocuments`, `Features/Documents/Handlers/SearchDocumentsHandler.cs`).

- **The index is declared on the snapshot that holds the fields.** `StoreOptionsExtensions.AddSearchIndexes` puts a full-text index on `Document.Title` and `Document.FileName`, and `AddOfficeRkyvApi` calls it next to `AddAggregateSnapshots`, so the server, the desktop and the pipeline tests share it. Fisher keeps the index in the transaction that writes the snapshot, so a document is found as soon as the call that created or renamed it has answered. When the index is first created, Fisher fills it from the documents that already exist; no start check or rebuild is needed.
- **The tokenizer is `Trigram`**, because the text is German: `rechnung` has to find `Stromrechnung`, and only a trigram index matches inside a word. The cost is that it does not stem (`Rechnungen` does not find `Rechnung`) and does not fold accents (`Marz` does not find `März`); case is ignored.
- **A search is a query with a validator.** `SearchDocumentsValidator` refuses a query over 200 characters (`too_long`) and one with no word the index can match (`too_short`, also for an empty query). The word rule, `SearchTerms` in `OfficeRkyv.Contracts`, is shared with the UI so the page can say "type more" with exactly the server's rule. Words under 3 characters are ignored, since a trigram index cannot match them.
- **The handler runs one raw FTS5 query through `IAdvancedSql`.** Each word is a quoted string, so none can be read as search syntax, and the strings are ANDed. Fisher's LINQ operators accept several `NgramSearch` calls but `OrderByRelevance` refuses a query with more than one, and the results are ordered by `bm25()`, then by title. The query depends on Fisher's storage names (the document table, its `fi_fts_` table, the camelCase JSON members and the integer `status`), which Fisher does not promise; they are constants in the handler, and the pipeline tests of every search scenario fail if they change.
- **Deleted documents are filtered by the handler**, as in every list: deletion is our own `EntityStatus`, not Fisher's soft delete, and the filter has to be in the SQL, before the limit.
- **Results are limited, not paged.** The handler takes 51 and returns 50 with `has_more`, so the client can say that more matched. Paging is in `TODOS.md`.

## Validation

Input rules are FluentValidation validators next to the handlers (`ArchiveValidators.cs`). Wolverine's middleware finds them and runs them before the handler.

- A validator is `public` and has no constructor dependencies, or discovery does not find it.
- It reports every violated rule, not the first.
- Each rule has an error code from `ViolationCodes`, set with `WithErrorCode`. `TextRules.RequiredAndAtMost` covers a required, length-limited text field.
- Rules see the value as it will be stored (trimmed), and pin the property name back with `OverridePropertyName`, since the name would otherwise come from the `Trim()` call.
- Maximum lengths are constants on the validator.

Validation is about the shape of the input. Whether the resource exists or its state allows the operation is the handler's job.

What cannot be a validator is refused where it is found: the content of a file is known only while it is read, so `DocumentIngest` throws the same `ValidationException` with the same `ViolationCodes` before any command exists.

## Failures

One place turns failures into gRPC statuses for every service: `UseApiErrorMapping` in `Shared/Errors`, called from `AddOfficeRkyvApi`.

| Thrown | Status | Detail for the client |
|---|---|---|
| `FluentValidation.ValidationException` | `INVALID_ARGUMENT` | `BadRequest`: one field violation per rule, `field` in snake_case as in the `.proto`, `reason` a `ViolationCodes` value |
| `InvariantViolationException` | `FAILED_PRECONDITION` | `ErrorInfo`: `reason` an `InvariantCodes` value, `domain` `ErrorDomain.Value` |
| `DuplicateNaturalKeyException` | `ALREADY_EXISTS` | none |
| `EventStreamUnexpectedMaxEventIdException` | `ABORTED` | none |
| `NotFoundException` | `NOT_FOUND` | none |
| `UnauthenticatedException` | `UNAUTHENTICATED` | none; the same text whatever the reason (no token, a token that does not verify or has expired, a user that is gone) |
| `PermissionDeniedException` | `PERMISSION_DENIED` | none |
| The client cancelled | `CANCELLED` | none, and not logged |
| Anything else | `INTERNAL` | none; logged at Error with the exception |

- **Status messages are our own fixed text.** An exception's message can hold SQL or paths, so it never reaches a client.
- **Codes are stable wire values.** They live in `OfficeRkyv.Contracts` (`ViolationCodes`, `InvariantCodes`) so API and UI share them. Never reword one; clients key their text off it.
- **Clients explain a refusal from the status code and the reason**, never from the message. The UI side is in [ui.md](ui.md).

The required behaviour is the `api-error-mapping` spec in `openspec/specs`.

## Adding an RPC

1. Add the RPC and its messages to the feature's `.proto` in `OfficeRkyv.Contracts`. Ids are strings; a mutation that leaves the resource in place returns its DTO with the new `version`.
2. Add the command or query record, and its mapping in the feature's mapper.
3. Add the handler in `Handlers/`, in the shape that fits, with a validator if the command has input rules and the existence check if it names an existing aggregate.
4. Add new events to the events file and an `Apply` for each on the aggregate.
5. Add the service method: resolve the caller first (`CallerAccessor.RequireAsync`), then map, dispatch, map back. A command on an existing aggregate gets the caller as `CallerId`, and the existence check refuses what is not theirs.
6. For a new refusal, add the code to `InvariantCodes` and its text on the UI side.
7. For a new aggregate, add its snapshot to `AddAggregateSnapshots` and its service to `MapOfficeRkyvApi`.
8. For a call that streams a file, follow [the streaming shape](#a-streaming-service-method): the service adapts the stream, and the rules live in the component it hands the stream to.
9. Cover it with pipeline tests; see [testing.md](testing.md).

## Decided in

- [landing-page-user-management](../openspec/changes/archive/2026-09-24-landing-page-user-management/design.md) (D1, D3): facts recorded in events, and existence checked before the version.
- [structured-error-codes](../openspec/changes/archive/2026-09-28-structured-error-codes/design.md) (D1 to D5): the error mapping, the validation adapter, validators through Wolverine's middleware, codes on invariant violations, codes in `Contracts`.
- [add-archives-and-inbox](../openspec/changes/archive/2026-09-29-add-archives-and-inbox/design.md) (D1 to D7): membership on the document, the inbox as a query, the handler styles, the cascade through the outbox, the filing race, the contracts.
- [add-linux-desktop-app](../openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md) (D3): the API composition as extension methods.
- [add-document-file-upload](../openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D1 to D10): file facts on the creation event, the blob store, content first, the streaming RPCs, the ingest component, limits while reading, the download.
- [add-document-title-search](../openspec/changes/archive/2026-10-17-add-document-title-search/design.md) (D1 to D4): the trigram index on the snapshot, the word rule, the raw FTS5 query that can rank several words, existing documents indexed by the migration.
- [add-sign-in](../openspec/changes/archive/2026-10-17-add-sign-in/design.md) (D1 to D4): the caller token, the caller resolved in the services, commands carrying the acting user, the open user service.
- [add-document-text-extraction](../openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (D1 to D7): background work as a message on a durable sequential queue, the kind of file read from its bytes, the per-page rule, an aggregate keyed by content hash, the text beside its blob, external programs run as processes, unread pages counted and missing programs not an error.
