# Design

## Context

See proposal.md for the motivation. The state this design starts from:

- **`CLAUDE.md`** (about 7.5 KB, loaded in every session) documents two topics in detail, UI localization and the desktop app, and nothing else. One source comment points at it (`WebUserSettingsService.cs`, "see CLAUDE.md").
- **`openspec/config.yaml`** has a five-line `context` and no `rules` or `operations`. OpenSpec puts `context` into the instructions of every artifact and `rules` into the instructions of the artifact they are keyed by, so today almost nothing reaches planning.
- **`openspec/testing.md`** names two test levels. Nothing links to it.
- **Conventions that exist only as archived decisions or code comments:**
  - the API composition (`AddOfficeRkyvApi`, `MapOfficeRkyvApi`) shared by the server, the desktop and the tests;
  - the shape of a feature slice, and the event-sourcing rules (facts recorded in events, the inbox as a query, cascades through the outbox, the version guard);
  - the error flow (one mapping place, stable error codes, localized on the client);
  - one facade per service over DotNetQuery;
  - telemetry by subscribing to what Wolverine, Fisher and DotNetQuery emit, verified with an in-memory exporter.
- **Tests:** the `OfficeRkyv.UnitTests` project holds three kinds of test (pure unit tests, pipeline tests on `PipelineHost`, UI-logic tests on the facades with fake gRPC clients). `OfficeRkyv.FunctionalTests` drives the AppHost with Playwright. The desktop is checked by hand.
- **Logging:** twelve direct `logger.LogXxx(...)` calls. Two are in the API (`GreeterService`, `ErrorStatusProvider`), ten in the desktop host. Handlers do not log. `ErrorMappingExtensions` already silences Wolverine's per-failure warnings so that only unexpected failures are logged, at Error.
- **Build:** `Directory.Build.props` enables the .NET analyzers at the latest level and treats warnings as errors in Release. The pre-push hook runs `dotnet test --configuration Release`.

## Goals / Non-Goals

**Goals:**

- One place per convention, readable by a person and by an agent, with `CLAUDE.md` and `config.yaml` pointing at it instead of repeating it.
- Planning artifacts that cannot skip testing, telemetry and doc upkeep without saying so.
- A logging convention the build enforces.
- No loss of the verified detail that `CLAUDE.md` holds today (the desktop workarounds in particular).

**Non-Goals:**

- No new log messages, spans or metrics, and no logging in handlers.
- No renaming or splitting of the `OfficeRkyv.UnitTests` project; the testing doc says what it holds.
- No rewriting of archived designs; the docs link to them for the reasoning.
- No documentation site or generator; the docs are plain Markdown read in the repo.
- No automated check that links in the docs resolve (see Open Questions).

## Decisions

### D1. `docs/` at the repo root, one topic per file, current state only

Six files: `architecture.md`, `api.md`, `ui.md`, `desktop.md`, `testing.md`, `observability.md`. Each states the conventions that hold now, names the code that embodies them, and ends with a "Decided in" list of links to the archived designs that explain why.

Each file opens with one paragraph saying what it covers and when to read it, so a reader arriving from the index knows at once whether they are in the right place.

- **Why not under `openspec/`:** the conventions are about the code, not about the planning workflow, and a person looks for docs in `docs/`. `openspec/testing.md` went unread there.
- **Why not one `ARCHITECTURE.md`:** the point of the index (D3) is to load only what a task needs; one large file is all or nothing.
- **Why "current state only":** a doc that also carries history grows with every change and nobody can tell which paragraph still holds. The archive already keeps the history.

### D2. What each doc holds

