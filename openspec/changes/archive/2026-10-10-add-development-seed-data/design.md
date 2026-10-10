# Design

## Context

See proposal.md for the motivation and specs/development-seed-data/spec.md for the requirements. What shapes the approach:

- The AppHost declares the database with `AddSqlite("officerkyv-db")` and no path, so the file is new on every AppHost start. Under `dotnet watch`, a code change in the API restarts only the API (`api-svc-rebuilder`), on the same file. The seed therefore meets both an empty and an already seeded database.
- `AddOfficeRkyvApi` / `MapOfficeRkyvApi` are the composition shared by the API server, the desktop app (`DesktopApi`) and the pipeline tests (`PipelineHost`). `docs/architecture.md` says a host adds only what is its own around them.
- The desktop app stores data in the user's real data directory and is started in Development by `just desktop`.
- The create handlers hold rules the seed should not copy: `CreateUserHandler` trims and normalizes the email and claims it as a natural key; `CreateArchiveHandler` and `CreateDocumentHandler` require an active owner. `FindUserByEmailQuery` finds a user by email, ignoring case, and does not return deleted users.
- Fisher runs its schema changes on startup (`ApplyAllDatabaseChangesOnStartup`) as part of host start. It also has an initial-data hook (`IInitialData`), which works on a session, below the handlers.
- The functional tests use `AspireFixture<Projects.OfficeRkyv_AppHost>` directly. The fixture type has overridable hooks (`ConfigureBuilder`, `ConfigureAppHost`, `Args`). `LandingPageTests` (currently skipped) asserts the empty landing page.
- Not verified: which environment the API gets under the Aspire test host. The design does not depend on it.

## Goals / Non-Goals

**Goals:**
- A fresh development database has a usable demo user without any action.
- No way for demo data to reach a non-Development database or the desktop app's data.
- The functional tests keep their empty starting point.

**Non-Goals:**
- Seeding files or document content (documents are metadata only today).
- A reset or re-seed command, or repairing partially deleted demo data.
- Configurable demo content.
- Seeding in the desktop app.
- Making the development database persistent.

## Decisions

### 1. The seed sends the existing create commands

A seeder in the API project sends, through Wolverine's `IMessageBus`: `FindUserByEmailQuery` for the demo email; if no user comes back, `CreateUserCommand`, then `CreateArchiveCommand` and three `CreateDocumentCommand`s with the new user's id as owner.

Alternative considered: Fisher's `IInitialData`, appending `UserCreated`, `ArchiveCreated` and `DocumentCreated` directly. Rejected: it repeats what the handlers know (trimming, the normalized email, the owner check) and would drift when a handler changes. The seeded data must be ordinary data (spec), and the handlers are the definition of that.

Verification: pipeline. The seeder runs against `PipelineHost`, and the result is read back through the gRPC clients.

### 2. The demo user is the marker for "already seeded"

Archives and documents have no unique key, so only the user can show that the seed ran. The seeder does nothing when the demo user exists, and creates everything when it does not. It does not look at archives or documents.

Consequences, both accepted: deleted demo archives or documents do not come back until the AppHost restarts; and if the API stops between creating the user and the last document, the rest is not added later. In development a restart of the AppHost gives a clean, fully seeded database.

If the demo user was deleted, `FindUserByEmailQuery` returns nothing and the seeder tries to create it again. Whether the store accepts that depends on how the natural key treats a deleted user, which this design does not rely on: a refusal is handled by decision 5.

Verification: pipeline (run twice; run after deleting the archive and a document).

### 3. Registered by the API server, not by the shared composition

`Program.cs` of `OfficeRkyv.Api` registers the seeder; `AddOfficeRkyvApi` does not know it. This keeps the desktop app and the pipeline tests free of it by construction, with no environment check to get wrong.

This follows `docs/architecture.md`: the seed is not a feature every host runs but something one host adds. The doc's rule is extended by one item, not departed from.

