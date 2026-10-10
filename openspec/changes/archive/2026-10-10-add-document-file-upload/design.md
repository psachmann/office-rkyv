# Design

## Context

See `proposal.md` for the motivation and the scope.

A document today is `OwnerId`, `Title` and `ArchiveId` on an event stream with an Inline snapshot. Fisher stores every snapshot and every event body as System.Text.Json text in a `data TEXT` column, so the event store is the wrong place for file content: bytes would be base64 inside JSON, loaded with every inbox query, never streamed, and kept forever.

Every RPC so far is unary and has one shape: the service maps a request to a command, Wolverine validates and handles it, the service maps the answer back. gRPC limits a message to 4 MB by default, so a file cannot travel as one message.

Three processes host the API through `AddOfficeRkyvApi(connectionString, configureStore)`: the API server, the desktop app and the pipeline tests' `PipelineHost`. Each decides where its database is; nothing else about storage exists yet. The development database is a new temporary file on every AppHost run, and the development seed runs once per database.

The UI reaches the API only through the generated gRPC clients, in both hosts, and what differs per host reaches the UI through an interface the host implements (`IUserSettingsService`).

## Goals / Non-Goals

**Goals:**

- One ingest path that the upload RPC, the development seed and a later folder watcher all call.
- File content that is written and read as a stream, never held whole in memory.
- A storage layout in which a failed, aborted or refused upload leaves nothing that looks like a stored file.
- Refusals of an upload reach the UI with a stable code, like every other refusal.

**Non-Goals:**

- Removing file content from disk. Nothing deletes a blob in this change.
- Detecting or refusing duplicate uploads.
- Inspecting content: no type sniffing, no virus scan, no text extraction.
- Resuming an interrupted upload.
- Migrating documents created before this change (D13).

## Decisions

### D1. The file is a set of facts on `DocumentCreated`

`DocumentCreated` gains the file's facts: `FileName`, `ContentType`, `Size` and `Sha256`. The aggregate carries them and no later event changes them, so "one immutable file" holds by construction, the same way "at most one archive" does. `DocumentDto` exposes all four.

The hash is recorded even though nothing decides on it yet. It is the key of the stored content, and it is the fact a later duplicate rule or the folder watcher will need; deriving it later would mean reading every file again.

*Alternative:* a separate `DocumentFileAttached` event after creation. Rejected: it creates a state, a document without a file, that the model says cannot exist.

*Verified at:* pipeline (the facts returned after an upload and after a reload of the stream).

### D2. Content lives in a content-addressed blob folder

A blob store under `Api/Shared` owns one directory:

```
blobs/
  tmp/<guid>          an upload in progress
  ab/abcdef...        finished content, named by its SHA-256, in a folder of its first two hex digits
```

Writing streams the incoming bytes into `tmp/<guid>` while hashing and counting them. When the stream ends, the file is renamed to its hash. `tmp` is inside the blob folder so that the rename is on one filesystem and atomic. If the target already exists, the temporary file is deleted instead: the same content is stored once. On any failure the temporary file is deleted. Whatever is still in `tmp` when the API starts is left over from a crash and is deleted then.

Reading opens the file by hash as a stream.

*Alternatives:*

- Bytes in a Fisher document or event. Rejected for the reasons in Context.
- A `BLOB` table of our own in the same SQLite file. It would keep "backup is one file" and could make content and event atomic. Rejected: scans are in the size range where SQLite is slower than the filesystem, reading a blob row is not a natural stream, and the database would grow with every scan. The blob store is an interface, so this stays possible.

*Verified at:* unit (the store against a temporary directory: content round trip, same content stored once, nothing left in `tmp` after a failed write, `tmp` emptied on start).

### D3. Each host says where the blobs are, and they live as long as the database

`AddOfficeRkyvApi` takes the blob directory beside the connection string.

| Host | Blob directory |
|---|---|
| Desktop | `blobs/` in the data directory, from `DesktopPaths` |
| API server | `<database file>.blobs` beside the database file; a configuration value overrides it |
| `PipelineHost` | a folder beside the test's temporary database, deleted with it |

The API server derives the folder from the database file because the two must have the same lifetime: the seed runs once per database, so a new development database needs new blobs, and a fixed folder would collect orphans on every `just dev`.

*Verified at:* unit (`DesktopPaths`, and the derivation from a connection string); functional (upload and download through the AppHost).

### D4. Content first, then the event; nothing is ever deleted

