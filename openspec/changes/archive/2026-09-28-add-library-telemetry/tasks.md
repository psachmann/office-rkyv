# Tasks

## 1. Api telemetry

- [x] 1.1 Add the Api telemetry extension (design D3): subscribe tracing to the `"Wolverine"` and `"Fisher"` sources, and metrics to `"Wolverine*"` and `OpenTelemetryOptions.MeterName`, using `ConfigureOpenTelemetryTracerProvider` and `ConfigureOpenTelemetryMeterProvider`. Verify with `dotnet build src/OfficeRkyv.Api`, which should succeed with no new package reference in the Api csproj.
- [x] 1.2 Call the extension from `src/OfficeRkyv.Api/Program.cs` and add `options.OpenTelemetry.TrackWriteLockContention()` to the `AddFisher` lambda (D4). Verify that `dotnet build` succeeds and the existing `UserPipelineTests` still pass.

## 2. Web telemetry

- [x] 2.1 In `src/OfficeRkyv.Web/Program.cs`, next to `AddDotNetQuery`, subscribe tracing and metrics to `QueryTelemetry.SourceName` (D1/D2). Verify that `dotnet build src/OfficeRkyv.Web` succeeds.

## 3. Automated verification

- [x] 3.1 Add `OpenTelemetry.Exporter.InMemory` (1.19.x, matching the other OpenTelemetry packages) to `Directory.Packages.props` and to `tests/OfficeRkyv.UnitTests`. Verify that `dotnet restore` succeeds.
- [x] 3.2 Add a telemetry test host modeled on `UserPipelineTests` that calls the Api telemetry extension, turns on `TrackWriteLockContention()`, and exports traces and metrics to in-memory lists (D5). Verify that the host starts in the test's setup.
- [x] 3.3 Test the observability spec's "gRPC call produces a handler span under the request span" and "command's store work appears under its handler" scenarios: after real `CreateUser` and `ListUsers` gRPC calls, a Wolverine span is a child of the server span and a Fisher span is a descendant of the handler span. Verify that the test passes.
- [x] 3.4 Test "No user data leaks into store spans": no Fisher span attribute contains the email address sent in `CreateUser`. Verify that the test passes.
- [x] 3.5 Test the metrics scenarios: after `ForceFlush()`, the exported metrics include `fisher.write_lock.wait` and at least one metric from a meter whose name starts with `Wolverine`. Verify that the test passes. If the Wolverine source-name assumption in 1.1 is wrong, this test or 3.3 fails. Correct the name, not the test.

## 4. End-to-end check

> Skipped by the user on 2026-09-28: not run, so the boxes stay unticked. The unit suite (92 tests) passes; the functional tests and the Aspire dashboard check were not performed.

- [ ] 4.1 Run the full test suite (`dotnet test`) and confirm everything passes, including the existing functional tests.
- [ ] 4.2 In the running app, check in the Aspire dashboard: open the users page and confirm that one trace goes DotNetQuery fetch -> gRPC client -> Api server -> Wolverine handler -> Fisher. Wait for a 30-second refetch and confirm it starts its own trace with the same shape. Confirm that the `DotNetQuery`, `Wolverine...` and `Fisher` metrics appear. Per the user's setup, use the AppHost that's already running under `dotnet watch`; don't start a second one.
