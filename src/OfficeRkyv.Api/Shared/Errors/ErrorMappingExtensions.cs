using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Grpc;

namespace OfficeRkyv.Api.Shared.Errors;

public static class ErrorMappingExtensions
{
    /// <summary>
    /// The one place failures become gRPC statuses, for every service the API hosts (see the
    /// api-error-mapping spec). Handlers keep throwing what they throw and know nothing about gRPC.
    /// Shared by the app and the tests so both run exactly the same pipeline.
    /// </summary>
    public static WolverineOptions UseApiErrorMapping(this WolverineOptions options)
    {
        // Runs each command's IValidator<T> before its handler; a failure throws with every violated rule.
        options.UseFluentValidation();

        // Registers Wolverine's validation provider first, which hands validation failures to our adapter.
        options.UseGrpcRichErrorDetails();
        options.Services.AddSingleton<IValidationFailureAdapter, ValidationFailureAdapter>();

        // Registered after the validation provider, and catches everything: providers run in registration
        // order and Wolverine's own default table (which exposes exception type names) is never reached.
        options.Services.AddScoped<IGrpcStatusDetailsProvider, ErrorStatusProvider>();

        options.Services.AddWolverineGrpc();

        // Wolverine's interceptor logs every mapped failure as a warning, expected refusals included.
        // ErrorStatusProvider logs the unexpected ones itself, at Error.
        options.Services.Configure<LoggerFilterOptions>(filters =>
            filters.AddFilter(typeof(WolverineGrpcExceptionInterceptor).FullName, LogLevel.Error)
        );

        return options;
    }
}