An upload writes the blob and only then appends `DocumentCreated`. A crash in between leaves a blob no document refers to, which is harmless. The reverse order would leave a document whose file is missing, which is not.

Deleting a document stays what it is: one event. The blob is left, because another document may refer to the same content and checking that races with a concurrent upload of the same file. A collector that removes unreferenced blobs is its own later change; it is the only code that would destroy user data, and it deserves its own design.

Consequence: a refused upload whose content was already complete (an unknown or deleted owner, see D6) leaves an orphan blob. Accepted until the collector exists.

### D5. Two streaming RPCs replace `CreateDocument`

```
rpc UploadDocument (stream UploadDocumentRequest) returns (UploadDocumentResponse);
rpc DownloadDocument (DownloadDocumentRequest) returns (stream DownloadDocumentResponse);
```

- An upload's first message is a header: `owner_id`, `file_name`, `content_type` and an optional `title`. Every later message is a chunk of bytes. The request is a `oneof` of the two. The response is the created `DocumentDto`.
- A download is requested by `document_id` and answers with chunks only. The name, type and size are already on the `DocumentDto` the client holds.
- Chunks are 64 KB, far below the 4 MB message limit, so no limit is raised.
- `CreateDocument` and its messages are removed.

*Alternative:* a plain HTTP endpoint on the API. Rejected: a second protocol with its own error mapping, and the UI would no longer reach the API only through the generated clients.

*Verified at:* pipeline (both RPCs through `Channel` with the generated client).

### D6. A streaming service method delegates to one ingest component

An upload cannot be mapped to a command before its content has been read. The service method reads the header, then hands the rest of the stream to an ingest component in the `Documents` feature, which:

1. checks the header's shape that can be checked without the content (the owner id parses, the file name is present),
2. writes the content to the blob store (D2), enforcing the limits (D7),
3. dispatches `CreateDocumentCommand` with the owner, the title (D8) and the four file facts on `IMessageBus`.

From step 3 on it is the ordinary path: validators, then the start-stream handler, which still checks that the owner exists and is active. The seeder calls the same component with a file stream (D12), and the folder watcher will later.

**This departs from `docs/api.md`**, which says a service maps, dispatches and maps back, with no logic. The departure is confined to the two streaming methods and to this one component, and the doc gains it as a named shape.

The owner is checked after the content is written, so an unknown owner costs one stored blob. Checking it first would need a second read of the user that can still be stale by the time the event is appended; the handler's check stays the authority.

*Verified at:* pipeline (an upload for an unknown owner is not found and creates no document; for a deleted owner it is refused with `user_deleted`).

### D7. Limits are enforced while reading, and refused like validation failures

- **Too large:** the blob store counts bytes and stops at the limit. The limit is 100 MB, an option of the API with that default. The refusal is `INVALID_ARGUMENT` with a field violation on `content` and the new code `too_large` in `ViolationCodes`.
- **Empty:** a stream with no content bytes is refused with `required` on `content`.
- **File name:** only its last path segment is kept. It is required and at most 255 characters, as ordinary validator rules on the command.
- **Content type:** recorded as the client declared it, at most 255 characters; an absent one is recorded as `application/octet-stream`. It only labels the download.

The first two are thrown by the ingest component as `FluentValidation.ValidationException`, so they take the existing path to `BadRequest` details. They cannot be validators on the command, because the command does not exist until the content has been read.

Size is `too_large` and not the existing `too_long`, since clients key text off the code and "too long" is worded for text fields.

*Verified at:* pipeline (a file one byte over the limit, with a small limit configured for the test, is refused with `too_large` and leaves no document and nothing in `tmp`; an empty file is refused with `required`); unit (file-name rules).

### D8. The server derives the title

When the header carries no title, the title is the file name without its last extension, trimmed and cut to the 200-character limit. If that leaves nothing (a file named `.pdf`), the whole file name is used. A title the client does send goes through the existing title rules unchanged.

The rule is on the server so that the UI, the seed and the folder watcher produce the same titles.

*Verified at:* unit (the derivation, including a name with several dots, a name longer than 200 characters and a name that is only an extension).

### D9. Duplicates are allowed

Uploading the same content twice for the same owner creates two documents that refer to one blob. A duplicate costs one event and no disk space, and the user can delete it. The recorded hash keeps a later rule possible.

*Verified at:* pipeline (two uploads of the same content give two documents with the same hash).

### D10. A download streams the blob of an existing document

