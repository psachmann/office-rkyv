# Design

## Context

`OfficeRkyv.Web` is a Blazor Server app (`InteractiveServer` render mode, one persistent SignalR circuit per user) with no localization infrastructure today — see proposal.md for motivation. The relevant constraint this design has to work around: a Blazor Server circuit's culture is captured once when the circuit starts and stays fixed for the circuit's lifetime, because SignalR interactions don't re-run ASP.NET Core's HTTP middleware pipeline. Changing language therefore requires a real browser navigation, not just a Blazor event.

Two facts from inspecting the installed `Radzen.Blazor` 11.3.2 package (`~/.nuget/packages/radzen.blazor/11.3.2/lib/net9.0/`) materially shape the Radzen-related decisions below:
- It ships satellite resource assemblies for `de`, `es`, `fr`, `it`, `ja` (English is its built-in, unlocalized default) — its own component text already localizes itself once the ambient culture is correct.
- `Radzen.RadzenComponent.UICulture` resolves its own text via a cascaded `DefaultUICulture` parameter first, falling back to `CultureInfo.CurrentUICulture` only if nothing is cascaded. `Radzen.ILocalizer` exists as an override hook ("Implement this interface ... to override the built-in English strings") but is not required for this change.

## Goals / Non-Goals

**Goals:**
- Idiomatic ASP.NET Core resource-based localization (`IStringLocalizer` + `.resx`) for `OfficeRkyv.Web`'s own UI text.
- Culture resolution order: cookie override → browser `Accept-Language` → default (`en`).
- A working, persistent language switcher in the header, visible today even with one language.
- Radzen's own built-in component text follows the resolved culture.
- Every currently-hardcoded string in `App.razor`, `MainLayout.razor`, `ReconnectModal.razor`, `Home.razor`, `Error.razor`, `NotFound.razor` moved to resources.

**Non-Goals:**
- Shipping any language other than English (resource *structure* is ready for more; content is not).
- Localizing `OfficeRkyv.Api`, gRPC messages, or any server-side text outside `OfficeRkyv.Web`.
- Localizing user-authored content (document names, uploaded file contents) — this change is UI chrome only.
- A persisted per-user/per-account language preference — cookie only, per the "start lightweight" decision.
- A custom `Radzen.ILocalizer` implementation — not needed since Radzen already covers de/es/fr/it/ja and defaults to English; it's a documented extension point for later, not built now.

## Decisions

**1. Localization stack: built-in `Microsoft.Extensions.Localization` + `.resx`, no third-party library.**
`AddLocalization(o => o.ResourcesPath = "Resources")` plus `IStringLocalizer<T>` injected per component. Alternatives considered: a custom JSON-based localizer (rejected — reinvents pluralization and tooling `.resx` already provides for free); a client-side/localStorage i18n library (rejected — built for Blazor WASM's model, doesn't fit Blazor Server's per-circuit, server-rendered-first model, and duplicates what ASP.NET Core's own localization middleware already does).

**2. Resource layout mirrors the component folder structure under `Resources/`.**
E.g. `Home.razor` → `Resources/Components/Pages/Home.resx` (backs `IStringLocalizer<Home>`), `MainLayout.razor` → `Resources/Components/Layout/MainLayout.resx`, etc. This is the zero-configuration convention `ResourceManagerStringLocalizerFactory` already follows for `ResourcesPath = "Resources"` — no custom resource-lookup code, and Visual Studio's `.resx` editor works unmodified. It keeps the "resource lives next to the component that uses it" locality that the user's existing empty `Resources/` folder implied. Rejected alternative: one flat `SharedResources.resx`. Simpler at today's 6 files, but doesn't scale once pages stop sharing vocabulary, and loses that locality.

**3. Culture resolution order via `RequestLocalizationOptions.RequestCultureProviders`.**
Order: `CookieRequestCultureProvider`, then `AcceptLanguageHeaderRequestCultureProvider` (first match wins). `DefaultRequestCulture = en`, `SupportedCultures = SupportedUICultures = [en]`. With only `en` supported, the Accept-Language provider will in practice always resolve to `en` — but the precedence and provider wiring are fully in place, so adding a second supported culture later is a one-line config change, not a redesign.

**4. Language change = forced full-page navigation, not a Blazor event.**
A minimal non-interactive endpoint, e.g. `GET /culture/set?culture={code}&redirectUri={uri}`, writes the culture cookie (`CookieRequestCultureProvider.MakeCookieValue`) and redirects back to `redirectUri`. This re-runs `RequestLocalizationMiddleware` and starts a fresh circuit with the new culture. The header switcher calls `NavigationManager.NavigateTo(url, forceLoad: true)` rather than mutating in-circuit state, because nothing else actually changes a live circuit's culture.

**5. Header switcher is a Radzen dropdown in `MainLayout`'s `RadzenHeader`, bound to a static supported-culture list.**
Only `en` is in that list today; the control is still shown (per proposal) so its presence and styling exist before a second language ships, instead of being added later as a header layout change.

**6. Radzen's built-in text: cascade `DefaultUICulture` explicitly from `MainLayout`, no other Radzen-specific work.**
Since `RadzenComponent.UICulture` already falls back to `CultureInfo.CurrentUICulture`, this cascade isn't strictly required for correctness today, but it makes the dependency explicit rather than implicit in ambient thread state flowing through Blazor Server's async/SignalR context — cheaper to add now than to debug later if that ambient flow ever misbehaves. No `Radzen.ILocalizer` implementation is written in this change.

**7. `App.razor`'s `<html lang="en">` becomes `<html lang="@CultureInfo.CurrentCulture.Name">`.**
So the static shell's declared language matches the resolved culture from first paint, not just the interactively-rendered content.

## Risks / Trade-offs

- **Full page reload on every language change** (Blazor Server can't hot-swap a live circuit's culture) → Mitigation: this is inherent to the platform, not a workaround to reconsider; language changes are rare interactions, and the switcher explicitly opts into `forceLoad: true` so this is a deliberate navigation, not a bug.
- **Accept-Language inference has no observable non-English outcome yet**, since only `en` ships → Mitigation: test the provider wiring and precedence directly (cookie beats header; header present with no cookie still resolves through the provider chain to the supported default), rather than asserting a non-English render that can't exist yet.
- **Resource file count grows with one `.resx` per component** → Mitigation: acceptable at the app's current size (6 files); revisit consolidation only if it becomes unwieldy.
- **Radzen's built-in localization behavior is tied to the installed `Radzen.Blazor` 11.3.2` satellite resources** → Mitigation: none needed now; noted as an implicit dependency if that package is ever changed.

## Open Questions

- Exact culture cookie name and expiration are implementation details (e.g. ASP.NET Core's default `CookieRequestCultureProvider` cookie, given a multi-month/year expiration so the choice survives across sessions) that don't affect the approach, specs, or task breakdown — left to implementation.
