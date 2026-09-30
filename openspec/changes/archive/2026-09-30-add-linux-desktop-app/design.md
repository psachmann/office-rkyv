# Design

## Context

See proposal.md - Why. The evidence for every desktop decision below is the "Spike results" section of the archived `linux-desktop-app-spike` change; findings are cited as *spike Q1-Q5*.

Current state, as observed in the repo:

- **`OfficeRkyv.Web`** (Web SDK, Blazor Server) holds everything UI:
  - components under `Components/` (layout, `AppTheme`, pages, user dialogs, `WorkspaceErrors`, `CreateUserErrors`);
  - facades, `Appearance`, `AppearanceState` and `WorkspaceKeys` under `Shared/`;
  - `Localization/SupportedCultures`, the `.resx` files under `Resources/`, and `wwwroot/` (`app.css`, `js/appearance.js`).
- **Host-bound pieces:**
  - `App.razor` reads the appearance cookie from `HttpContext` and renders the static `AppTheme`.
  - `Routes.razor` starts `AppearanceState` from that value.
  - `Settings.razor` changes the language by navigating to `/culture/set` with a forced reload.
  - `Program.cs` wires request localization, `/culture/set`, and the four gRPC clients against Aspire's `https://api-svc`.
- **The unit tests** reference the Web project with `Aliases="web"` (both Web and Api have a top-level `Program`), in seven files.
- **`OfficeRkyv.Api`** composes Fisher, Wolverine (`Solo`), gRPC and the error mapping inline in `Program.cs`, together with ServiceDefaults and telemetry. `tests/.../PipelineHost.cs` repeats that composition.
- **No desktop code exists.** The default dev shell carries no GTK libraries.

## Goals / Non-Goals

**Goals:**
- One UI code base for both hosts, with the web behaviour and all existing tests unchanged in substance.
- One API composition for the API server, the desktop and the tests.
- A settings seam that makes the two hosts' very different mechanics (cookies and reload versus a file and in-place changes) invisible to the pages.
- A desktop app that meets the `desktop-app` spec, carrying every workaround the spike found, with the workarounds contained in the desktop project.

**Non-Goals:**
- Packaging, installers and distribution; other operating systems; desktop-only features such as file import.
- Changing the web's user-visible behaviour, or the gRPC contracts.
- Splitting `OfficeRkyv.Api` into a library and a host. The desktop references it as the spike did (spike Q2); that is enough.

## Decisions

### D1. Project layout

```
OfficeRkyv.Contracts  <---------------------------------------------+
        ^                                                            |
OfficeRkyv.UI (Razor class library, new)        OfficeRkyv.Api (Web SDK)
  pages, dialogs, MainLayout, facades,             AddOfficeRkyvApi / MapOfficeRkyvApi (D3)
  IUserSettingsService, Appearance,                      ^           ^
  SupportedCultures, .resx, wwwroot                      |           |
        ^               ^                                |           |
        |               +-----------------+              |           |
OfficeRkyv.Web (Web SDK, thin host)     OfficeRkyv.Desktop (MAUI GTK4, new)
  App.razor, Routes, ReconnectModal,      root component + host page, DesktopApi,
  Error, /culture/set, cookie settings    file settings, GTK glue
  (run by the AppHost, as today)          (run from the desktop dev shell)
```

- `OfficeRkyv.UI` uses `Microsoft.NET.Sdk.Razor`, with root namespace `OfficeRkyv.UI`. It has no `Program`, so the unit tests reference it without an alias.
- Its static assets are served at `_content/OfficeRkyv.UI/…` in both hosts; the spike confirmed referenced libraries' assets resolve in the `BlazorWebView` (spike Q1).
- `OfficeRkyv.Desktop` is a plain `net10.0` exe on `Microsoft.NET.Sdk.Razor` (the spike's working shape). Referencing the two Web SDK projects (UI through Web's packages, and Api) brings in the ASP.NET Core shared framework, so it can host Kestrel (spike Q2).

