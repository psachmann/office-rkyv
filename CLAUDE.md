# CLAUDE.md

office-rkyv is paperless office software: a gRPC API on an event store, and one Blazor UI served by a web host and a Linux desktop host. The conventions live in `docs/`; this file is the index. Read the doc for an area before changing it.

## Read before you touch

| Before touching | Read |
|---|---|
| Anything, for orientation | [docs/architecture.md](docs/architecture.md) |
| `src/OfficeRkyv.Api`, `src/OfficeRkyv.Contracts` | [docs/api.md](docs/api.md) |
| `src/OfficeRkyv.UI`, `src/OfficeRkyv.Web` | [docs/ui.md](docs/ui.md) |
| `src/OfficeRkyv.Desktop` | [docs/desktop.md](docs/desktop.md) |
| `tests/`, or adding tests | [docs/testing.md](docs/testing.md) |
| Telemetry or logging | [docs/observability.md](docs/observability.md) |

## Rules for every session

- **No hardcoded user-facing text.** It comes from `.resx` resources through `IStringLocalizer<T>` ([docs/ui.md](docs/ui.md#localization)).
- **Log only through `[LoggerMessage]` methods**, never `logger.LogXxx(...)` directly ([docs/observability.md](docs/observability.md#logging)).
- **Don't remove a desktop workaround** without re-checking it on a real desktop ([docs/desktop.md](docs/desktop.md#workarounds-for-the-experimental-backend)).
- **Run things through `just`** inside the Nix dev shell: `just build`, `just test`, `just format`, `just dev`, `just desktop`.
- **Conventional commits.**

## Keeping the docs true

The docs state the conventions that hold now; the reasoning behind them is in the archived designs under `openspec/changes/archive`. A change that sets or changes a convention updates the doc in the same change.