The service dispatches the existing find-by-id query, opens the blob by the document's hash and writes it in chunks. An unknown document is `NOT_FOUND`; a deleted one is refused with `document_deleted`, as for a rename. A document whose blob is missing is an unexpected failure: `INTERNAL`, logged at Error with the document's id.

*Verified at:* pipeline (the downloaded bytes equal the uploaded ones, for a file larger than one chunk; unknown and deleted documents).

### D11. The UI uploads files one after another, outside the mutation helper

- The upload dialog takes several files and lists each with its state: waiting, uploading with progress, done, or refused with its localized reason.
- Files are uploaded one at a time. The store has one writer, and on the web host each file already travels browser to Web process over the SignalR circuit before it goes to the API.
- `DocumentsFacade` gets an upload method that takes the owner, the file's name, type and stream, and a progress callback; it writes the header and the chunks to the streaming client and invalidates the inbox query when a document was created. It is a method and not a DotNetQuery mutation, because a mutation has no progress to report. This adds to the facade convention in `docs/ui.md`; it does not replace it.
- The dialog refuses an over-limit file before sending it, for a quick answer. The server stays the authority.
- The refusal for `too_large` and for an empty file gets its text in the English and German resources.

*Verified at:* UI logic (the facade sends a header then chunks, reports progress, invalidates the inbox, and maps `too_large` to its message; a refused file does not stop the next); functional (a journey uploading two files and seeing both in the inbox).

### D12. Saving a downloaded file is the host's job

`OfficeRkyv.UI` defines an interface that takes a file name, a content type and a stream. The page gets the stream from the facade's download method and hands it over.

- **Web:** the Web process passes the stream to the browser through Blazor's stream interop, and a small script saves it as a normal download.
- **Desktop:** a GTK save dialog, prefilled with the file name, and the stream is written to the chosen path. If the dialog does not work on the experimental backend, the fallback is to save into the user's Downloads directory under a name that does not clash and show a notification with the path.

**First by-hand run (desktop):** choosing Download aborted the whole app with `GLib-GIO-ERROR: No GSettings schemas are installed on the system`, exit code 134. GTK's own file dialog reads `org.gtk.gtk4.Settings.FileChooser`, nothing in the `desktop` dev shell put a schema directory on `XDG_DATA_DIRS`, and GLib aborts instead of throwing, so the Downloads fallback above could not catch it. Two changes answer it: `flake.nix` gives the `desktop` shell GTK's and `gsettings-desktop-schemas`' schemas (checked: `gsettings list-schemas` in that shell lists the FileChooser schema), and `DesktopFileSaver` looks the schema up before opening the dialog and takes the fallback with a Warning when it is missing. The save dialog itself still has to be checked on a real desktop (task 8.2).

**Second by-hand run (desktop, after those changes):** Download opened the system save dialog with the file name proposed, the chosen name and path were used, and a green notification confirmed the save once the dialog had closed. Two saved files were compared with their blobs by SHA-256 and are byte-identical (a 512000-byte file and a 102400-byte one). So the dialog works on this desktop (COSMIC) and the Downloads fallback is not needed here; it stays as the answer for desktops where the schema is missing. Cancelling a save writes nothing and shows no error, and a save after closing and restarting the app works too (both confirmed by hand).

*Alternative for the web:* an HTTP endpoint on the Web host that streams the file. It suits very large files better, but adds a route outside the gRPC path. Kept as the fallback if browser memory becomes a problem at 100 MB.