*Alternative:* keep the UI in `OfficeRkyv.Web` and reference the Web project from the desktop, as the spike did. Rejected, because it drags the web host's `Program`, its assets at the root, and its host-only components into the desktop. The spike needed a copied layout to get past the render mode.

### D2. What moves into `OfficeRkyv.UI`, and what stays

**Moves** (namespaces `OfficeRkyv.Web.*` become `OfficeRkyv.UI.*`; resources keep their folder-mirroring convention, relative to the UI project):
- `Components/Layout/MainLayout.razor` (+ `.css`), `Components/Layout/AppTheme.razor`.
- `Components/Pages/Home`, `Greeter`, `NotFound`, `Settings`, `Users/*` (dialogs, `Workspace`, the two error mappers).
- `Shared/*`, with `AppearanceState` replaced by D4.
- `Localization/SupportedCultures`.
- The matching `Resources/**.resx`, and `wwwroot/app.css` and `wwwroot/js/appearance.js`.

**Stays in `OfficeRkyv.Web`:**
- `App.razor`, `Routes.razor`, `_Imports.razor`.
- `Layout/ReconnectModal` (Blazor Server circuit UI) and `Pages/Error` (uses `HttpContext`), with their resources.
- `Program.cs`, including request localization and `/culture/set`.

**Changes made while moving:**
- `MainLayout`: `<RadzenComponents @rendermode="InteractiveServer" />` becomes `<RadzenComponents />`. The web host's `Routes` is already interactive, so behaviour is unchanged, and the `BlazorWebView` no longer throws (spike Q1).
- `MainLayout` keeps injecting `IHostEnvironment` (for the dev tools in Development); the desktop host registers one (D6).
- The hosts' head markup references `_content/OfficeRkyv.UI/app.css`. The scoped-CSS bundle needs no explicit link: each host's own generated `<Host>.styles.css` already `@import`s the UI library's bundle (checked in the built output for the web host). The JS import becomes `./_content/OfficeRkyv.UI/js/appearance.js`.
- `CLAUDE.md`'s localization section is updated to the new paths: resources for shared components live in `OfficeRkyv.UI/Resources/…`, those for web-only components in `OfficeRkyv.Web/Resources/…`.

The router stays per host. It is a few lines, and each host has different root concerns (initial settings on the web, the rebuild key on the desktop). Both route over `typeof(OfficeRkyv.UI.Components.Layout.MainLayout).Assembly`, with `DefaultLayout` `MainLayout` and `NotFoundPage` `NotFound`.

### D3. API composition as extension methods

In `OfficeRkyv.Api`:
- `IHostApplicationBuilder.AddOfficeRkyvApi(string connectionString, Action<StoreOptions>? configure = null)`. It registers Fisher (connection, `AddAggregateSnapshots`, `ApplyAllDatabaseChangesOnStartup`, `IntegrateWithWolverine`), Wolverine (`ApplicationAssembly`, `Solo`, `AutoApplyTransactions`, `UseApiErrorMapping`) and gRPC.
- `IEndpointRouteBuilder.MapOfficeRkyvApi()`, which maps the Greeter, Users, Archives and Documents services.

Host-specific concerns stay with each host:
- **API server:** ServiceDefaults, telemetry and `TrackWriteLockContention` (through `configure`), and the Aspire connection string.
- **Desktop:** the socket, the data path and the signal-free lifetime (D6).
- **Tests' `PipelineHost`:** a temp file and a loopback port.

`UseWolverine` sits on `IHostBuilder`; the extension reaches it through `builder` as the host type allows (`WebApplicationBuilder.Host`). If the generic form proves awkward, it takes a `WebApplicationBuilder`, which all three callers use.

### D4. `IUserSettingsService`

In `OfficeRkyv.UI.Settings`:

```csharp
public sealed record UserSettings(AppearancePreference Appearance, CultureInfo Culture);

public interface IUserSettingsService
{
    UserSettings Current { get; }            // synchronous: pages render from it immediately
    IObservable<UserSettings> Settings { get; }  // replays Current on subscribe, then every change
    Task SetAppearanceAsync(AppearancePreference appearance);
    Task SetCultureAsync(CultureInfo culture);
}
```

The service owns persistence, the current value, change notification, and applying a change, because the two hosts apply changes in fundamentally different ways (spike Q4, Q5). Writes are async, so a future server-backed implementation would not change callers.

**Change notification is System.Reactive, not .NET events** (the codebase's convention: DotNetQuery's queries and mutations are observables, and components compose them with `CompositeDisposable`).
- Both implementations back `Settings` with a `BehaviorSubject<UserSettings>`, and `Current` is its value. Emitting only real changes is the implementation's job: a set to the current value emits nothing.
- Unlike the mutation state that once needed `Skip(1)`, replaying the current value is wanted here: a subscriber always starts from the settings in force.
- Consumers narrow what they need, for example `Settings.Select(s => s.Culture).DistinctUntilChanged()`. They keep the subscription in their disposables and marshal to the renderer with `InvokeAsync`, because an emission may arrive from another thread (for example a GTK signal).
- The subjects are completed when their service is disposed.
- `OfficeRkyv.UI` references `System.Reactive` explicitly (7.0.0, the version DotNetQuery already brings in), pinned in the root `Directory.Packages.props`, instead of relying on it transitively.

- **Pages** (`Settings`) depend only on the interface:
  - The language drop-down's value is `SupportedCultures.Match(Current.Culture).Name`, which fixes the empty drop-down on `en-US` (spike Q5).
  - A change calls `SetCultureAsync`. The page no longer knows about `/culture/set`.
- **Web implementation** (`OfficeRkyv.Web`, scoped per circuit): today's behaviour moved behind the interface.
  - `App.razor` reads the appearance cookie as now, and `Routes` passes it to `Initialize(appearance, CultureInfo.CurrentUICulture)`.
  - `SetAppearanceAsync` calls the shared `applyAppearance` in `appearance.js`, which flips the theme links' media and writes the cookie, as today.
  - `SetCultureAsync` navigates to `/culture/set` with the current page as the redirect, with `forceLoad`, as today.
- **Desktop implementation** (`OfficeRkyv.Desktop`, a singleton, because the GTK and culture state are process-wide): D6.

`Appearance` (parsing, cookie values, stylesheets) stays in the UI library. The web uses all of it; the desktop uses `Parse`/`ToCookieValue`-style names for its file.

*Alternative:* one shared "apply" in JS for both hosts. Rejected, because on the desktop, applying goes through GTK (D6); the spike showed that is what makes first paint and live changes right.

### D5. Culture matching in one place

`SupportedCultures.Match(CultureInfo requested)` returns the supported culture that equals the request, else the first supported culture that is the request's parent (walking up the `Parent` chain), else `Default`.
- The desktop resolves its initial culture as: the stored choice, if supported; else `Match(CultureInfo.CurrentUICulture)`, which .NET derives from `LANG`/`LC_*`.
- The web keeps using the request localization middleware, which already falls back to parents.

### D6. The desktop host

**Startup order** in `Program.Main` and `GtkMauiApplication`:
1. Load the settings file, `$XDG_CONFIG_HOME/office-rkyv/settings.json` (`{ "appearance": "system|light|dark", "culture": "en" }`). A missing file means the defaults; an unreadable or invalid file is logged and treated as missing.
2. Apply the culture: `CultureInfo.DefaultThreadCurrent(UI)Culture` and the current thread's.
3. Run the GTK application. In the backend's `OnStarted`, which only runs in the primary instance:
   - apply the appearance to GTK (below);
   - start the API (`DesktopApi.Start`);
   - then the window with the `BlazorWebView` opens.

