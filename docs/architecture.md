# Architecture

The map of the repo: which project owns what, which may reference which, how one request travels, how the two hosts differ, and the tooling around the code. Read it first when you are new to the repo or when a change adds, moves or connects projects. The other docs go into one area each: [api.md](api.md), [ui.md](ui.md), [desktop.md](desktop.md), [testing.md](testing.md), [observability.md](observability.md).

`openspec/config.yaml` repeats the summary of this doc in its `context`, so that planning starts with the map. When the projects or the request path change, change both.

## Projects

Sources are in `src/`, tests in `tests/`.

| Project | Owns |
|---|---|
| `OfficeRkyv.Contracts` | The `.proto` files and the stable error codes (`ErrorCodes.cs`). The wire contract between API and UI. |
| `OfficeRkyv.Api` | The gRPC services, one folder per feature under `Features/`, and what they share under `Shared/` (among it the blob store for file content). Also a library: the desktop app and the tests host it. |
| `OfficeRkyv.UI` | A Razor class library with every page, dialog, facade and resource of the shared UI. It knows no host. |
| `OfficeRkyv.Web` | The Blazor Server host of the UI, plus the two components only a web host has (`ReconnectModal`, `Error`). The development and functional-test harness. |
| `OfficeRkyv.Desktop` | The Linux GTK4 host of the UI, with the API in the same process. Experimental backend. |
| `OfficeRkyv.ServiceDefaults` | What every server process gets: OpenTelemetry, health checks, service discovery, HTTP resilience. |
| `OfficeRkyv.AppHost` | The Aspire orchestration: a SQLite database, the API (`api-svc`) and the web host (`web-svc`). |
| `tests/OfficeRkyv.UnitTests` | Unit, pipeline and UI-logic tests. |
| `tests/OfficeRkyv.FunctionalTests` | Playwright tests against the AppHost. |

## References

As the project files have it:

| Project | References |
|---|---|
| `Contracts`, `ServiceDefaults` | nothing of ours |
| `Api` | `Contracts`, `ServiceDefaults` |
| `UI` | `Contracts` |
| `Web` | `UI`, `Contracts`, `ServiceDefaults` |
| `Desktop` | `Api`, `UI` |
| `AppHost` | `Api`, `Web` |

The rules behind the table:

- **`UI` never references `Api`.** It talks to the API only through the generated gRPC clients from `Contracts`, in both hosts, including the desktop where the API is in the same process.
- **`UI` never references a host.** What differs per host reaches it through an interface the host implements (`IUserSettingsService`) or a registration the host makes (DotNetQuery options, client addresses).
- **`Web` does not reference `Api`.** The web host is a separate process and finds the API by service discovery.
- **`Contracts` holds only what crosses the wire.** Domain types stay in `Api`.

## One request

```
page or dialog (UI)
  -> DotNetQuery query or mutation
  -> facade (UI/Shared)
  -> generated gRPC client (Contracts)
        ~~ network (web) or unix socket (desktop) ~~
  -> <Feature>ServiceImpl (Api)       maps the request to a command or query
  -> Wolverine                        runs validators, then the handler
  -> handler (Api/Features/.../Handlers)
  -> Fisher session                   appends events, updates the snapshot
  -> SQLite
```

