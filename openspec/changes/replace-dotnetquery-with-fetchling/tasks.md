# Tasks

## 1. Packages

- [x] 1.1 In `Directory.Packages.props`, replace the four `DotNetQuery.*` versions with `Fetchling.Core`, `Fetchling.Extensions.DependencyInjection`, `Fetchling.Blazor`, `Fetchling.Blazor.DevTools` at 3.0.0. Verify: `grep -i dotnetquery Directory.Packages.props` finds nothing.
- [x] 1.2 Rename the `PackageReference` entries in the UI, Web, Desktop and UnitTests project files. Verify: `mise run build` restores.

## 2. Code

- [x] 2.1 Rename namespaces in `GlobalUsings.cs`, `Components/_Imports.razor`, `Program.cs`, `MauiProgram.cs`, `UsersFacadeTests.cs` and `WorkspaceFacadeTests.cs`, and `AddDotNetQuery` to `AddFetchling` in those files. Verify: `mise run build` succeeds with no CS0246.
- [x] 2.2 Reword the comments that name the library (`Program.cs`, `OfficeRkyvUiExtensions.cs`). Verify: `grep -ri dotnetquery src tests` finds nothing.
- [x] 2.3 Fix `.vscode/tasks.json` to use `OfficeRkyv.slnx`. Verify: the file names no `DotNetQuery.slnx`.

## 3. Docs

- [x] 3.1 Update `docs/ui.md`, `docs/architecture.md`, `docs/testing.md` and `docs/observability.md` to name Fetchling (including "DotNetQuery.Blazor" as "Fetchling.Blazor"). Verify: grep for `DotNetQuery` and `DotNet Query` in `docs/` finds nothing.
- [x] 3.2 Update `openspec/config.yaml` (stack line and request flow). Verify: grep finds nothing.
- [x] 3.3 In `TODOS.md`, rename the "Upload as a DotNet Query mutation" item to Fetchling and drop its link to the missing `dotnet-query-streaming-issue.md`. Verify: the item stays, with no dead link.

## 4. Verification

- [ ] 4.1 Run `mise run format` and `mise run build`. Verify: both succeed with no new warnings.
- [ ] 4.2 Run `mise run test` (unit, pipeline, UI-logic and functional levels from docs/testing.md). Verify: results match the pre-change run; known flaky tests are those listed in TODOS.md and docs/testing.md.
- [ ] 4.3 Run the web host (`mise run dev`) and check a list page loads and the exported telemetry shows the `Fetchling` source and `fetchling.*` metrics. Verify: observed in the Aspire dashboard.
- [ ] 4.4 Run `mise run desktop` and open a list page. Verify: the page renders and the app's stdout shows no missing-assembly or `Fetchling` errors.
