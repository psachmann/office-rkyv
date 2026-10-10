# Observability

How this project produces traces, metrics and logs, and what must never end up in them. Read it before adding a span, a metric or a log message, before adding a library that emits telemetry, and before writing a plan that has to say what its effect on telemetry is.

The required behaviour for traces and metrics is the `observability` spec in `openspec/specs`. This doc is the convention behind it.

## Traces and metrics

- **Subscribe, don't instrument.** The app has no `ActivitySource` and no `Meter` of its own. The spans and metrics come from the libraries that do the work: ASP.NET Core and `HttpClient` (through service defaults), Wolverine (message handling), Fisher (the event store) and DotNetQuery (the UI's data layer). A new signal is first looked for in what those already emit.
- **Service defaults build the providers.** `OfficeRkyv.ServiceDefaults` sets up the tracer and meter providers with the ASP.NET Core, `HttpClient` and runtime instrumentation. Health-check requests are left out of the traces.
- **Each service adds its own library sources.** A service subscribes with `ConfigureOpenTelemetryTracerProvider` and `ConfigureOpenTelemetryMeterProvider`, which add to the providers that service defaults built. Service defaults never names a library it does not reference.
  - **Api:** `AddApiTelemetry` in `Shared/Telemetry/TelemetryExtensions.cs` adds the sources `Wolverine` and `Fisher`, and the meters `Wolverine*` and Fisher's (`OpenTelemetryOptions.MeterName`). The API server and the telemetry test both call it.
  - **Web:** `Program.cs` adds `QueryTelemetry.SourceName`, which DotNetQuery uses for both its source and its meter.
- **Use the library's constant where it has one.** Where it has none (Fisher's source, Wolverine's source and meter), the name is a string with a comment saying why. Wolverine's meter is matched by the wildcard `Wolverine*`, because its name carries a suffix chosen at startup.
- **Options that belong to the store are set on the store.** Fisher's write-lock metrics are turned on where the API server calls `AddOfficeRkyvApi` (`options.OpenTelemetry.TrackWriteLockContention()`), not in the telemetry extension. `TrackConnections` stays off: Fisher rejects it on SQLite.
- **Export is optional.** Signals are exported only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, which the Aspire AppHost does. Without it every service starts and works.
- **The desktop app exports nothing.** It does not use service defaults; it has a console logger only.

### Verifying

A string name that is wrong fails silently, so the subscription is tested, at the pipeline level: `ApiTelemetryTests` builds a host with the same `AddApiTelemetry` the app calls, adds the in-memory exporter, makes real gRPC calls and checks the spans and metrics that come out, including that no event-store span carries the email address that was sent. A change to the API's telemetry extends that test.

The web side (the query spans, and one trace id from the UI through the API) is checked by hand in the Aspire dashboard. Its subscription is two lines guarded by a compile-time constant.

## Logging

- **Log at the edges.** A log line is for the life of a host (started, listening, stopped, a setting applied) and for a failure nobody expected. The story of one request is in its trace, so handlers do not log.
- **Expected refusals are not logged.** A validation failure, a missing resource and a refused business rule are answers, not faults. `UseApiErrorMapping` filters Wolverine's gRPC interceptor to Error for that reason, and `ErrorStatusProvider` logs only what nothing else claimed.
- **Levels:**
  - Information: the life of a host.
  - Warning: a fault the app continues after, such as an unreadable settings file, or unfinished upload files found and removed from the blob store's `tmp` folder at start.
  - Error: an unexpected failure, with the exception; for a download whose content is missing from the blob store, the document's id.
- **Every message is a `[LoggerMessage]` method.** Never call `logger.LogInformation(...)` and its siblings directly. The analyzer rule CA1848 is a warning (`.editorconfig`), and a warning fails the Release build that the pre-push hook runs.

### Writing a log message

Declare the message as a partial method on the type that logs, which becomes `partial`. Keep the methods together at the end of the type.

```csharp
internal sealed partial class ErrorStatusProvider(ILogger<ErrorStatusProvider> logger) : IGrpcStatusDetailsProvider
{
    private RpcStatus Unexpected(Exception exception, ServerCallContext context)
    {
        LogUnhandledException(logger, exception, context.Method);

        return Plain(StatusCode.Internal, "An unexpected error occurred.");
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception in gRPC call {Method}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method);
}
```

- The method is `private static partial void Log<What>(ILogger logger, ...)`, with an explicit `Level` and `Message`.
- Placeholders in the message match the parameter names, ignoring case. An `Exception` parameter is attached to the entry and needs no placeholder.
- No hand-assigned event ids. The method name is the event name, and the generator derives the numeric id from it, so renaming a method changes the id shown in the console (`OfficeRkyv.Desktop[1777324192]`).
- The message stays on the type that emits it. There is no shared class of log messages per project.
- A Razor component declares its log methods in a code-behind file (`Main.razor.cs`): the generator does not see code written in a `.razor` file, and the analyzer may not either.
- A type that takes an optional logger keeps `logger ?? NullLogger.Instance`, since a generated method needs a logger.

## What must never appear in a span or a log

- Document contents.
- The name of a document's file and the title of a document, which say as much as the content does.
- Personal data, such as an email address or a display name.
- Bound parameter values of a database statement.
- An exception's message in anything sent to a client. It may be logged with the exception at Error; it never goes into a gRPC status (see [api.md](api.md#failures)).

Identify things by id (a document by its id, file content by its hash). The spec requires the first three for event-store spans, and the telemetry test checks it; for logs it is a rule a reviewer checks.

## Decided in

- [add-library-telemetry](../openspec/changes/archive/2026-09-28-add-library-telemetry/design.md) (D1 to D5): per-service registration, constants and strings, the API's one extension, the write-lock metrics, the in-memory exporter test.
- [structured-error-codes](../openspec/changes/archive/2026-09-28-structured-error-codes/design.md) (D1): unexpected failures logged at Error, expected refusals not logged.
- [add-document-file-upload](../openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (Telemetry and logs): no new source or meter for file transfer, and the file name and title kept out of spans and logs.
