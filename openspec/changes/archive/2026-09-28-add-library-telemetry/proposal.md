# Proposal

## Why

Wolverine, Fisher and DotNetQuery already emit OpenTelemetry spans and metrics, but neither service subscribes to them. As a result, a trace in the Aspire dashboard stops at the gRPC request: it never shows which handler ran, what the event store did, or which client-side query started the call. On a single SQLite file, waiting for the write lock is the likeliest first bottleneck, and nothing measures it today.

## What Changes

- The Api subscribes to Wolverine's and Fisher's traces and metrics, so handler execution and event-store work show up under the gRPC request span that triggered them.
- The Api turns on Fisher's opt-in write-lock contention metrics (time spent waiting for the SQLite write lock, and retries).
- The Web app subscribes to DotNetQuery's traces and metrics, so query fetches and mutations show up as spans that contain the outgoing gRPC call, along with cache and duration metrics.
- Each service registers the telemetry for the libraries it uses. ServiceDefaults keeps only the telemetry every service shares (ASP.NET Core, HttpClient, runtime, OTLP export).
- An automated test proves that the Api's real telemetry registration produces Wolverine and Fisher spans and metrics.

Out of scope: `[Audit]` tags on messages, custom Fisher change-set counters, trace sampling, and extra gRPC client instrumentation.

## Capabilities

### New Capabilities

- `observability`: which traces and metrics the services emit and how spans correlate across the Web -> Api -> handler -> store path.

### Modified Capabilities

None.

## Impact

- `src/OfficeRkyv.Api`: telemetry registration for Wolverine and Fisher, plus the Fisher store's OpenTelemetry options in `Program.cs`.
- `src/OfficeRkyv.Web`: telemetry registration for DotNetQuery in `Program.cs`.
- `src/OfficeRkyv.ServiceDefaults`: no behavior change. Library-specific names are deliberately kept out of it.
- `tests/OfficeRkyv.UnitTests`: a new telemetry test. This adds the `OpenTelemetry.Exporter.InMemory` package (test-only) to `Directory.Packages.props`.
- Runtime: more spans and metrics sent to the OTLP endpoint. The Web app's 30-second refetch interval produces a full trace per open circuit every 30 seconds.
