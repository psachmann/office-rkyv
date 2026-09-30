using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OfficeRkyv.UI.Localization;

namespace OfficeRkyv.UnitTests;

public class CultureResolutionTests
{
    private static async Task<string?> ResolveCultureAsync(HttpContext context)
    {
        var middleware = new RequestLocalizationMiddleware(
            _ => Task.CompletedTask,
            Options.Create(SupportedCultures.CreateRequestLocalizationOptions()),
            NullLoggerFactory.Instance
        );

        await middleware.Invoke(context);

        return context.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.Name;
    }

    [Test]
    public async Task NoCookieAndUnsupportedAcceptLanguage_ResolvesToEnglish()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "fr-FR,fr;q=0.9";

        var resolved = await ResolveCultureAsync(context);

        await Assert.That(resolved).IsEqualTo("en");
    }

    [Test]
    public async Task CookieOverridesConflictingAcceptLanguage()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "fr-FR,fr;q=0.9";
        context.Request.Headers.Cookie =
            $"{CookieRequestCultureProvider.DefaultCookieName}="
            + Uri.EscapeDataString(CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("en")));

        var resolved = await ResolveCultureAsync(context);

        await Assert.That(resolved).IsEqualTo("en");
    }

    [Test]
    public async Task NoCookieNoHeader_ResolvesToDefault()
    {
        var context = new DefaultHttpContext();

        var resolved = await ResolveCultureAsync(context);

        await Assert.That(resolved).IsEqualTo(SupportedCultures.Default.Name);
    }
}
