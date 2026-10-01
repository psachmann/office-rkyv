# Desktop app

What the Linux desktop host (`src/OfficeRkyv.Desktop`) is, and the workarounds it carries for the experimental backend it is built on. Read it before changing anything in that project, and before changing something in the shared UI that the desktop also runs (the layout, the settings, the theme links).

## What it is

A standalone Linux app: the API (`AddOfficeRkyvApi`, the same composition as the API server) and the shared UI (`OfficeRkyv.UI`) in one process, in a GTK4 window through .NET MAUI's experimental GTK4 backend with a `BlazorWebView`. Run it with `just desktop` (enters `nix develop .#desktop`, which adds GTK4, WebKitGTK and GLib; building needs none of them). How to run it and where it keeps data and settings is in `README.md`. The web app stays as the Aspire/Playwright harness; Playwright cannot drive WebKitGTK, so the desktop is checked by hand, with the app's stdout (Development logs the page's console too) as evidence.

Appearance and language go through `IUserSettingsService`, which has one implementation per host; see [ui.md](ui.md#user-settings).

## Workarounds for the experimental backend

Each is verified and explained in the archived spike, `openspec/changes/archive/2026-09-30-linux-desktop-app-spike/design.md` (Spike results), and `openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md` (D6). Don't remove one without re-checking it on a real desktop:

- **No render mode on `RadzenComponents`** in `MainLayout`: a `BlazorWebView` throws on it. The web host doesn't need it either.
- **Appearance (`GtkAppearance`):** WebKitGTK derives `prefers-color-scheme` only from `gtk-application-prefer-dark-theme`, which COSMIC never sets (it publishes through the XDG portal, read by GTK as `gtk-interface-color-scheme`). So System mirrors the latter into the former, and Light or Dark fix it. The backend's `ApplicationHandler.MapAppTheme` writes that setting itself (always light) while it builds the app and each window, so it is started in `App.CreateWindow` and watches the setting and puts it back. The host page (`wwwroot/index.html`) has both theme links keyed on `prefers-color-scheme` and an inline default background, so the first frame is right.
- **Environment:** `IHostEnvironment` is registered by hand (`DesktopHostEnvironment`, from `DOTNET_ENVIRONMENT`), as the shared layout asks for it.
- **Quitting:** `Application.Quit()` does not end the backend's loop; closing the last window does. SIGTERM and SIGINT (`SignalHandling`) therefore close every window, and the API is stopped after `Run` returns, which removes its socket.
- **Activation:** the backend's private `OnActivate` runs the whole startup on every activation, and a second launch activates the running instance. `Program.CreateMauiApp` therefore starts the API once and returns one cached `MauiApp`; a second launch opens a second window on the running instance, starts no second API, and exits.
- **A crash leaves the socket behind:** `DesktopApi.ClaimSocket` connects to it first, deletes it if nothing answers, and refuses to start if something does.
- **No `.razor.css` in the desktop project** (it breaks `BlazorWebView`'s static assets); if one is ever needed, see the spike's `MauiCss` removal. Scoped CSS of the UI library is fine: the host's `<Host>.styles.css` imports it.

## Logging

The desktop host has no generic host for its own startup, so `DesktopHost.Initialize` creates one console logger (category `OfficeRkyv.Desktop`) and hands it to the parts that log. Outside Development it logs warnings and above; in Development it logs information too, which is the evidence a manual check relies on. The messages follow the convention in [observability.md](observability.md#logging).

## Decided in

- [linux-desktop-app-spike](../openspec/changes/archive/2026-09-30-linux-desktop-app-spike/design.md) (Spike results): what was tried on a real desktop, and what each workaround answers.
- [add-linux-desktop-app](../openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md) (D1, D3, D6, D8): the project layout, the API in the process, the host, and why the desktop is checked by hand.
