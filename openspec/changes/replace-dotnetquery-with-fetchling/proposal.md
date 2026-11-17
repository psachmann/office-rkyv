# Proposal

## Why

DotNetQuery, the data-fetching library behind every page, was renamed Fetchling in 3.0. The `DotNetQuery.*` packages stay on 2.x and get no 3.0 release, so the project can only move forward by swapping package IDs. Doing it now keeps it a mechanical rename: Fetchling 3.0 changes no types, members or behaviour beyond the name.

## What Changes

- Replace the four `DotNetQuery.*` package versions in `Directory.Packages.props` with `Fetchling.Core`, `Fetchling.Extensions.DependencyInjection`, `Fetchling.Blazor` and `Fetchling.Blazor.DevTools`, all 3.0.0.
- Update the `PackageReference` entries in the UI, Web, Desktop and UnitTests projects.
- Rename namespaces in `GlobalUsings.cs`, `_Imports.razor`, `Program.cs`, `MauiProgram.cs` and the two facade test files, and rename `AddDotNetQuery` to `AddFetchling`.
- The telemetry source and meter name changes from `DotNetQuery` to `Fetchling`, and metrics from `dotnetquery.*` to `fetchling.*`. The app subscribes through `QueryTelemetry.SourceName`, so it needs no code change; anything outside the repo that spells the old names out would.
- Update the guideline docs and `openspec/config.yaml` to name Fetchling.
- Fix `.vscode/tasks.json`, which points at a nonexistent `DotNetQuery.slnx` instead of `OfficeRkyv.slnx`.
- **BREAKING** (outside the repo only): dashboards, alerts or collector config that match `dotnetquery.*` metric names, the `DotNetQuery` source name or `DotNetQuery.Core.*` log categories.

Not in this change: turning `DocumentsFacade.UploadAsync` into a mutation. The TODO stays until that is its own change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. No spec names the library or its telemetry identifiers, so no requirement changes (`skip_specs: true`).

## Impact

- **Guideline docs** whose text names the library: `docs/ui.md`, `docs/architecture.md`, `docs/testing.md`, `docs/observability.md`. No convention changes; the wording follows the new name.
- **Code:** `src/OfficeRkyv.UI`, `src/OfficeRkyv.Web`, `src/OfficeRkyv.Desktop`, `tests/OfficeRkyv.UnitTests`, `Directory.Packages.props`.
- **Dependencies:** four packages swapped, 2.0.0 to 3.0.0.
- **Archived changes** under `openspec/changes/archive` keep the old name as history.