**API** (`DesktopApi`, free of GTK, so it is unit-testable):
- Composes with `AddOfficeRkyvApi` against `$XDG_DATA_HOME/office-rkyv/office-rkyv.db`.
- Kestrel listens with `ListenUnixSocket` on `$XDG_RUNTIME_DIR/office-rkyv/api.sock` over HTTP/2. The directory is created with mode 0700, so only the user can connect.
- It registers `IHostLifetime` = `NoSignalLifetime`, so the host does not take the signals (spike Q2).
- `ClaimSocket` runs before binding: a socket that answers means another instance, so refuse; one that refuses is stale, so delete it (spike Q2).
- The four gRPC clients are registered with `AddWolverineGrpcClient` at `http://localhost` and `ConfigurePrimaryHttpMessageHandler` with a `SocketsHttpHandler` that dials the socket (spike Q2).

**Single instance:**
- `ApplicationId` `io.github.psachmann.OfficeRkyv` makes `Gtk.Application` unique over D-Bus, so a second launch hands off to the primary: the second process prints its settings line and exits, starting no API (observed in task 5.5).
- **The hand-off is not quiet in the primary.** The backend's private `OnActivate` runs the whole startup again on every activation: `CreateMauiApp()`, a new `IApplication`, and a new window (so a second launch opens a second window in the running instance, which shares its API). Two findings followed, both handled in `Program`:
  - `EnsureApiStarted` is guarded, so the API still starts once.
  - `CreateMauiApp` returns one cached `MauiApp`. Without that, each activation built its own `Application`, `Application.Current` was the newest, and SIGTERM closed only that one's windows, leaving the process running.
- `ClaimSocket` is the backstop, for example if D-Bus is unavailable.

**Shutdown:**
- `PosixSignalRegistration` for SIGTERM and SIGINT (a callback API, not a .NET event, so it stays as it is) dispatches to the UI thread and calls `Application.CloseWindow` for every window; `Application.Quit()` does not end the loop in this backend (spike Q2).
- When `Run` returns, the API is stopped and disposed, which removes the socket (spike Q2).