An upload and a download are streams. The upload service method reads the header, and `DocumentIngest` writes the content to the blob store before it dispatches the command that creates the document; a download streams the blob of an existing document back. See [api.md](api.md#a-streaming-service-method).

A failure anywhere below the service becomes a gRPC status with a stable code in one place (`ErrorStatusProvider` and the validation adapter). The UI turns the code into localized text. The API side of this is in [api.md](api.md), the UI side in [ui.md](ui.md).

## One API composition

`OfficeRkyvApiExtensions` has the two calls every process that hosts the API makes:

- `AddOfficeRkyvApi(connectionString, blobDirectory, configureStore)` registers the event store (Fisher on SQLite), the file content store (a directory of files named by their SHA-256), Wolverine with the error mapping, and gRPC. The blob directory belongs to the database: both are one user's data, and a database that is replaced needs its content replaced with it.
- `MapOfficeRkyvApi()` maps the gRPC services.

The API server (`src/OfficeRkyv.Api/Program.cs`), the desktop (`DesktopApi`) and the pipeline tests (`PipelineHost`) all call these, so they run the same pipeline. A host adds only what is its own around them: service defaults and telemetry, ports or a socket, and where the data lives (the database file and the blob directory). Register a new feature inside these two methods, never in a host.

The API server adds one thing of its own beyond service defaults and telemetry: the development seed (`AddDevelopmentSeedData()` in `src/OfficeRkyv.Api/Program.cs`). In the Development environment it creates a demo user (`demo@example.com`) with an empty archive and three inbox documents, each from a sample PDF in `src/OfficeRkyv.Api/Data`, through the same ingest the upload uses and once per database: when the demo user exists it adds nothing. It is deliberately not part of the shared calls, so the desktop app, which is also started in Development by `just desktop` but keeps the user's real data, and the pipeline tests never seed. `Seed__DemoData=false` switches it off, and outside Development it never runs, whatever the setting says.

## Two hosts

| | Web | Desktop |
|---|---|---|
| Process | `Web` and `Api` are separate processes, started by the AppHost | One process holds the UI and the API |
| UI runtime | Blazor Server, one SignalR circuit per user | `BlazorWebView` in a GTK4 window |
| API address | `https://api-svc`, resolved by Aspire service discovery | A unix socket in the user's runtime directory |
| Database | A throwaway SQLite database per AppHost run | One file in the user's data directory |
| File content | `<database file>.blobs`, beside the throwaway database, so it has its lifetime | `blobs/` in the user's data directory |
| Settings | Cookies (`WebUserSettingsService`) | `settings.json` (`DesktopUserSettingsService`) |
| Saving a download | A browser download (`WebFileSaver`) | A GTK save dialog (`DesktopFileSaver`) |
| Language change | Sets a cookie and reloads the page | Applied in place |
| Telemetry | Service defaults; exported when an OTLP endpoint is configured | None; a console logger |
| Verified by | Functional tests | Unit tests for its logic, and by hand |

The web host stays the harness for development and for the functional tests. The desktop is the product for a single person on Linux; see [desktop.md](desktop.md) and `README.md` for how to run it and where it keeps its files.

## Tooling

- **Dev shell:** everything runs inside the Nix dev shell (`nix develop`, or direnv through `.envrc`). It provides the .NET 10 SDK, `just`, the protobuf tools and the OpenSpec CLI. A second shell, `.#desktop`, adds the GTK libraries that only running the desktop app needs.
- **Recipes:** `just` lists them. `just setup` once, `just dev` for the web app through the AppHost, `just desktop`, `just build`, `just test`, `just format`.
- **Formatting:** CSharpier, through `just format`. The pre-commit hook formats the staged files.
- **Pre-push:** `dotnet test --configuration Release`. Release treats warnings as errors (`Directory.Build.props`), so an analyzer warning fails the push.
- **Build settings:** `Directory.Build.props` sets the target framework, nullable reference types and the .NET analyzers for every project. `.editorconfig` holds the style and the analyzer severities.
- **Package versions:** central, in `Directory.Packages.props`. A project file names a package without a version.
- **Commits:** conventional commits.
- **Planning:** changes are planned with OpenSpec under `openspec/`. Specs in `openspec/specs` state required behaviour; `openspec/changes/archive` keeps the proposal, design and tasks of every finished change.
- **These docs:** they state the conventions that hold now, and the archived designs keep the reasoning. A change that sets or changes a convention updates the doc in the same change.

## Decided in

- [structured-error-codes](../openspec/changes/archive/2026-09-28-structured-error-codes/design.md) (D5): `Contracts` as a class library shared by API and UI.
- [add-linux-desktop-app](../openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md) (D1, D2, D3): the project layout, what moved into `UI`, and the API composition as extension methods.
- [add-document-file-upload](../openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D2, D3): where file content lives and how each host chooses its directory.
