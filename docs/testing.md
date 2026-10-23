# Testing

The test levels this project has, what each one runs, and which to pick. Read it before adding or changing a test, and before writing a plan that says how something will be verified: the level names below are the vocabulary the OpenSpec rules use.

## The five levels

| Level | Project | What runs | Examples |
|---|---|---|---|
| Unit | `tests/OfficeRkyv.UnitTests` | Pure logic. No host, no store, no network. | `EmailAddressTests`, `CreateUserCommandValidatorTests`, `DesktopPathsTests`, `TranslationCompletenessTests` |
| Pipeline | `tests/OfficeRkyv.UnitTests` | The real Wolverine pipeline over a real Fisher store in a temporary SQLite file, with the gRPC services served over the wire. | `ArchivePipelineTests`, `UserPipelineTests`, `ApiTelemetryTests` |
| UI logic | `tests/OfficeRkyv.UnitTests` | Facades, error mapping and settings services, with fake gRPC clients and no browser. | `WorkspaceFacadeTests`, `CreateUserErrorsTests`, `WebUserSettingsServiceTests` |
| Functional | `tests/OfficeRkyv.FunctionalTests` | Playwright against the whole app, started through the Aspire AppHost. | `WorkspaceTests`, `LandingPageTests`, `SettingsTests` |
| By hand | none | The desktop app on a real desktop, with its stdout as evidence. | Any change to `src/OfficeRkyv.Desktop` that a unit test cannot reach |

The project named `OfficeRkyv.UnitTests` holds the first three levels. Pipeline tests are integration tests; they live there because they need no browser and no AppHost, and they run in seconds.

## Which level to pick

Pick the lowest level that can show the behaviour.

- **A rule about a value** (normalizing, validating, mapping, parsing): unit.
- **What a command or query does** (events written, refusals, concurrency, what a gRPC client sees on failure, which spans come out): pipeline.
- **What the UI does with an answer** (which queries refetch after a mutation, which message a failure becomes): UI logic.
- **What a person sees and does on a page**, across the web host and the API: functional. Keep these to journeys; rules are cheaper to cover below.
- **What only the desktop host does** (the window, the theme, signals, a second launch): the parts with logic get unit tests (`GtkAppearanceTests`, `DesktopApiTests`); the rest is checked by hand.

## Unit tests

- Tech stack: TUnit and TUnit.Mocks
- We are using TUnit assertions by default
- We are using fixtures where it makes sense and test setup can be shared

The same holds for the pipeline and UI-logic levels, which live in the same project. Test doubles so far are small hand-written fakes next to the test that uses them (`FakeArchivesClient`, `CapturingLogger`); use TUnit.Mocks where a fake would be more code than the test.

## Pipeline tests