*Verified at:* functional (Playwright receives the download and its bytes equal the uploaded file); unit (the desktop's non-clashing file name, if the fallback is used); by hand for the desktop (download a seeded-size PDF, choose a path, compare the saved file's checksum with the original; the app's stdout shows no error).

### D13. The seed uploads sample files; old documents are not migrated

The development seed creates its three documents through the ingest component (D6) from `sample_1.pdf` to `sample_3.pdf` under `src/OfficeRkyv.Api/Data`, with its existing titles sent explicitly. The files ship with the API's build output. They are the only files in that folder: the two further samples that were committed with it were unused and are removed.

A `DocumentCreated` event written before this change has no file facts. No migration is written: development databases are throwaway, and a desktop database from before this change is deleted by hand. This is acceptable only because the desktop app has no users but its developer.

*Verified at:* pipeline (the seeded documents have the sample files' sizes and can be downloaded).

### D14. Two spikes come first

Both go through Wolverine's gRPC integration, which has only been used for unary calls here:

1. **Server:** whether the exception interceptor behind `UseApiErrorMapping` covers a client-streaming and a server-streaming method, so that a `ValidationException` thrown while reading the stream reaches the client as `INVALID_ARGUMENT` with its details.
2. **Client:** whether the clients registered with `AddWolverineGrpcClient` wrap streaming calls the way they wrap unary ones, so the UI's error mapping sees the same typed exception.

If either does not, the two streaming methods get their own small mapping that calls the same `ErrorStatusProvider`, and the facade unwraps the `RpcException` itself. The decisions above do not change.

A third check belongs to D12: the GTK save dialog on a real desktop.

**Results (spikes 1.1 to 1.4, run before any feature code):**

1. **Server: covered.** A client-streaming method that throws `ValidationException` reaches the client as `INVALID_ARGUMENT` with the `BadRequest` field violation (`content` / `too_large`); a server-streaming method that throws `NotFoundException` reaches it as `NOT_FOUND`. Both go through the same interceptor and `ErrorStatusProvider` as unary calls.
2. **Client: covered.** A client from `AddWolverineGrpcClient` wraps a refused streaming call as it wraps a unary one: the refused upload surfaces as `ArgumentException` and the refused download as `KeyNotFoundException`, each with the `RpcException` (status and details) as `InnerException`. `WorkspaceErrors.From` already unwraps that.
3. **Fallback: none needed.** The streaming methods use the existing error mapping, and the facade needs no special unwrapping.
4. **AppHost database:** `AddSqlite` with no path creates a randomly named file (`xxxxxxxx.xxx.db`) in the temp directory, and nothing removes it. The blob folder is therefore named after the database file (`<database file>.blobs`) and leaves leftovers beside it exactly as the database files already do.

## Telemetry and logs

- **Traces and metrics:** no new source or meter. The two RPCs appear as ASP.NET Core server spans, an upload's command as a Wolverine span, the append as Fisher spans. Writing the blob has no span of its own, following "subscribe, don't instrument". `ApiTelemetryTests` is extended with an upload and checks that no span carries the file name or the title.
- **Logs:** one Warning at start when leftover temporary files were removed, with their number. One Error when a download finds no blob for a document, with the document id. Refused uploads are not logged. All through `[LoggerMessage]` methods.
- **Never in a span or a log:** file content, the file name and the title. A document is named by its id, content by its hash.

## Guideline docs to update

- `docs/api.md`: the streaming service shape and the ingest component (D6), content beside the event store and written first (D2, D4), limits enforced while reading (D7), the steps for adding an RPC.
- `docs/architecture.md`: the blob folder per host (D3), the request path of an upload and a download, the changed signature of `AddOfficeRkyvApi`, the seed's files.
- `docs/ui.md`: the file-saving interface (D12), the facade's upload and download methods beside queries and mutations (D11).
- `docs/desktop.md`: how a file is saved, and whichever workaround D12 ends with.
- `docs/testing.md`: `PipelineHost`'s blob folder, driving a streaming call, asserting a download in a functional test.
- `docs/observability.md`: the file name and the title added to what must never appear in a span or a log.
- `openspec/config.yaml` `context`: the request path, where it repeats `docs/architecture.md`.

## Risks / Trade-offs

- [Wolverine's gRPC integration does not cover streaming calls] → D14 runs first; the fallback is local mapping code for two methods.
- [The GTK save dialog fails on the experimental backend] → the Downloads-directory fallback in D12, decided by a check on a real desktop.
- [Blobs accumulate: deleted documents, refused uploads with complete content, and crashes all leave content behind] → accepted for this change; the collector is the next change. Until then disk use only grows.
- [A 100 MB file passes through the Web process and the SignalR circuit, and a download is held by the browser before it is saved] → uploads are sequential and streamed in 64 KB chunks; if memory or time is a problem, the web download moves to an HTTP endpoint (D12).
- [The declared content type can be wrong or hostile] → it is only ever sent back as a download label; nothing renders content in the app.
- [The file name is user input used on a filesystem] → it is never used as a path on the server, where content is named by hash only; the desktop save uses the path the user chose, or a sanitized name in the fallback.
- [The blob folder and the database can be backed up at different moments] → a database newer than its blobs has documents without content; documented in `README.md` beside where the desktop keeps its data.

## Migration Plan

1. The spikes (D14).
2. Contract, blob store and API in one step, since removing `CreateDocument` breaks every caller at compile time: the UI, the seed and the tests move in the same change.
3. Delete any desktop database from before the change (D13).

Rollback is reverting the change. Documents created with files would then be read by code that ignores the extra fields; their content stays in the blob folder untouched.