The seeder runs as a hosted service registered after the store, so the schema exists when it starts. Wolverine must be started before a message can be sent; if a hosted service starts too early for that, the seeder is triggered from `IHostApplicationLifetime.ApplicationStarted` instead. Which of the two is needed is settled by the pipeline test of decision 1, not assumed.

Verification: pipeline. A `PipelineHost` started with `ASPNETCORE_ENVIRONMENT=Development` semantics (environment name set on the builder) has no users.

### 4. One setting, evaluated together with the environment

A setting `Seed:DemoData` (boolean). The decision is a pure function: seed when the environment is Development and the setting is not `false`. Absent means on. Outside Development the answer is always no, so a stray `Seed__DemoData=true` in a deployment cannot seed.

`appsettings.Development.json` of the API states `"Seed": { "DemoData": true }` so the setting is discoverable.

Alternative considered: environment only, no setting. Rejected: the functional tests need an off switch that does not depend on which environment the Aspire test host uses.

Verification: unit, on the pure function, for the four combinations in the spec.

### 5. Failure is logged, never fatal

The seeder catches any exception from its commands, logs a Warning with the exception, and returns. It logs one Information line when it seeded ("Seeded demo data for {Email}") and nothing when the demo user already existed. Both are `[LoggerMessage]` methods on the seeder, as `docs/observability.md` requires; the levels follow that doc (Information for the life of a host, Warning for a fault the app continues after).

Verification: pipeline, with a seeder whose first command is made to fail (for example a demo email the validator refuses, injected through the seeder's data) and a capturing logger.

### 6. The functional tests switch the seed off

`tests/OfficeRkyv.FunctionalTests` gets a fixture class deriving from `AspireFixture<Projects.OfficeRkyv_AppHost>` that sets `Seed__DemoData=false` on the `api-svc` resource, and the three test classes use it. The exact hook (`ConfigureBuilder` adding an environment variable to the resource, or `Args` read by the AppHost) is chosen when implementing, by which one the fixture supports for a project resource.

Alternative considered: let the tests run with demo data. Rejected: `LandingPageTests` is written against an empty database, and tests should not depend on development conveniences.

Verification: functional. `WorkspaceTests` and `SettingsTests` keep passing; a check that the landing page shows the empty state is added to an unskipped test, since `LandingPageTests` is skipped.

### 7. Demo content is constants on the seeder

Email, display name, archive name and document titles are constants in the seeder. They are data, not UI text, so they are not localized and the rule against hardcoded user-facing text does not apply.

### Observability

- Traces: the seed's commands produce the same Wolverine and event-store spans as any command, without a gRPC parent. No new span, no new attribute.
- Metrics: none new; the existing handler and store metrics count the seed's five commands.
- Logs: one Information line on a successful seed, one Warning on failure, both `[LoggerMessage]`. The demo email is the only value logged and is not personal data.

### Guideline docs to update

- `docs/architecture.md`, "One API composition": name the development seed as something the API server adds around the shared calls, and state that the desktop app does not.
- `docs/testing.md`, "Functional tests": the fixture starts the app with the seed switched off, so tests begin with an empty database.

`docs/api.md` and `docs/observability.md` need no change.

## Risks / Trade-offs

- [The seeder starts before Wolverine can handle a message] → Decided by the pipeline test; fall back to `ApplicationStarted` (decision 3).
- [The web UI loads the landing page before the seed finished] → The window is five local commands. The AppHost already makes `web-svc` wait for `api-svc`; a reload shows the user. Not worth blocking startup for.
- [A deployment runs in Development by mistake and gets a demo user] → Accepted: Development is by definition not a deployment. The desktop app, the one host a user runs in Development, is excluded by construction.
- [The demo user hides the empty landing page from the developer] → Set `Seed__DemoData=false` for that session; documented in `docs/architecture.md`.
- [A half-finished seed leaves a user without archive or documents] → Accepted (decision 2); restart the AppHost.

## Open Questions

- Does Fisher release the natural key of a deleted user? It only decides whether a deleted demo user comes back on the next API restart or produces the Warning of decision 5. Either outcome satisfies the spec.