**Appearance** (`GtkAppearance`, the spike's bridge generalized; spike Q4):
- WebKitGTK derives `prefers-color-scheme` only from `Gtk.Settings.GtkApplicationPreferDarkTheme`.
  - **System:** mirror `GtkInterfaceColorScheme == Dark` into it now and on every `notify::gtk-interface-color-scheme`.
  - **Light/Dark:** stop mirroring and set it to false or true.
- Expressed as one reactive pipeline, with no hand-written event handlers:
  - GirCore's `Gtk.Settings.OnNotify` is a .NET event, so it is wrapped once with `Observable.FromEventPattern`, filtered to that property, and turned into `IObservable<bool> desktopPrefersDark`, starting with the current value.
  - `CombineLatest(settings.Settings.Select(s => s.Appearance), desktopPrefersDark, (choice, desktopDark) => choice switch { Light => false, Dark => true, _ => desktopDark })`, then `DistinctUntilChanged()`, observed on the GTK main loop, sets `GtkApplicationPreferDarkTheme`.
  - The subscription lives as long as the application.
- The host page, `wwwroot/index.html`, carries:
  - the two theme links with the System media queries, permanently;
  - an inline `html` background, light by default and dark under `prefers-color-scheme: dark`.
- So every mode is right from the first frame, and switching needs no JS. The spike verified System and the startup background. That a fixed choice overrides the desktop through the same setting was inferred there, and task 5.3 verifies it first.

**Language** (spike Q5): `SetCultureAsync` writes the file, sets the culture as at startup, and emits the new settings. The desktop root component subscribes to `Settings.Select(s => s.Culture).DistinctUntilChanged().Skip(1)`; here the `Skip(1)` drops the replayed culture the root already renders with. It rebuilds its tree through a `@key` on the `Router`, which switches all text, including Radzen's own; component state on the page is lost, as with the web's reload. (The spike used a static .NET event for this; the observable replaces it.)

**Rest of the host:**
- Registers `IHostEnvironment` (`DOTNET_ENVIRONMENT`, defaulting to Production), Radzen, localization, DotNetQuery (`Ssr`), the facades and the settings service.
- Console logging, and in Development the WebView developer tools with WebKit's console on stdout (spike Q1).
- The project contains no `.razor.css`. If it ever needs one, add the spike's `MauiCss` removal (spike Q1).
- `[assembly: SupportedOSPlatform("linux")]`, so CA1416 does not fail the Release build.

### D7. Build, packages and dev shell

- Both new projects go into `OfficeRkyv.slnx`.
- The three preview packages are pinned exactly in the root `Directory.Packages.props`: Gtk4 and BlazorWebView, plus Essentials only if a task needs it.
- The desktop builds without native libraries, so `dotnet build`, `dotnet test` and the pre-push hook keep working in the default shell. Only running it needs `nix develop .#desktop`, a second dev shell adding `gtk4`, `webkitgtk_6_0` and `glib` to `LD_LIBRARY_PATH` (spike Q3; GLib must be listed explicitly, or the app exits silently).
- A `just desktop` recipe runs it in that shell. The AppHost does not change.

### D8. Testing

- Existing unit and functional tests stay and must pass after every step. They change only in namespaces and aliases (UI) and in the new composition (`PipelineHost`).
- **New unit tests:**
  - `SupportedCultures.Match`;
  - the web settings service against a fake JS runtime and `NavigationManager`;
  - the desktop settings store (missing, valid and broken files, round trip) and the culture resolution;
  - `DesktopApi`: the API over the socket, the stale-socket claim, a second-instance refusal, and the socket removed on stop.
- The functional tests keep covering the shared UI through the web host.
- The GTK-bound behaviour (window, appearance, signals, quitting, D-Bus single instance) is verified by a person on a graphical session, with the app's own stdout as evidence, as in the spike. Playwright cannot drive WebKitGTK.

## Risks / Trade-offs

- [The experimental backend changes or breaks between previews] → Exact version pins. The workarounds are isolated in the desktop project, and the UI and API do not depend on MAUI, so a backend switch (for example to Photino) touches only `OfficeRkyv.Desktop`.
- [Moving the UI breaks the web subtly (asset paths, resource names, scoped CSS)] → The move is its own task group, verified by the full unit suite and the Playwright functional tests before anything else changes. Resource names follow from the RCL's root namespace and the folder convention, and the existing "every key exists" tests catch mismatches.
- [A fixed Light or Dark choice through `prefer-dark` does not override the desktop as inferred] → Verified first in the desktop group (task 5.3). Fallback: flip the links' media from the host page for fixed choices, as the web does, accepting the web's first-paint approach.
- [GApplication hand-off does not behave as expected with this backend (for example, a second `OnStarted` or a second window)] → `ClaimSocket` still refuses a second API, which satisfies the spec. The observed behaviour is recorded, and the real single-instance mechanism is kept to what works.
- [The desktop's data is the user's only copy; the event schema will evolve] → Out of scope here, but noted: the desktop turns event versioning from practice into a need. It is the first long-lived store; the dev database is still thrown away per AppHost run.
- [The first gRPC call after start takes ~1.3 s (spike Q2)] → Acceptable for a desktop start; the window shows while it happens.

## Migration Plan

Each step leaves the repo building and green:
1. API composition (D3).
2. UI library (D2), web unchanged.
3. Settings service (D4, D5) with the web implementation.
4. The desktop host (D6), dev shell and docs (D7).

Rollback is per step with git. The web app is never broken between steps.

## Open Questions

- The application id `io.github.psachmann.OfficeRkyv` is a reasonable default; any reverse-DNS id works, and it can change before packaging without affecting anything else.
