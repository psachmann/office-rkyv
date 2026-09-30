# CLAUDE.md

## UI localization (`src/OfficeRkyv.UI`, `src/OfficeRkyv.Web`)

The Blazor UI (a shared Razor class library, `src/OfficeRkyv.UI`, used by the web host `src/OfficeRkyv.Web`) sources all static text from `.resx` resources via `IStringLocalizer<T>` — never hardcode user-facing strings directly in `.razor` markup or `@code` blocks.

### Adding a new localized string

1. Find (or create) the resource file for the component that owns the text, in the project that owns the component: `Resources/<path mirroring the component's folder>/<ComponentName>.resx`. E.g. `src/OfficeRkyv.UI/Components/Pages/Home.razor` → `src/OfficeRkyv.UI/Resources/Components/Pages/Home.resx`. Web-host-only components (`ReconnectModal`, `Error`) keep theirs in `src/OfficeRkyv.Web/Resources/…`. This is the zero-config convention `ResourceManagerStringLocalizerFactory` follows for `AddLocalization(o => o.ResourcesPath = "Resources")` — no extra plumbing needed, it's just file placement.
2. Add the key/value to that `.resx`.
3. Inject `@inject IStringLocalizer<ComponentName> Localizer` in the component (or the equivalent constructor injection in C#) and reference the string as `@Localizer["Key"]`.
4. If the string needs inline HTML formatting (e.g. a `<strong>` mid-sentence), store the markup in the resource value and render with `@((MarkupString)Localizer["Key"].Value)` — see `Error.razor` for an example. Only do this for our own trusted, app-authored resource strings, never for anything derived from user input.

Only English (`en`) ships today. `OfficeRkyv.UI.Localization.SupportedCultures` is the single source of truth for which cultures are supported — it drives both `RequestLocalizationOptions` and the language switcher on the settings page (`Settings.razor`), so they can never disagree. Adding a second language is: drop in the new culture's `.resx` files (same names, `.{culture}.resx` suffix) and add the culture to `SupportedCultures.All`.

### Culture resolution order (web host)

1. An explicit culture cookie (set by the language switcher), if present — always wins.
2. Otherwise, the browser's `Accept-Language` header, matched against `SupportedCultures.All`.
3. Otherwise, `SupportedCultures.Default` (English).

### Why changing language reloads the page (web host)

This is a Blazor Server app: each user has one persistent SignalR circuit, and that circuit's culture is fixed for its entire lifetime — SignalR interactions don't re-run the HTTP middleware pipeline that resolves culture. There is no way to hot-swap a live circuit's culture. The language switcher therefore navigates to `GET /culture/set?culture={code}&redirectUri={uri}`, which sets the culture cookie and redirects with `forceLoad: true`, forcing a real browser reload and a fresh circuit with the new culture. Don't try to "fix" this by making the switcher an in-place Blazor event — it can't work without a real navigation.

### Radzen's own built-in text

Radzen ships its own translations for several cultures (not English, which is its built-in default) via satellite resource assemblies. It resolves its own component text (grid/pager/date-picker chrome, etc.) from a `DefaultUICulture` cascading parameter, cascaded from `MainLayout.razor`, falling back to `CultureInfo.CurrentUICulture` if nothing is cascaded. You don't need to do anything extra for Radzen's own strings to follow the app's resolved culture — just don't remove that cascade.

## Desktop app (`src/OfficeRkyv.Desktop`)

A standalone Linux app: the API (`AddOfficeRkyvApi`, the same composition as the API server) and the shared UI (`OfficeRkyv.UI`) in one process, in a GTK4 window through .NET MAUI's experimental GTK4 backend with a `BlazorWebView`. Run it with `just desktop` (enters `nix develop .#desktop`, which adds GTK4, WebKitGTK and GLib; building needs none of them). How to run it and where it keeps data and settings is in `README.md`. The web app stays as the Aspire/Playwright harness; Playwright cannot drive WebKitGTK, so the desktop is checked by hand, with the app's stdout (Development logs the page's console too) as evidence.

### `IUserSettingsService`: one interface, one implementation per host

The shared UI reads and changes appearance and language only through `IUserSettingsService` (`Current`, the replaying `IObservable<UserSettings> Settings`, `SetAppearanceAsync`, `SetCultureAsync`). The change notification is System.Reactive, not .NET events: subscribe to `Settings`, and keep the subscription in the component's disposables.

- **Web** (`WebUserSettingsService`): appearance is a cookie plus `appearance.js`; language is the culture cookie and a forced reload through `/culture/set` (see the localization section above).
- **Desktop** (`DesktopUserSettingsService`): both are kept in `settings.json`, and a change is saved, put in force and emitted. Appearance reaches the window through `GtkAppearance`; a language change is applied **in place**: the service sets the process culture, and the root component (`Main.razor`) rebuilds its router through a `@key` when the culture stream changes. The web reloads because a Blazor Server circuit's culture is fixed; a `BlazorWebView` has no such limit, so the desktop doesn't pay for a reload. Component state on the page is lost either way.

### Workarounds for the experimental backend

Each is verified and explained in the archived spike, `openspec/changes/archive/2026-09-30-linux-desktop-app-spike/design.md` (Spike results), and `openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md` (D6). Don't remove one without re-checking it on a real desktop:

- **No render mode on `RadzenComponents`** in `MainLayout`: a `BlazorWebView` throws on it. The web host doesn't need it either.
- **Appearance (`GtkAppearance`):** WebKitGTK derives `prefers-color-scheme` only from `gtk-application-prefer-dark-theme`, which COSMIC never sets (it publishes through the XDG portal, read by GTK as `gtk-interface-color-scheme`). So System mirrors the latter into the former, and Light or Dark fix it. The backend's `ApplicationHandler.MapAppTheme` writes that setting itself (always light) while it builds the app and each window, so it is started in `App.CreateWindow` and watches the setting and puts it back. The host page (`wwwroot/index.html`) has both theme links keyed on `prefers-color-scheme` and an inline default background, so the first frame is right.
- **Environment:** `IHostEnvironment` is registered by hand (`DesktopHostEnvironment`, from `DOTNET_ENVIRONMENT`), as the shared layout asks for it.
- **Quitting:** `Application.Quit()` does not end the backend's loop; closing the last window does. SIGTERM and SIGINT (`SignalHandling`) therefore close every window, and the API is stopped after `Run` returns, which removes its socket.
- **Activation:** the backend's private `OnActivate` runs the whole startup on every activation, and a second launch activates the running instance. `Program.CreateMauiApp` therefore starts the API once and returns one cached `MauiApp`; a second launch opens a second window on the running instance, starts no second API, and exits.
- **A crash leaves the socket behind:** `DesktopApi.ClaimSocket` connects to it first, deletes it if nothing answers, and refuses to start if something does.
- **No `.razor.css` in the desktop project** (it breaks `BlazorWebView`'s static assets); if one is ever needed, see the spike's `MauiCss` removal. Scoped CSS of the UI library is fine: the host's `<Host>.styles.css` imports it.
