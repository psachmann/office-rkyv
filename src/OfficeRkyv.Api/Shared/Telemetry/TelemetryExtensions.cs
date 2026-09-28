using Fisher.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OfficeRkyv.Api.Shared.Telemetry;

public static class TelemetryExtensions
{
    // Fisher's ActivitySource is an internal type, so its name has no constant to reference.
    private const string FisherSourceName = "Fisher";

    // Wolverine builds its source and meter names at startup and exposes no constant for either.
    private const string WolverineSourceName = "Wolverine";

    // A wildcard, because the meter name carries a runtime suffix (see the startup log line
    // "Exporting Open Telemetry metrics from Wolverine with name ...").
    private const string WolverineMeterPattern = "Wolverine*";

    /// <summary>
    /// Subscribes the API's telemetry providers to what Wolverine and Fisher emit (see the observability
    /// spec). Adds to the providers ServiceDefaults builds rather than creating its own, and is shared by
    /// the app and the tests so both subscribe to exactly the same names.
    /// </summary>
    /// <remarks>
    /// Fisher's write-lock metrics are opt-in on the store, not here: see <c>AddFisher</c> in Program.cs.
    /// </remarks>
    public static IServiceCollection AddApiTelemetry(this IServiceCollection services)
    {
        services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddSource(WolverineSourceName, FisherSourceName)
        );
        services.ConfigureOpenTelemetryMeterProvider(metrics =>
            metrics.AddMeter(WolverineMeterPattern, OpenTelemetryOptions.MeterName)
        );

        return services;
    }
}