| Doc | Sections | Source |
|---|---|---|
| `architecture.md` | The projects and what each owns; which project may reference which; the request path from page to SQLite; the two hosts (web and desktop) and what differs between them; the one API composition; the tooling (Nix shell, `just`, CSharpier, hooks, central package versions) | Project files, `OfficeRkyvApiExtensions`, `README.md`, desktop design D1 to D3 |
| `api.md` | Anatomy of a feature slice (`<Feature>.cs` aggregate, `Commands`, `Events`, `Queries`, `Mapper`, `ServiceImpl`, `Handlers/`); handler shapes (static handler, `Validate` step, decider function); event-sourcing rules; validation; the error flow and the stable error codes; how to add an RPC | `src/OfficeRkyv.Api/Features/Archives` as the worked example; designs of `landing-page-user-management`, `structured-error-codes`, `add-archives-and-inbox` |
| `ui.md` | The localization section of today's `CLAUDE.md`, moved; facades and DotNetQuery (shared queries, per-component mutations, invalidation keys); error codes to localized text; `IUserSettingsService` (moved from the desktop section); Radzen components before own CSS; `data-testid` | `CLAUDE.md`, `src/OfficeRkyv.UI/Shared`, designs of `add-ui-localization`, `add-settings-page`, `move-workspace-nav-into-sidebar` |
| `desktop.md` | The desktop section of today's `CLAUDE.md`, moved: what the app is, and every workaround for the experimental backend with its reason | `CLAUDE.md`, the spike's results, desktop design D6 |
| `testing.md` | The five levels (D4); stack per level; conventions per level; which level to pick; known flakiness and how to treat it | `openspec/testing.md`, the test projects |
| `observability.md` | Tracing and metrics (subscribe, register per service, constants where the library has them, verify with the in-memory exporter, export only with an OTLP endpoint); logging (D5); what must never appear in a span or a log | `add-library-telemetry` design, `TelemetryExtensions`, `ErrorMappingExtensions`, the `observability` spec |

The text that moves out of `CLAUDE.md` is moved, not rewritten: it was verified on a real desktop and against the running web app, and a paraphrase could lose a detail. Only headings and cross-references are adjusted.

### D3. `CLAUDE.md` becomes an index: a few always-on rules and a routing table

`CLAUDE.md` keeps two things, in well under a screen:

1. **Rules that apply to every session**, one line each:
   - never hardcode user-facing text; it comes from `.resx` through `IStringLocalizer<T>`;
   - log only through `[LoggerMessage]` methods;
   - do not remove a desktop workaround without re-checking it on a real desktop;
   - run things through the `just` recipes inside the Nix shell;
   - conventional commits.
2. **A routing table**: "before you touch X, read Y".

| Before touching | Read |
|---|---|
| Anything, for orientation | `docs/architecture.md` |
| `src/OfficeRkyv.Api`, `src/OfficeRkyv.Contracts` | `docs/api.md` |
| `src/OfficeRkyv.UI`, `src/OfficeRkyv.Web` | `docs/ui.md` |
| `src/OfficeRkyv.Desktop` | `docs/desktop.md` |
| `tests/`, or adding tests | `docs/testing.md` |
| Telemetry or logging | `docs/observability.md` |

Alternatives considered:

- **`@docs/...` imports in `CLAUDE.md`:** an import is loaded with the file, so every session would again carry everything. That is the cost this change removes.
- **A `CLAUDE.md` in each project folder:** it loads on demand, but it scatters the conventions over seven folders, hides them from a person browsing docs, and gives OpenSpec nothing to point at.
- **Path-scoped rule files under `.claude/rules/`:** the same content would live in a second, tool-specific place. The routing table does the same job in plain Markdown.

The comment in `WebUserSettingsService.cs` that says "see CLAUDE.md" changes to `docs/ui.md`.

### D4. Five test levels, named for what they run

| Level | Project | What runs | Example |
|---|---|---|---|
| Unit | `UnitTests` | Pure logic, no host | `EmailAddressTests`, validators |
| Pipeline | `UnitTests` | `PipelineHost`: the real Wolverine pipeline over a real Fisher store in a temp SQLite file, gRPC over the wire | `UserPipelineTests`, `ApiTelemetryTests` |
| UI logic | `UnitTests` | Facades and error mapping with fake gRPC clients, no browser | `WorkspaceFacadeTests` |
| Functional | `FunctionalTests` | Playwright against the Aspire AppHost; one AppHost per session, so each test creates its own user | `WorkspaceTests` |
| By hand | none | The desktop app, with its stdout as evidence | Desktop changes |

The doc says plainly that pipeline tests are integration tests living in a project named `UnitTests`. The names are the vocabulary the `rules` in D6 refer to, so they must be exact and stable.

