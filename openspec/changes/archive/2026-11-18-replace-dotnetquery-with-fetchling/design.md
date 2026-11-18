# Design

## Context

Fetchling 3.0 is DotNetQuery 2.x under a new name: every package, assembly and namespace is renamed, nothing else about the API changes (its migration guide). The old packages get no 3.0 release, so there is no in-place upgrade. See proposal.md for why.

The library is used in the UI project (facades, `Transition`, `QueryDevTools`), both hosts (registration, and on the web the telemetry subscription) and two unit-test files.

## Goals / Non-Goals

**Goals:**
- No `DotNetQuery` name left in live code, project files, docs or `openspec/config.yaml`.
- Same behaviour and the same build and test results as before.

**Non-Goals:**
- Using anything new in 3.0, including making `DocumentsFacade.UploadAsync` a mutation (stays in TODOS.md).
- Editing archived changes.

## Decisions

**D1. One change, one sweep.** Package IDs, namespaces, the DI call and docs move together, because the build fails between a package swap and a namespace rename (CS0246). Alternative: stage by project. Rejected: nothing compiles in between.

**D2. Rename mechanically, then let the compiler find the rest.** Replace `DotNetQuery` with `Fetchling` in live files and `AddDotNetQuery` with `AddFetchling`; `mise run build` finds anything missed. Prose such as "DotNet Query" is checked by hand.

**D3. Telemetry code needs no edit.** `Program.cs` subscribes with `QueryTelemetry.SourceName`, which becomes `"Fetchling"` on its own. Its comment is reworded.

**D4. Archive stays as written.** Archived designs record the reasoning at the time.

**D5. Fix `.vscode/tasks.json` in passing.** It points at `DotNetQuery.slnx`, which does not exist (the solution is `OfficeRkyv.slnx`). It is a name sweep hit, so it goes here.

**D6. The TODO stays, with its text updated.** "Upload as a DotNet Query mutation" is renamed to Fetchling. It links to `dotnet-query-streaming-issue.md`, which is not in the repo; the dead link is removed.

## Effect on traces, metrics and logs

The source and meter name changes from `DotNetQuery` to `Fetchling`; metrics `dotnetquery.*` become `fetchling.*`; log categories `DotNetQuery.Core.*` become `Fetchling.Core.*`. The `observability` spec does not name them, so it is unchanged. Anything outside the repo that matches the old names must be edited.

## Verification

- Build and the existing unit tests (`UsersFacadeTests`, `WorkspaceFacadeTests`, unit level) cover the namespace and DI changes.
- `ApiTelemetryTests` is a pipeline test of the API and does not cover the Web subscription, so the data-layer spans and metrics are checked by a functional test run (`mise run test`) and by looking for the `Fetchling` source in the Web's exported telemetry.
- The desktop is verified by hand: it starts and a list page renders (see docs/testing.md, by hand).

## Guideline docs to update

`docs/ui.md`, `docs/architecture.md`, `docs/testing.md`, `docs/observability.md`. Wording only; no convention departs from them.

## Risks / Trade-offs

- [External dashboards or alerts match the old names] -> Listed as **BREAKING** in the proposal; nothing in the repo contains such config.
- [A look-alike `DotNetQuery.*` package from another publisher] -> Swap to the `Fetchling.*` IDs, which sit under the reserved prefix.
- [A 3.0 behaviour difference despite the rename-only claim] -> The existing tests and a manual page load on both hosts.
