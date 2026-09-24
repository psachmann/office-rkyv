using DotNetQuery.Core;
using DotNetQuery.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Localization;
using OfficeRkyv.Contracts;
using OfficeRkyv.Web.Components;
using OfficeRkyv.Web.Localization;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRadzenComponents();
builder.Services.AddDotNetQuery(options =>
{
    options.ExecutionMode = QueryExecutionMode.Ssr;
    options.StaleTime = TimeSpan.Zero; // data is immediately stale
    options.CacheTime = TimeSpan.FromMinutes(10); // cache entries live 10 minutes after last subscriber
    options.RefetchInterval = TimeSpan.FromSeconds(30); // automatically refetch every 30 seconds
});
builder.Services.AddLocalization(options => options.ResourcesPath = SupportedCultures.ResourcesPath);

// "api-svc" is resolved by Aspire service discovery (see AppHost). gRPC only accepts plain
// http/https here; the "https+http" form throws "No address resolver configured".
builder.Services.AddGrpcClient<Greeter.GreeterClient>(options => options.Address = new Uri("https://api-svc"));

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

app.UseAntiforgery();

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
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