The existing conventions carry over unchanged: TUnit assertions, `TUnit.Mocks`, fixtures where setup is shared, page objects, and locating by role or label first and by `data-testid` for elements with no name of their own (which is what `WorkspacePage` does; today's file says only "we use `data-testid`").

### D5. Logging: `[LoggerMessage]` methods on the owning type, enforced by CA1848

**The convention.**

- A log message is a `[LoggerMessage]` method with an explicit `Level` and `Message`. It is a `private static partial void Log<What>(ILogger logger, ...)` on the type that logs, which becomes `partial`. The methods sit together at the end of the type.
- No hand-assigned event ids: nothing consumes them. The method name is the event name. _As built: the generator derives a numeric id from the method name, so the console prefix changes from `[0]` to that number; the message text and level are unchanged._
- Log at the edges: host lifecycle, and failures nobody expected. Handlers do not log; the trace carries the story of a request. Expected refusals (validation, not found, a business rule) are not logged, which is what the error mapping already ensures.
- Levels as used today: Information for lifecycle, Warning for a fault the app continues after, Error for an unexpected failure.
- Never log document contents or personal data such as an email address. This mirrors the `observability` spec's rule for event-store spans. It is an assumption of this design, since no log call does it today (`GreeterService` logs a name, but it is sample code).

**Why methods on the owning type and not a shared `Log` class per project:** the message stays next to the one place that emits it, and deleting the code deletes the message. A shared class turns into a list nobody prunes.

**The conversion.** All twelve calls keep their text, level and arguments.

- **Static classes** (`SignalHandling`, `GtkAppearance`, `DevelopmentConsole`) become `static partial`.
- **`Main.razor`** cannot declare a generated method in its `@code` block, because the logging generator does not see what the Razor generator produces. It gets a code-behind `Main.razor.cs` with the partial method.
- **`DesktopSettingsStore`** takes an optional `ILogger?`. A generated method needs a logger, so the store keeps `logger ?? NullLogger.Instance`.
- **Computed arguments** (`string.Join(...)` in `DesktopHost`, the `"dark"`/`"light"` choice in `GtkAppearance`) stay as arguments. They are evaluated even when the level is off, as they are today; these are startup and settings-change paths, so it does not matter.

**Enforcement.** `.editorconfig` sets `dotnet_diagnostic.CA1848.severity = warning` for C# files. CA1848 flags a direct `LogXxx` call. Debug builds show the warning; the Release build, which the pre-push hook runs, fails on it.

- **Why an analyzer and not only the doc:** a rule that only a document states is the kind of implicit convention this change is meant to end.
- **Limit:** analyzers may not report on code the Razor generator produces, so a direct call in a `.razor` file could pass. The doc states the rule for that case.

### D6. `openspec/config.yaml`: the map in `context`, the checks in `rules`, the upkeep in `operations`

`context` stays on one screen, because it is added to every artifact's instructions. It holds what a planner cannot get quickly from the code, and points to the docs for the rest:

```yaml
context: |
  Product: office-rkyv, paperless office software to store and search any kind of document.

  Stack: .NET 10, C#. API: ASP.NET Core gRPC, Wolverine (message handling), Fisher (event store
  on SQLite), FluentValidation, Mapperly. UI: Blazor with Radzen components, DotNetQuery for data.
  Tests: TUnit, Playwright, Aspire. Nix dev shell, `just` recipes, CSharpier, conventional commits.

  Projects (src/):
  - Contracts: the .proto files and the error codes, shared by API and UI.
  - Api: gRPC services, one folder per feature under Features/. Composed by AddOfficeRkyvApi and
    MapOfficeRkyvApi for every process that hosts it.
  - UI: Razor class library with all pages, facades and localization. Knows no host.
  - Web: Blazor Server host of UI. Also the development and functional-test harness.
  - Desktop: Linux GTK4 host of UI with the API in the same process. Experimental backend.
  - ServiceDefaults: OpenTelemetry, health checks, service discovery.
  - AppHost: Aspire orchestration for development and the functional tests.

  One request: page -> DotNetQuery query or mutation -> facade -> gRPC client -> <Feature>ServiceImpl
  -> Wolverine -> handler -> Fisher session -> SQLite. Failures become gRPC statuses with a stable
  error code in one place; the UI maps the code to localized text.

  Guideline docs are the source of truth for conventions. Read the ones a change touches before
  writing an artifact:
  - docs/architecture.md: projects, dependencies, hosts, tooling
  - docs/api.md: feature slices, event sourcing, validation, errors
  - docs/ui.md: localization, facades, settings, styling
  - docs/desktop.md: the desktop host and its workarounds
  - docs/testing.md: the test levels (unit, pipeline, UI logic, functional, by hand)
  - docs/observability.md: tracing, metrics, logging

rules:
  proposal:
    - Name each guideline doc under docs/ whose conventions this change sets or changes, or state that there is none.
  specs:
    - Write each scenario so that one of the test levels in docs/testing.md can verify it.
  design:
    - For each decision that needs verification, name the test level from docs/testing.md that covers it.
    - State the effect on traces, metrics and logs, following docs/observability.md, or state that there is none.
    - List the guideline docs under docs/ that this change must update, or state that there are none.
    - Where a decision departs from a guideline doc, say so and say why.
  tasks:
    - Name the test level from docs/testing.md in every task that adds or changes tests.
    - Include a task for each guideline doc the design says must be updated.
    - For desktop behaviour, which is verified by hand, say what to observe in the app's stdout.

operations:
  apply:
    guidance:
      - Before editing a part of the repo, read the guideline doc that CLAUDE.md lists for it.
      - If the implementation departs from a guideline doc, update the doc in the same change.
  archive:
    guidance:
      - Before archiving, check that every guideline doc the design lists was updated. The docs state the current convention; the archived design keeps the reasoning.
```

- **Every rule accepts "none" with a statement.** A rule that forces a telemetry section onto a change that renames a button gets ignored; a rule that asks for one line saying "none" does not.
- **Why rules and not only docs:** the docs say how to do a thing; the rules make a plan say that it did. These are the two hooks that keep the docs true: the design names the doc it changes, and archiving checks it.
- **Why the architecture map is in `context` and not only in `docs/architecture.md`:** a proposal's capability list and a design's first decisions depend on knowing which project owns what. Requiring a file read before that is the kind of step that gets skipped. The map is short enough to repeat; the detail is not, so it is only linked.

The two places that carry the map (`context` and `docs/architecture.md`) can drift. The archive guidance covers it, since a change that adds or moves a project lists `docs/architecture.md`, and the doc carries a note that `config.yaml` repeats its summary.

### D7. Order: docs first, then the index, then the config, then the logging

The index and the config point at the docs, so the docs must exist first. `CLAUDE.md` is cut only after `ui.md` and `desktop.md` hold its text. The logging conversion comes last and is independent; `observability.md` is written to describe the convention, and the code catches up within the same change.

## Risks / Trade-offs

- **Detail leaves every session.** The desktop workarounds and the localization rules are no longer in context unless the table sends the reader there. → The always-on rules keep the two that cause damage when missed (hardcoded text, removing a workaround), and `config.yaml` names the docs at planning time.
- **The docs drift from the code.** → The design rule and the archive guidance (D6). The docs name code by type and folder, not by line, so a refactor breaks fewer references.
- **The rules add ceremony to small changes.** → Each accepts a one-line "none".
- **`context` costs tokens in every artifact.** → One screen, with the detail linked.
- **The source generator may not run in the desktop project.** It is a Razor SDK project with no framework reference of its own; the generator normally arrives through the ASP.NET Core shared framework or the `Microsoft.Extensions.Logging.Abstractions` package. → Convert one desktop call first and build. If the partial method has no generated body, add the package reference (and its version in `Directory.Packages.props`).
- **CA1848 may flag code that is not ours to change**, such as generated code. → The Release build shows it at once; narrow the `.editorconfig` section to `src/` or exclude the path, and record which.
- **`ErrorStatusProviderTests` uses its own `CapturingLogger`.** Generated methods call `IsEnabled` before `Log`, and pass a generated state type instead of formatted values. → Run that test right after converting `ErrorStatusProvider`; if it asserts on the state's type or its logger reports "not enabled", adjust the test double, not the production code.
- **Two files carry the architecture map.** → See D6.

## Migration Plan

One change, no deployment. Rollback is a revert: nothing in the docs, the index or the config is read at run time, and the logging conversion keeps every message as it was.

## Open Questions

- Should a unit test check that every relative link in `CLAUDE.md` and `docs/` resolves? It would catch a renamed file at pre-push. It is left out here to keep the change to what was asked; the tasks check the links once by hand.