- `PipelineHost` (in `tests/OfficeRkyv.UnitTests/PipelineHost.cs`) builds the API with the same `AddOfficeRkyvApi` and `MapOfficeRkyvApi` the server and the desktop use, so a test runs the real validation, decider middleware, concurrency guard and error mapping. Start it in `[Before(Test)]` and dispose it after; each test gets its own database file.
- Drive it through `Bus` for what a handler does, and through `Channel` with a generated gRPC client for what a client sees. `GrpcFailure` reads the status and its details from a failed call.
- **Each host gets its own blob directory**, beside its database file and named after it (`PipelineHost.BlobDirectory`), deleted with it. `StartAsync(maxFileSizeBytes: ...)` sets a small upload limit, so a test can reach "one byte over" without sending 100 MB.
- **Every call is made as a user.** The services refuse a call without a caller token, so a test calls through a channel made for a user: `ChannelFor(userId)` puts a token on every call as the desktop's clients do (call credentials, on a channel that allows them over plain HTTP), `CallOptionsFor(userId)` gives the headers for one call on the anonymous `Channel`, and `TokenFor(userId, key, expires)` makes a token made by the same issuer the hosts use. The host validates with a fixed test key (`PipelineHost.CallerKey`), so `OtherKey` or an expiry beyond the five minutes of clock skew gives a token the API refuses (`CallerTests`). A generated client is made per user (`new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(alice.Id))`), and tests that talk to the bus pass the user explicitly, as the seed and the cascade do. A test that composes its own host for a service calls `AddCallerAuthentication` and `UseAuthentication` as `AddOfficeRkyvApi` and `MapOfficeRkyvApi` do.
- **Files go through `TestDocuments`.** `UploadAsync` and `DownloadAsync` drive the streaming RPCs through the generated client (one made for a user) in chunks of a size you choose, and `Bytes(n)` makes content that is not one repeated byte. A test that is about something else than the upload (filing, cascades) makes documents with `TestDocuments.Command`, which has file facts for content nobody reads; one that downloads must upload.
- **Data from before this host can be started on.** `StartAsync(databasePath: ...)` starts the host on an existing database file, with the blob directory beside it, and deletes both when it is disposed. A test that needs data written by an earlier version writes it first with a `DocumentStore.For(...)` configured as that version was (`DocumentSearchTests`: snapshots and no search index), and then starts the host on the file.
- **Extraction runs in the background, and the OCR programs are faked.** An upload is answered before its text is read, so a test waits first: `PipelineHost.WaitForExtractionAsync(sha256)` returns the outcome once it exists, and `WaitForExtractionsToSettleAsync()` waits until every queued extraction has been handled, for a test that has to know that a second message did nothing. `FindExtractionAsync` and `Texts` (the text store, `ReadPagesAsync`) read the result. `StartAsync(ocr: ...)` installs a `FakeOcr` in place of `pdftoppm` and `tesseract`: it implements both interfaces, answers with prepared text per page (`TextOfPage`), fails on chosen pages (`FailOnPages`) or as a whole (`Unavailable`, the programs not installed), counts its calls (`RenderCalls`, `ReadCalls`, `Reads`), and can hold a read (`HoldReads`) so that a test sees an extraction that is still running. The scenarios derive from `ExtractionPipelineTestBase`, which starts a host with a fake and one user (`Alice`) and uploads (`UploadAsync`).
- **The searchable text is read and awaited through the host.** `FindContentTextAsync(sha256)` and `CountContentTextsAsync()` read the `ContentText` rows; there is no wait for them, since a row is written in the transaction of the extraction's outcome, so `WaitForExtractionAsync` is enough. `WaitForIndexingAsync()` waits for the start-up work of `ContentTextIndexer`, which runs in the background after a host has started. A test of texts extracted before the search existed stops a host with `StopKeepingDataAsync()`, deletes the rows through a `DocumentStore.For(...)` configured with `AddAggregateSnapshots` and `AddSearchIndexes`, and starts another on the file (`ContentTextIndexerPipelineTests`). A PDF's text layer built with PdfPig's standard fonts holds ASCII only: a test that needs an umlaut in a text makes the file an image and prepares the text in `FakeOcr.TextOfPage`.
- **PDFs are built in the test, not checked in.** `TestPdfs.WithPages("text", "")` makes a PDF with PdfPig's document builder, a page with a text layer for each text and a blank page for each empty one (`WithoutText(n)`, `ScanOf(image)` for a picture as the only content); `Truncated`, `Zip`, `Png`, `Jpeg` and `Tiff` make the other kinds of content. A page's text layer needs at least 20 letters or digits to be used, so `TestPdfs.InvoiceText` has 23.
- **A host can be started again on the data of an earlier one.** `StopKeepingDataAsync()` stops a host and keeps its database and blob directory; `StartAsync(databasePath: first.DatabasePath)` starts another on them (`RestartPipelineTests`: an extraction that was running when the first host stopped is finished by the second). Disposing a host still deletes the data. A host waits for a running handler on the way down, so a fake that holds a read must end it when the host stops, as `FakeOcr` does.
- **One test runs the real programs.** `RealProgramsPipelineTests` builds a PDF with a text page, renders it with `pdftoppm`, puts the picture alone into a second PDF, uploads that without a fake, and checks that the extracted text has the words. It calls `Skip.Test` when either program is not on `PATH`, so a machine without them reports it skipped and not failed; in the dev shell it runs, and so it does in the pre-push hook.
- **A program is run through `IProgramRunner`, and its unit tests use a fake:** `FakeProgramRunner` records the calls and answers with a prepared `ProgramResult`, so the argument lists and the mapping of a failed, timed-out or missing program to an unread page (`OcrProgramTests`) and the start check (`OcrProgramsStartCheckTests`) need no process. `ProgramRunnerTests` runs `sh`, `printf`, `cat` and `sleep` for the runner itself.
- **A streaming refusal is read like a unary one:** wrap the whole call in `GrpcFailure.Of`, because a server that refuses mid-upload makes the next write fail and the call's status is what explains it.
- Use `InvokeAndSettleAsync` when the command cascades (an archive deletion returning its documents), so the test asserts on the settled state and not on a race.
- Telemetry is verified here too, with the in-memory exporter against the real wiring; see [observability.md](observability.md).

## UI-logic tests

- Build a small service provider with `AddDotNetQuery()`, the facades, and fake gRPC clients that derive from the generated client classes.
- To see the clients as the web host registers them, register the real client with `CallerTokenCredentials.AddTo` and a fake `AuthenticationStateProvider` per scope (`CallerTokenCredentialsTests`, `WorkspaceErrorsOverTheWireTests`): the scope is the circuit, and whoever the fake says is signed in is who the token is for.
- A query only refetches while something is subscribed, as a rendered page is. A test that counts fetches holds a subscription open.

## Functional tests

- Tech stack: TUnit with Playwright and TUnit.Aspire for env setup
- We are using TUnit assertions by default
- We are using data-testid to locate user input elements and other ui elements
- We are using page objects to encapsulate locators and view logic into reusable units

