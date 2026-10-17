using System.Text;
using DotNetQuery.Core;
using DotNetQuery.Core.Observability;
using DotNetQuery.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UI;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Files;
using OfficeRkyv.UI.Localization;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.Web.Auth;
using OfficeRkyv.Web.Components;
using OfficeRkyv.Web.Files;
using OfficeRkyv.Web.Settings;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Wolverine.Grpc.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddOfficeRkyvUi();
builder.Services.AddDotNetQuery(options =>
{
    options.ExecutionMode = QueryExecutionMode.Ssr;
    options.StaleTime = TimeSpan.Zero; // data is immediately stale
    options.CacheTime = TimeSpan.FromMinutes(10); // cache entries live 10 minutes after last subscriber
    options.RefetchInterval = TimeSpan.FromSeconds(30); // automatically refetch every 30 seconds
});

// DotNetQuery uses one name for both its ActivitySource and its Meter (see the observability spec).
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(QueryTelemetry.SourceName));
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(QueryTelemetry.SourceName));

// One instance per circuit, reachable as the concrete type (Routes starts it) and as the interface the pages use.
builder.Services.AddScoped<WebUserSettingsService>();
builder.Services.AddScoped<IUserSettingsService>(services => services.GetRequiredService<WebUserSettingsService>());

// Hands a downloaded file to the browser.
builder.Services.AddScoped<IFileSaver, WebFileSaver>();

// Who is signed in: a cookie, set by the two /account endpoints. A circuit cannot set a cookie, so the
// sign-in service asks for a one-time ticket and navigates to the endpoint that redeems it.
builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "office-rkyv.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);

        // A page that needs a user is challenged by the authorization middleware before Blazor renders it. The
        // answer is the landing page, where a visitor signs in, and not the scheme's default /Account/Login.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect("/");

            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SignInTickets>();
builder.Services.AddScoped<ISignInService, WebSignInService>();

// The key the API validates caller tokens with; the AppHost hands both processes the same one.
var callerTokenKey = Encoding.UTF8.GetBytes(
    builder.Configuration[CallerTokens.SigningKeySetting]
        ?? throw new InvalidProgramException($"No caller token signing key provided ({CallerTokens.SigningKeySetting})")
);

// "api-svc" is resolved by Aspire service discovery (see AppHost). gRPC only accepts plain
// http/https here; the "https+http" form throws "No address resolver configured".
// Wolverine's typed clients wrap AddGrpcClient; a failed call surfaces as a typed .NET exception with
// the original RpcException (and its status details) as InnerException. Every call carries the signed-in
// user's caller token, issued per call from the circuit's authentication state.
AddApiClient<UsersService.UsersServiceClient>(builder.Services, callerTokenKey);
AddApiClient<ArchivesService.ArchivesServiceClient>(builder.Services, callerTokenKey);
AddApiClient<DocumentsService.DocumentsServiceClient>(builder.Services, callerTokenKey);

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseRequestLocalization(SupportedCultures.CreateRequestLocalizationOptions());

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAccountEndpoints();

app.MapGet(
    "/culture/set",
    (string culture, string? redirectUri, HttpContext context) =>
    {
        if (!SupportedCultures.IsSupported(culture))
        {
            return Results.BadRequest();
        }

        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );

        var target =
            redirectUri is { Length: > 0 }
            && redirectUri.StartsWith('/')
            && !redirectUri.StartsWith("//", StringComparison.Ordinal)
            && !redirectUri.StartsWith("/\\", StringComparison.Ordinal)
                ? redirectUri
                : "/";

        return Results.LocalRedirect(target);
    }
);

app.MapStaticAssets();

// The pages live in the UI library; without its assembly here the first (server-rendered) request would not
// find them and answer 404.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(OfficeRkyv.UI.Components.Layout.MainLayout).Assembly);

app.Run();

static void AddApiClient<TClient>(IServiceCollection services, byte[] callerTokenKey)
    where TClient : class
{
    var builder = services.AddWolverineGrpcClient<TClient>(options => options.Address = new Uri("https://api-svc"));

    // Proto-first clients, like all of these, always have an HTTP client builder to configure.
    CallerTokenCredentials.AddTo(
        builder.HttpClientBuilder
            ?? throw new InvalidOperationException(
                $"The gRPC client {typeof(TClient).Name} has no HTTP client builder."
            ),
        callerTokenKey
    );
}
