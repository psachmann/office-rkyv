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

## Signing in

The desktop offers the same landing page and sign-in as the web, with the same two seams ([ui.md](ui.md#signing-in)), implemented in memory.

- **`DesktopSignIn`** is one singleton for the running app and the `AuthenticationStateProvider` of every window and every gRPC client. Signing in or out sets the principal and raises `AuthenticationStateChanged`. Nothing is written to disk, so every start is signed out. It is app-wide and not per window: a second launch opens a second window on the running instance, and one person runs the app.
- **`DesktopSignInService`** is the `ISignInService`, one per window (it needs that window's `NavigationManager`). A sign-in only changes the state: the landing page, where it is made, sends a signed-in visitor on to `/workspace`. A sign-out also navigates to `/`, because it can be made from the settings page, which needs no user.
- **The key.** `DesktopHost` makes 32 random bytes at every start and hands them to `DesktopApi.StartAsync` (the API's JwtBearer) and to `DesktopApi.AddSocketClients` (the token issuer). Nothing outside the process knows it.
- **Call credentials over the socket.** gRPC attaches call credentials only over TLS, so the clients set `UnsafeUseInsecureChannelCallCredentials`. That is safe here because the socket lives in the owner-only runtime directory; the web host keeps TLS. The token is still required, so the API has one code path (checked by `DesktopApiTests`).
- **Two log lines**, `[LoggerMessage]` at Information with the user id only, as the evidence of the by-hand check: `Signed in as user {UserId}` and `Signed out`.

## Saving a downloaded file

`DesktopFileSaver` implements `IFileSaver` (see [ui.md](ui.md#files-upload-and-download)). It opens GTK's save dialog (`Gtk.FileDialog`) with the file name proposed and writes the stream to the path chosen; a dismissed dialog writes nothing and is not an error. File content itself is kept in `blobs/` in the data directory, beside the database.

- **The dialog is the experimental backend's.** If it cannot be shown at all, the file is saved into the user's Downloads directory under `DownloadPaths.Unique` (an existing file is never overwritten: `name (1).ext`) and the page tells the user where, as it does after the dialog. The fallback is logged as a Warning.
- **The dialog needs a GSettings schema, and its absence kills the process.** GTK's file dialog reads `org.gtk.gtk4.Settings.FileChooser`. Where no schema is installed (on NixOS, nothing puts GTK's on `XDG_DATA_DIRS` by itself) GLib does not throw, it aborts: `GLib-GIO-ERROR: No GSettings schemas are installed on the system` and exit code 134, which no `catch` sees. Two things answer it, and neither should be removed alone: the `desktop` dev shell adds GTK's and `gsettings-desktop-schemas`' schema directories to `XDG_DATA_DIRS` (`flake.nix`), and `DesktopFileSaver` looks the schema up before opening the dialog and takes the Downloads fallback, with a Warning, when it is missing.
- **Not verified automatically.** Playwright cannot drive WebKitGTK, so a save is checked by hand; the name rules are unit-tested (`DownloadPaths`).

## Logging

The desktop host has no generic host for its own startup, so `DesktopHost.Initialize` creates one console logger (category `OfficeRkyv.Desktop`) and hands it to the parts that log. Outside Development it logs warnings and above; in Development it logs information too, which is the evidence a manual check relies on. The messages follow the convention in [observability.md](observability.md#logging).

## Decided in

- [linux-desktop-app-spike](../openspec/changes/archive/2026-09-30-linux-desktop-app-spike/design.md) (Spike results): what was tried on a real desktop, and what each workaround answers.
- [add-linux-desktop-app](../openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md) (D1, D3, D6, D8): the project layout, the API in the process, the host, and why the desktop is checked by hand.
- [add-document-file-upload](../openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D12): saving a downloaded file as the host's job, and the fallback.
- [add-sign-in](../openspec/changes/archive/2026-10-17-add-sign-in/design.md) (D7): the app-wide in-memory sign-in, and the key made at start.