In practice the page objects (`WorkspacePage`, `SettingsPage`) find an element the way a person does, by role, label or visible text, and use `data-testid` only for parts with no name of their own, such as a navigation entry or a document row. Follow that; [ui.md](ui.md#test-ids) says which elements get a test id.

- **One AppHost per test session.** Every class takes `AppHostFixture` (an `AspireFixture<Projects.OfficeRkyv_AppHost>`) with `Shared = SharedType.PerTestSession`, so all functional tests share one app and one SQLite database. The fixture starts the API with `Seed__DemoData=false`, so the development seed ([architecture.md](architecture.md#one-api-composition)) does not put a demo user into that database; `DevelopmentSeedTests` guards it.
- **Each test creates its own user and signs in as them** (`WorkspacePage.OpenNewUsersWorkspaceAsync` creates the user through the landing page's dialog, which signs in and lands in their workspace). A browser context has its own cookie, so tests stay independent and parallel, and a test that needs a second user opens a second context (`Browser.NewContextAsync`, see `SignInTests`). Other users' data is never looked at.
- **`LandingPageTests` has an AppHost of its own** (`SharedType.PerClass`): it needs the empty database first, which the shared one cannot give while other classes create users, and its tests build on each other's users and are ordered with `DependsOn`. The user cards are found by role, a button named by the display name (`WorkspacePage.UserCard`).
- **`SignInTests`** covers choosing a card, a reload and a new tab, signing out, the pages a signed-out or signed-in visitor is sent to, a forged or reused sign-in address, two browsers as two users, and the old `/users/{id}` address. The sidebar's name and sign-out button are found as `sidebar-user` and by role.
- **Browsers are limited** with `[ParallelLimiter<BrowserParallelLimit>]` on every class.
- **Assert with Playwright's `Expect`**, which waits, for anything on the page; TUnit assertions for everything else.
- **Files are set on the upload dialog's input** (`WorkspacePage.UploadAsync`, from byte arrays), and a download is the Playwright download event (`WorkspacePage.DownloadAsync`): compare its suggested name and the bytes it saved with the originals.
- **A document row is found by its exact title cell** (`DocumentRow`), because the row also shows the file name, which a rename does not change.
- **A search is debounced, so wait for something only the results show.** `SearchTests` drives the header and the search dialog through `SearchDialogPage`. The dialog runs the search once typing has rested, and a document that the inbox shows too proves nothing; wait for one that is in an archive before acting on the results.
- **The page is not interactive until its circuit connects, so a click or a key before that does nothing.** `SearchDialogPage.OpenWithTheControlAsync` and `OpenWithTheShortcutAsync` repeat the action until the dialog is there (and look first whether it already is: a click would hit the dialog's overlay). A negative check, such as the shortcut opening nothing, first opens and closes the dialog once so the shortcut is known to be registered, then waits a second for what must not happen.

## By hand: the desktop

Playwright cannot drive WebKitGTK, so nothing automated opens the desktop window. A task that changes desktop behaviour says what to do in the app and what to observe in its stdout. `just desktop` runs it in Development, which logs the host's lifecycle and the page's console to the terminal. The sign-in's logic is unit-tested (`DesktopSignInTests`, `DesktopApiTests`); that the window shows the landing page, the workspace and the two log lines (`Signed in as user …`, `Signed out`) is observed by hand.

## Running

- `just test` runs both projects (Debug). `just test Release` is what the pre-push hook runs, with warnings as errors.
- `dotnet test --project tests/OfficeRkyv.UnitTests` runs the three fast levels only.
- The functional tests need Playwright's browsers once: `just setup`.

## Known flakiness

`SettingsTests.Settings_IsReachableFromTheSidebar_AtTheBottom_AndMarkedAsTheCurrentPage` fails now and then on an untouched checkout, with a `NullReferenceException` from a bounding box that was read while the sidebar entry was not visible. If it is the only failure in a run, run it again before looking for a cause in your change.

## Decided in

- [landing-page-user-management](../openspec/changes/archive/2026-09-24-landing-page-user-management/design.md) (D5): the shared AppHost and the ordered landing-page tests.
- [add-library-telemetry](../openspec/changes/archive/2026-09-28-add-library-telemetry/design.md) (D5): telemetry verified with an in-memory exporter against the real wiring.
- [add-linux-desktop-app](../openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md) (D8): what is unit-tested in the desktop host, and what is checked by hand.
- [add-sign-in](../openspec/changes/archive/2026-10-17-add-sign-in/design.md) (D13): calling as a user in pipeline tests, signing in in functional tests.
- [add-document-text-extraction](../openspec/changes/archive/2026-10-17-add-document-text-extraction/design.md) (D6, D9): fakes for the OCR programs, and the one test that runs the real ones.
