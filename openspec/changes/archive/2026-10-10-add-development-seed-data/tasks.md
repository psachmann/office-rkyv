# Tasks

## 1. The seeder

- [x] 1.1 Add the on/off decision as a pure function in `src/OfficeRkyv.Api` (seed only when the environment is Development and `Seed:DemoData` is not `false`) with unit-level tests for the four combinations in the spec; verify the tests pass
- [x] 1.2 Add the seeder in `src/OfficeRkyv.Api`: find the demo user by email, and if absent send `CreateUserCommand`, `CreateArchiveCommand` and three `CreateDocumentCommand`s through `IMessageBus`, with the demo content as constants; verify `just build` succeeds without warnings
- [x] 1.3 Make the seeder catch failures and log through `[LoggerMessage]` methods: Information when it seeded, Warning with the exception when it failed, nothing when the demo user existed; verify no `logger.LogXxx` call was added (`grep -rn 'logger\.Log[A-Z]' src/OfficeRkyv.Api` finds nothing new)

## 2. Pipeline tests

- [x] 2.1 Add pipeline-level tests that run the seeder against `PipelineHost` and read back through the gRPC clients: an empty database gets the user, the empty archive "Taxes 2026" and the three inbox documents; a database with another user keeps that user unchanged; verify they pass
- [x] 2.2 Add pipeline-level tests for "once per database": running the seeder twice leaves one user, one archive and three documents; after deleting the archive and one document, a further run recreates neither; verify they pass
- [x] 2.3 Add a pipeline-level test that a seeded document can be filed into the seeded archive through the API; verify it passes
- [x] 2.4 Add a pipeline-level test that a refused first command ends the seed without throwing, creates no archive or document, and logs one Warning (capturing logger); verify it passes
- [x] 2.5 Add a pipeline-level test that a `PipelineHost` whose environment is Development has no users after startup, proving the shared composition does not seed; verify it passes

## 3. Wiring

- [x] 3.1 Register the seeder in `src/OfficeRkyv.Api/Program.cs`, guarded by the decision from 1.1, as a hosted service after the store, or from `ApplicationStarted` if the bus is not ready that early; add `"Seed": { "DemoData": true }` to `appsettings.Development.json`; verify `AddOfficeRkyvApi` and `src/OfficeRkyv.Desktop` are unchanged (`git diff --stat` lists neither)
- [x] 3.2 Run `dotnet test --project tests/OfficeRkyv.UnitTests`; verify all unit, pipeline and UI-logic tests pass

## 4. Functional tests

- [x] 4.1 Add a fixture deriving from `AspireFixture<Projects.OfficeRkyv_AppHost>` that sets `Seed__DemoData=false` for `api-svc`, and use it in `LandingPageTests`, `WorkspaceTests` and `SettingsTests`; add a functional-level check in an unskipped test that the landing page shows the empty state ("No users yet") before any user is created
- [x] 4.2 Run the functional tests (only when no AppHost is already running); verify all pass. A failure of the settings sidebar test alone is the known flake: rerun before investigating

## 5. Guideline docs

- [x] 5.1 Update `docs/architecture.md`, "One API composition": the API server adds the development seed around the shared calls, the desktop app does not, and `Seed__DemoData=false` switches it off; verify the section names the setting
- [x] 5.2 Update `docs/testing.md`, "Functional tests": the fixture starts the app with the seed off, so tests begin with an empty database; verify the fixture's class name in the doc exists in the code

## 6. By hand

- [x] 6.1 Start the app through the AppHost (restart the one under `dotnet watch`; do not start a second); verify the landing page lists "Demo User", the workspace shows the empty archive "Taxes 2026" and three inbox documents, and the `api-svc` log has the line "Seeded demo data"
- [x] 6.2 Change a file in the API so `dotnet watch` restarts only the API; verify there is still one demo user, one archive and three documents, and no second "Seeded demo data" line
- [x] 6.3 Desktop: run `just desktop`; verify no "Demo User" appears that was not there before, and stdout has no "Seeded demo data" line and no exception
- [x] 6.4 Run `just format`; verify it leaves no changes
