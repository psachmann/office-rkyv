# Design

## Context

`ServiceDefaults.ConfigureOpenTelemetry()` sets up the OpenTelemetry SDK for both services. It subscribes to ASP.NET Core, HttpClient, runtime and `ApplicationName`, and adds the OTLP exporter only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Of the libraries this change covers, only two publish a name constant:

| Library     | Process | ActivitySource            | Meter                                         | Public constant                                   |
|-------------|---------|---------------------------|-----------------------------------------------|---------------------------------------------------|
| DotNetQuery | Web     | `DotNetQuery`             | `DotNetQuery`                                 | `QueryTelemetry.SourceName` (both)                |
| Wolverine   | Api     | `Wolverine`               | built at runtime (`Wolverine...`)             | none                                              |
| Fisher      | Api     | `Fisher` (internal class) | `Fisher`                                      | `Fisher.Services.OpenTelemetryOptions.MeterName`  |

Web-to-Api propagation already works: Grpc.Net.Client runs on HttpClient, so client spans and the W3C `traceparent` header come from the existing HttpClient instrumentation.

`UserPipelineTests` builds its own host by copying the `AddFisher` and `UseWolverine` setup from `Program.cs` instead of running `Program.cs`. A test that also copied the telemetry wiring would prove nothing about the real wiring.

## Goals / Non-Goals

**Goals:**
- Each service owns the registration for the libraries it references, next to where it configures them.
- The Api's telemetry wiring can be called from a test host, so the test checks the same code `Program.cs` runs.

**Non-Goals:**
- Changing ServiceDefaults' behavior or the rule for when OTLP export is enabled.
- Sampling, filtering, or reducing trace volume.
- Custom spans or metrics of our own.

## Decisions

### D1: Per-service registration, not central names in ServiceDefaults
Each service adds its library sources and meters with `IServiceCollection.ConfigureOpenTelemetryTracerProvider(...)` and `ConfigureOpenTelemetryMeterProvider(...)`. These add to the provider ServiceDefaults already builds rather than creating a second one.

*Alternative considered:* list every name as a string in `ConfigureOpenTelemetry()`. That needs no new references, but it can't cover Fisher's write-lock instruments, which must be turned on in `StoreOptions` in the Api. It also leaves ServiceDefaults naming libraries it doesn't depend on. Because part of the Api's configuration has to live in the Api anyway, all of it does.

### D2: Use constants where the library exposes them, strings where it doesn't
- Web: `QueryTelemetry.SourceName` for both the source and the meter.
- Api: `OpenTelemetryOptions.MeterName` for Fisher's meter. The literal `"Fisher"` for its source, because Fisher's `FisherTracing` class is internal. The literal `"Wolverine"` for Wolverine's source.
- Wolverine's meter: the wildcard `"Wolverine*"`. Wolverine builds the meter name at startup and logs it ("Exporting Open Telemetry metrics from Wolverine with name {Name}"). The wildcard stays correct whatever that name turns out to be, and survives a service-name change.

The literal strings fail silently if they're wrong. The test (D5) is what catches that.

### D3: The Api's telemetry lives in one extension in the Api project
Add an extension in the Api project (e.g. `Shared/Telemetry/ApiTelemetry.cs`) that registers the Wolverine and Fisher tracer and meter subscriptions. `Program.cs` calls it, and so does the test host. The OpenTelemetry types reach the Api transitively through its ServiceDefaults project reference, so no new package reference is needed.

The Web app's registration is a two-line call in `Program.cs`, next to `AddDotNetQuery`. It doesn't get an extension: nothing else calls it, and the `QueryTelemetry.SourceName` constant already makes a typo impossible.

### D4: Fisher's contention metrics are enabled in the store configuration
In the `AddFisher` lambda in `Program.cs`, call `options.OpenTelemetry.TrackWriteLockContention()`. It turns on `fisher.write_lock.wait` (a histogram in milliseconds, recorded around every `BEGIN IMMEDIATE`, contended or not) and `fisher.write_lock.retries` together.

`TrackConnections` stays off. It isn't just unhelpful: Fisher rejects it outright, because connection pooling is handled by Microsoft.Data.Sqlite for the whole process and can't be attributed to one store.

### D5: Verify with an in-memory exporter against the real wiring
Add a test in `OfficeRkyv.UnitTests` whose host is built the same way as `UserPipelineTests` (a real Fisher store on a temporary SQLite file, the real Wolverine pipeline, the real gRPC service), with three differences:
- it calls the D3 extension;
- it turns on `TrackWriteLockContention()` the same way `Program.cs` does;
- it adds `AddInMemoryExporter` for both traces and metrics.

The test makes real gRPC calls to `CreateUser` and `ListUsers`, then checks:
- Wolverine handler spans are children of the gRPC server span;
- Fisher spans are descendants of the handler spans;
- no Fisher span attribute contains the email address that was sent;
- after a forced meter flush, metrics from both `Fisher` (including `fisher.write_lock.wait`) and a `Wolverine*` meter are present.

This needs the test-only `OpenTelemetry.Exporter.InMemory` package, pinned to the same 1.19.x line as the other OpenTelemetry packages.

The DotNetQuery side and the end-to-end Web-to-Api trace id are checked by hand in the Aspire dashboard. Automating a Blazor-circuit trace assertion would cost much more than it's worth for two subscriptions guarded by a compile-time constant.

## Risks / Trade-offs

- [Wolverine's actual source name differs from `"Wolverine"`, or a future version renames it] -> The D5 test fails loudly. Fix the string there and in the extension.
- [Trace volume: each open circuit's 30-second refetch produces a trace spanning both services] -> Accepted for development. If it gets noisy, add a sampler or a DotNetQuery filter in a follow-up change.
- [Wolverine's durability agent emits background traces (e.g. `wolverine_node_assignments`)] -> Solo mode keeps this minimal. If it shows up in the dashboard, `Durability.NodeAssignmentHealthCheckTracingEnabled = false` turns it off.
- [The test's host setup drifts from `Program.cs` for the Fisher and Wolverine options, as `UserPipelineTests` already does] -> Only the telemetry wiring is shared (D3). The store and pipeline duplication is existing technical debt and stays out of scope.
- [Metric assertions are timing-sensitive] -> Call `MeterProvider.ForceFlush()` before asserting instead of waiting on the export interval.

## Migration Plan

Additive only. Deploying adds new spans and metrics. Rolling back means removing the registrations. No data or configuration migration is involved.
