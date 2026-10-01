# Proposal

## Why

Most of the project's conventions are implicit. The API's architecture, the test levels and the telemetry approach exist only in the code and in decisions scattered over nine archived designs; `openspec/config.yaml` gives planning five lines of context and no rules; `openspec/testing.md` is referenced by nothing, so no workflow reads it; and there is no logging convention at all. New proposals, designs and code therefore depend on someone rediscovering the rules each time.

## What Changes

- **Guideline docs as the single source.** A new `docs/` folder holds the current conventions, one topic per file: `architecture.md`, `api.md`, `ui.md`, `desktop.md`, `testing.md`, `observability.md`. They state what holds today; the archived designs stay as the record of why.
- **`CLAUDE.md` shrinks to an index.** Its localization and desktop sections move into `docs/ui.md` and `docs/desktop.md`. What remains is a short list of rules needed in every session and a table that says which doc to read before touching which part of the repo.
- **`openspec/testing.md` moves to `docs/testing.md` and is completed.** It names the five test levels the code already has (unit, pipeline, UI logic, functional, by hand for the desktop) and says when each applies.
- **`openspec/config.yaml` carries the map and the checks.**
  - `context`: a one-screen architecture map and pointers to the docs.
  - `rules`: per-artifact checks for proposal, specs, design and tasks (test level, telemetry impact, which guideline doc changes).
  - `operations`: guidance that apply follows the docs and that archiving a change which set or changed a convention updates the doc.
- **Logging gets a convention, and the code follows it.** Log messages are declared with the `LoggerMessage` source generator. The existing log calls (two in the API, ten in the desktop host) are converted, and the analyzer rule CA1848 is raised to a warning so a direct `logger.LogXxx(...)` call fails the Release build that the pre-push hook runs.
- **`README.md` links to `docs/`** so the docs are reachable for a person, not only through `CLAUDE.md`.

No log message text, level or timing changes. The one visible difference is the event id in a log line's prefix, which the generator derives from the method name where it used to be `0`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This change is documentation, workflow configuration and a behaviour-preserving refactor of how log messages are declared, so the change sets `skip_specs: true`. The `observability` spec covers traces and metrics and is not touched.

## Impact

- **New files:** `docs/architecture.md`, `docs/api.md`, `docs/ui.md`, `docs/desktop.md`, `docs/testing.md`, `docs/observability.md`.
- **Rewritten:** `CLAUDE.md` (to an index), `openspec/config.yaml` (`context`, `rules`, `operations`).
- **Removed:** `openspec/testing.md` (moved).
- **Edited:** `README.md` (a link to `docs/`), `.editorconfig` (CA1848 severity).
- **Code:** the log call sites in `src/OfficeRkyv.Api` (`GreeterService`, `ErrorStatusProvider`) and `src/OfficeRkyv.Desktop` (`DesktopHost`, `SignalHandling`, `DevelopmentConsole`, `GtkAppearance`, `DesktopSettingsStore`, `DesktopUserSettingsService`, `Main.razor`).
- **Dependencies:** none expected. If the source generator is not active in the desktop project (Razor SDK, no direct framework reference), it needs a `Microsoft.Extensions.Logging.Abstractions` package reference; see the design.
- **Workflow:** every later OpenSpec artifact is written against the new context and rules, and archiving gains a step.
