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
- **Files go through `TestDocuments`.** `UploadAsync` and `DownloadAsync` drive the streaming RPCs through the generated client in chunks of a size you choose, and `Bytes(n)` makes content that is not one repeated byte. A test that is about something else than the upload (filing, cascades) makes documents with `TestDocuments.Command`, which has file facts for content nobody reads; one that downloads must upload.
- **Data from before this host can be started on.** `StartAsync(databasePath: ...)` starts the host on an existing database file, with the blob directory beside it, and deletes both when it is disposed. A test that needs data written by an earlier version writes it first with a `DocumentStore.For(...)` configured as that version was (`DocumentSearchTests`: snapshots and no search index), and then starts the host on the file.
- **A streaming refusal is read like a unary one:** wrap the whole call in `GrpcFailure.Of`, because a server that refuses mid-upload makes the next write fail and the call's status is what explains it.
- Use `InvokeAndSettleAsync` when the command cascades (an archive deletion returning its documents), so the test asserts on the settled state and not on a race.
- Telemetry is verified here too, with the in-memory exporter against the real wiring; see [observability.md](observability.md).

## UI-logic tests

- Build a small service provider with `AddDotNetQuery()`, the facades, and fake gRPC clients that derive from the generated client classes.
- A query only refetches while something is subscribed, as a rendered page is. A test that counts fetches holds a subscription open.

## Functional tests

- Tech stack: TUnit with Playwright and TUnit.Aspire for env setup
- We are using TUnit assertions by default
- We are using data-testid to locate user input elements and other ui elements
- We are using page objects to encapsulate locators and view logic into reusable units

In practice the page objects (`WorkspacePage`, `SettingsPage`) find an element the way a person does, by role, label or visible text, and use `data-testid` only for parts with no name of their own, such as a navigation entry or a document row. Follow that; [ui.md](ui.md#test-ids) says which elements get a test id.

- **One AppHost per test session.** Every class takes `AppHostFixture` (an `AspireFixture<Projects.OfficeRkyv_AppHost>`) with `Shared = SharedType.PerTestSession`, so all functional tests share one app and one SQLite database. The fixture starts the API with `Seed__DemoData=false`, so the development seed ([architecture.md](architecture.md#one-api-composition)) does not put a demo user into that database; `DevelopmentSeedTests` guards it.
- **Each test creates its own user** and looks at nobody else's data (`WorkspacePage.OpenNewUsersWorkspaceAsync`), which is what lets them run in parallel. `LandingPageTests` is the exception: it needs the empty database first, so its tests are ordered with `DependsOn`.
- **Browsers are limited** with `[ParallelLimiter<BrowserParallelLimit>]` on every class.
- **Assert with Playwright's `Expect`**, which waits, for anything on the page; TUnit assertions for everything else.
- **Files are set on the upload dialog's input** (`WorkspacePage.UploadAsync`, from byte arrays), and a download is the Playwright download event (`WorkspacePage.DownloadAsync`): compare its suggested name and the bytes it saved with the originals.
- **A document row is found by its exact title cell** (`DocumentRow`), because the row also shows the file name, which a rename does not change.
- **A search is debounced, so wait for something only the results show.** The page runs the search once typing has rested and puts it in the address. A document that the plain inbox shows too proves nothing; wait for one that is in an archive, or for the address (`?q=`), before acting on the results.

## By hand: the desktop

Playwright cannot drive WebKitGTK, so nothing automated opens the desktop window. A task that changes desktop behaviour says what to do in the app and what to observe in its stdout. `just desktop` runs it in Development, which logs the host's lifecycle and the page's console to the terminal.

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
