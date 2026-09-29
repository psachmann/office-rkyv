# CLAUDE.md

## UI localization (`src/OfficeRkyv.Web`)

The Blazor UI sources all static text from `.resx` resources via `IStringLocalizer<T>` — never hardcode user-facing strings directly in `.razor` markup or `@code` blocks.

### Adding a new localized string

1. Find (or create) the resource file for the component that owns the text: `Resources/<path mirroring the component's folder>/<ComponentName>.resx`. E.g. `Components/Pages/Home.razor` → `Resources/Components/Pages/Home.resx`. This is the zero-config convention `ResourceManagerStringLocalizerFactory` follows for `AddLocalization(o => o.ResourcesPath = "Resources")` — no extra plumbing needed, it's just file placement.
2. Add the key/value to that `.resx`.
3. Inject `@inject IStringLocalizer<ComponentName> Localizer` in the component (or the equivalent constructor injection in C#) and reference the string as `@Localizer["Key"]`.
4. If the string needs inline HTML formatting (e.g. a `<strong>` mid-sentence), store the markup in the resource value and render with `@((MarkupString)Localizer["Key"].Value)` — see `Error.razor` for an example. Only do this for our own trusted, app-authored resource strings, never for anything derived from user input.

Only English (`en`) ships today. `OfficeRkyv.Web.Localization.SupportedCultures` is the single source of truth for which cultures are supported — it drives both `RequestLocalizationOptions` and the language switcher on the settings page (`Settings.razor`), so they can never disagree. Adding a second language is: drop in the new culture's `.resx` files (same names, `.{culture}.resx` suffix) and add the culture to `SupportedCultures.All`.

### Culture resolution order

1. An explicit culture cookie (set by the language switcher), if present — always wins.
2. Otherwise, the browser's `Accept-Language` header, matched against `SupportedCultures.All`.
3. Otherwise, `SupportedCultures.Default` (English).

### Why changing language reloads the page

This is a Blazor Server app: each user has one persistent SignalR circuit, and that circuit's culture is fixed for its entire lifetime — SignalR interactions don't re-run the HTTP middleware pipeline that resolves culture. There is no way to hot-swap a live circuit's culture. The language switcher therefore navigates to `GET /culture/set?culture={code}&redirectUri={uri}`, which sets the culture cookie and redirects with `forceLoad: true`, forcing a real browser reload and a fresh circuit with the new culture. Don't try to "fix" this by making the switcher an in-place Blazor event — it can't work without a real navigation.

### Radzen's own built-in text

Radzen ships its own translations for several cultures (not English, which is its built-in default) via satellite resource assemblies. It resolves its own component text (grid/pager/date-picker chrome, etc.) from a `DefaultUICulture` cascading parameter, cascaded from `MainLayout.razor`, falling back to `CultureInfo.CurrentUICulture` if nothing is cascaded. You don't need to do anything extra for Radzen's own strings to follow the app's resolved culture — just don't remove that cascade.
