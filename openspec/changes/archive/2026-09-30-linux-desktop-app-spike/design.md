# Design

## Context

See proposal.md - Why. What the spike has to work with, as observed in the repo and from the backend's documentation:

- **The backend.** MAUI's Linux GTK4 backend (`dotnet/maui-labs`, `platforms/Linux.Gtk4`) is experimental and unsupported. Packages: `Microsoft.Maui.Platforms.Linux.Gtk4` and `Microsoft.Maui.Platforms.Linux.Gtk4.BlazorWebView`; the newest is `0.1.0-preview.12.26421.1` (an older `Platform.Maui.Linux.Gtk4` id is deprecated). It needs .NET 10, GTK 4.12 or newer and WebKitGTK 6. There is no Linux target framework: the app is a separate "head" project inheriting `GtkMauiApplication`. The `maui-linux-gtk4` template (`-b` adds Blazor) is known to reference package versions that do not exist (dotnet/maui-labs#519); the workaround is to pin the versions by hand.
- **The UI.** `OfficeRkyv.Web` is a Blazor Server app: Radzen with the Material theme, `.resx` localization, DotNetQuery facades over gRPC clients, an ES-module JS file (`wwwroot/js/appearance.js`), and a static `AppTheme` that links both theme stylesheets with media queries. Its host-specific parts (`App.razor`, cookies, `/culture/set`, `ReconnectModal`) are not expected to work in a `BlazorWebView`.
- **The API.** `OfficeRkyv.Api` composes Fisher (SQLite), Wolverine in `Solo` mode, gRPC and the error mapping in its `Program.cs`. The unit tests' `PipelineHost` repeats that composition against a temp file, over a loopback socket, so it is known to work inside another process.
- **The repo's build.** The root `Directory.Build.props` (net10.0, analyzers, warnings as errors in Release) and `Directory.Packages.props` (central package versions) apply to everything below the repo root. `OfficeRkyv.slnx` lists its projects explicitly, and the root build, the tests and the pre-push hook all run through it.
- **The machine.** NixOS. The dev shell in `flake.nix` carries no GTK, WebKitGTK or GObject-introspection packages, only ICU on `LD_LIBRARY_PATH`. The shell reports a Wayland session (`DISPLAY=:1`, `WAYLAND_DISPLAY=wayland-1`); there is no Xvfb, Weston or Cage.

## Goals / Non-Goals

**Goals:**
- A clear answer to each of the five questions, with the evidence that supports it.
- A go / go-with-workarounds / no-go recommendation, and the concrete facts a follow-up change needs: versions, Nix packages, environment variables and the snippets that mattered.

**Non-Goals:**
- Any product code, or anything reusable. The spike is deleted afterwards; only its findings stay.
- Polish, packaging, tests, or handling every Radzen component. It has to answer the questions, not ship.
- Changing the specs, `flake.nix`, the root props or the solution file.

## Decisions

### D1. The spike lives in `spikes/linux-desktop/`, isolated from the repo's build

The spike has its own `Directory.Build.props` and `Directory.Packages.props`. MSBuild uses the nearest one and stops, so the repo's root props and central package versions do not reach it, and preview packages can be pinned there without touching the root `Directory.Packages.props`. It is not added to `OfficeRkyv.slnx`, so `dotnet build`, `dotnet test` and the pre-push hook never see it, and they need no GTK libraries.

*Alternative:* a project under `src/` in the solution. Rejected, because every build, and the pre-push hook, would then require GTK and preview packages for a throwaway.
*Alternative:* a project outside the repo. Rejected, because it could not reference the repo's projects as easily, and the results would be detached from it.

### D2. Use the real components, by referencing the Web assembly

The spike's `BlazorWebView` uses a `Router` over the `OfficeRkyv.Web` assembly, so it renders the real Settings, Home and Workspace pages, the real layout, and the real facades. That is a far better test of question 1 than a hand-built page, and the friction of referencing an application project (static asset paths under `_content`, the Web project's own `Program`, resources) is itself a finding for the shared-UI-library change.

*Fallback:* if referencing the Web assembly proves unworkable, use a scratch page built from the same Radzen components the real pages use (data grid, dialog, context menu, select bar, drop-down), and record why the real components could not be used.

The spike supplies its own host page (`wwwroot/index.html`) and root components. It does not reuse `App.razor`, `AppTheme`'s server-side cookie read, `ReconnectModal`, or `/culture/set`.

### D3. The API runs in the same process, composed the way `PipelineHost` does it

The desktop process starts a `WebApplication` with the same Fisher, Wolverine (`Solo`), gRPC and error-mapping composition and the four gRPC services, referencing the `OfficeRkyv.Api` project. It listens on a unix socket, with the database file under the XDG data directory (a temp path is fine for the spike). This is a third copy of that composition, which is acceptable for a throwaway and is the strongest argument for the composition extension that the follow-up change will make.

### D4. gRPC over a unix socket, with the real clients

Kestrel listens on a socket path (`ListenUnixSocket`, HTTP/2). The four gRPC clients are registered the way `Program.cs` registers them (`AddWolverineGrpcClient`), but with a `SocketsHttpHandler` whose `ConnectCallback` dials the socket. If `AddWolverineGrpcClient` does not expose the handler, that is a finding. The check is end to end: the real facades create a user, an archive and a document, and a refused request (a blank title) reaches the UI as the localized `WorkspaceErrors` message.

*Alternative:* call the Wolverine bus directly from the facades. Rejected for the spike, because it bypasses exactly the seam (contracts, error mapping, `WorkspaceErrors`) that the design intends to keep.

### D5. Results are reported by the app and by a person

The spike writes what it can measure to stdout: a self-test page calls back into C# with results (which theme applies, `matchMedia` values, the thread and culture in use, socket round-trips). What needs an eye (glyphs and layout, a flash of the wrong theme at startup, a dialog opening properly) is looked at by a person on the graphical session, and the observation is recorded. The apply step asks for that look rather than guessing.

### D6. Nix: an ad hoc shell in the spike directory

The GTK and WebKitGTK libraries come from a throwaway `shell.nix` (or flake) inside `spikes/linux-desktop/`, not from the repo's `flake.nix`. The spike starts from the candidate packages (`gtk4`, `webkitgtk_6_0`, `gobject-introspection`, `glib-networking`, and whatever GirCore fails to find), and records the minimal set and the environment variables that make it work (for example the typelib path and `LD_LIBRARY_PATH`). The follow-up change turns that into the real dev-shell change.

### D7. Pass criteria and the decision rule

| # | Question | Passes when |
|---|---|---|
| 1 | UI renders and works | The real pages render with the Material theme and icon font; a dialog, the context menu, the select bar and the drop-down work; the ES-module import resolves; localized text shows. |
| 2 | API in process over a socket | The real facades complete a create-and-list round trip over the socket; a validation failure shows the localized message; closing the window stops the host, removes the socket file and leaves no stray SQLite journal. Also observe what a second instance does (socket path, database lock). |
| 3 | Nix shell | A clean shell with the recorded packages and variables starts the app on the developer's session, with no manual library fiddling. |
| 4 | System appearance | With the desktop set to dark and to light, `prefers-color-scheme` reports it inside the view; a live change of the desktop setting is followed with no reload; the startup flash is absent, or a workaround (default background, the media-query links in the host page) removes it. |
| 5 | In-process language change | With a throwaway two-language resource, changing the culture and re-rendering the root shows the new language without restarting; the thread and culture that `IStringLocalizer` sees are recorded, along with Radzen's `DefaultUICulture` cascade. |

**Decision rule:**
- **Go:** questions 1 to 3 pass, and 4 and 5 pass or have a known workaround.
- **Go with workarounds:** the workarounds are small and understood, and are written into the follow-up change.
- **No-go for MAUI GTK4:** any of 1 to 3 fails with no workaround. Then, time-boxed, try Photino.Blazor on the failing question or questions only, and record whether it clears them.

The recommendation also names the follow-up changes the results suggest (the shared UI library, the composition extension and `IUserSettingsService`, then the desktop host), and any that the results change.

### D8. The spike is deleted; its findings stay

The results are written into this design document in a "Spike results" section, with the short snippets a follow-up needs, and then `spikes/` is removed. This mirrors how the earlier guard spike was handled: the throwaway code goes, the answers stay in the change's artifacts.

## Risks / Trade-offs

- [Preview packages change or break between builds] → Pin `0.1.0-preview.12.26421.1` in the spike's own `Directory.Packages.props`, and record the exact versions that worked.
- [The template references package versions that do not exist] → Pin by hand, per the known issue; do not rely on the template's version wildcards.
- [Referencing an application project (the Web assembly) from another host is awkward, especially static assets] → Fall back to a scratch page (D2) and record what went wrong, since it informs the shared-library change.
- [Some checks need a person: the shell cannot see the screen] → State this up front (D5); the tasks that need a look ask for it, and unattended checks report to stdout.
- [WebKitGTK behaviour differs by desktop, GPU and session (Wayland or X11)] → Record the session and desktop used; try `GDK_BACKEND=x11` only if a Wayland-specific problem shows up.
- [A spike that drifts into building the product] → The non-goals and the deletion task are the guard; nothing under `src/`, `tests/` or the root files is changed, and the final `git status` proves it.
- [The result is "no-go", and the time is spent] → That is the purpose of the spike, and the fallback (Photino.Blazor) is time-boxed to the failing questions.

## Spike results

Run on 2026-09-29 on NixOS, COSMIC 1.8 (cosmic-comp, Wayland, Xwayland `:1`), .NET SDK 10.0.401. Things a person had to judge were checked by the developer on the screen; everything else was measured by the app and logged to stdout.

### Decision: go, with workarounds

Questions 1 to 3 pass, each with small, understood workarounds; questions 4 and 5 pass with known workarounds. Under the D7 rule this is **go with workarounds**. The Photino.Blazor fallback (task 8.1) was not needed.

| # | Question | Result |
|---|---|---|
| 1 | UI renders and works | **Pass, with workarounds.** The real Settings, Home and Workspace pages, layout and facades run in `BlazorWebView`. The developer drove the whole flow (create user, open workspace, create archive and document, "File into...", delete archive with confirm dialog, document back in the inbox, whitespace title refused with the localized message). Dialogs, context menu, data grid, select bar and drop-down behaved normally. |
| 2 | API in process, over a unix socket | **Pass, with workarounds.** Real API, real gRPC clients and facades over a unix socket. Clean shutdown removes the socket and leaves only the database file. A second instance is refused. |
| 3 | Nix shell | **Pass.** Three runtime libraries on `LD_LIBRARY_PATH`, no typelibs needed. |
| 4 | System appearance | **Workaround needed.** On COSMIC, WebKitGTK does not see the desktop's dark setting at all, neither at start nor live. A 20-line bridge fixes both. With a default background in the host page there is no startup flash. |
| 5 | In-process language change | **Pass.** Setting the culture on the UI thread and re-rendering switches both our text and Radzen's own text, without a reload. |

### Versions that worked

- `Microsoft.Maui.Platforms.Linux.Gtk4`, `.Essentials`, `.BlazorWebView`: `0.1.0-preview.12.26421.1` (pulls GirCore `0.7.0`, `Microsoft.AspNetCore.Components.WebView.Maui` `10.0.41`, `Microsoft.AspNetCore.Components.WebView` `10.0.3`).
- The template (`maui-linux-gtk4 -b`, same version) writes `Version="0.6.0-*"`, which does not exist (NU1102). Pin by hand. With `-b` it generates no Blazor code at all, only the package reference.
- nixpkgs `44a91898084f46797b5fac650c7e8c9ac38c43d4` (the repo's `flake.lock`): GTK `4.22.4`, WebKitGTK `2.52.6` (ABI 6.0), GLib `2.88.3`.

### Question 3: what NixOS needs

```nix
runtimeLibs = with pkgs; [ gtk4 webkitgtk_6_0 glib icu ];  # icu is .NET's own
LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath runtimeLibs;
```

- Without GTK: a clear error ("GTK 4 was not found. Please install GTK 4.12 or newer").
- **Without `glib` on the path: the app exits immediately with exit code 0 and no output at all.** GLib is a dependency of GTK in the store, but GirCore loads it by soname, so it must be on the path explicitly.
- `gobject-introspection` is not needed: GirCore uses generated P/Invoke bindings, not typelibs.
- The first window was not noticed on screen (COSMIC placed it out of view); a Wayland protocol trace showed it mapped and focused exactly like the stock `gtk4-demo`. A `VK_SUBOPTIMAL_KHR` warning after resizes is harmless.

### Question 1: the UI in the view

Wiring (none of it generated): `Sdk="Microsoft.NET.Sdk.Razor"`; `AddBlazorWebView()` plus `AddLinuxGtk4BlazorWebView()`; register the GTK `BlazorWebViewHandler` by its full name, because it clashes with MAUI's own; a host page with `_framework/blazor.webview.js` and `autostart="false"`; `<Content Update="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />`.

Findings, each with its fix:
- **Render mode.** `MainLayout.razor`'s `<RadzenComponents @rendermode="InteractiveServer" />` makes the WebView throw ("the current platform does not support the render mode"). It is redundant in the web host, where `Routes` is already interactive. **Fix in the shared library: drop the attribute.** It is the only render mode outside `App.razor`.
- **`IHostEnvironment`.** `MainLayout` injects it (for the dev tools); a MAUI host does not register it. The desktop host must register one, or the layout must stop depending on it.
- **Static assets.** Served from `app://localhost/`. Assets of referenced projects and packages do resolve through the runtime static-web-assets manifest (Radzen CSS, JS and icon font all loaded). Because `OfficeRkyv.Web` is an *application*, its own assets are served at the root (`app.css`, `js/appearance.js`, `OfficeRkyv.Web.styles.css`). As a class library they move to `_content/<Library>/`, so the host pages and the `import("./js/appearance.js")` path must change with the extraction.
- **Scoped CSS in the desktop project is silently dropped.** MAUI's `Microsoft.Maui.Controls.Globs.props` claims every `**/*.css` as `MauiCss`, leaving the Razor SDK nothing to scope. Fix, or keep all components in the shared library:
  ```xml
  <MauiCss Remove="**/*.razor.css" />
  <ScopedCssInput Include="**/*.razor.css" Exclude="bin/**;obj/**" />
  ```
- **No diagnostics by default.** Add console logging (`Microsoft.Extensions.Logging.Console`, `AddBlazorWebViewDeveloperTools`). The WebKit console can be sent to stdout: the platform view is a `Gtk.Box` containing a `WebKit.WebView`, whose `GetSettings().EnableWriteConsoleMessagesToStdout = true`.
- **`/culture/set` breaks the view.** It is a forced navigation to a web-only endpoint: the view reloads the host page, Blazor does not start again, and the page stays at "Loading..." ("JS object instance with ID 1 does not exist"). Expected; question 5 is the replacement.

### Question 2: the API in the process

- The composition of `PipelineHost` works unchanged in the GTK process; everything it needs is public. Referencing the two Web-SDK projects brings in the ASP.NET Core shared framework, so a Razor-SDK exe can host Kestrel.
- Kestrel `ListenUnixSocket($XDG_RUNTIME_DIR/…sock)` with HTTP/2. The first call takes about 1.3 s (JIT and Wolverine code generation); later calls are fast.
- The Web project's clients needed no workaround, because the builder exposes the `IHttpClientBuilder`:
  ```csharp
  services
      .AddWolverineGrpcClient<TClient>(o => o.Address = new Uri("http://localhost"))  // names the authority only
      .HttpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
      {
          ConnectCallback = async (_, ct) =>
          {
              var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
              await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
              return new NetworkStream(socket, ownsSocket: true);
          },
      });
  ```
- **Signals.** The API host's console lifetime takes SIGTERM/SIGINT, stops only the API, and leaves GTK running, so the process never exits. Fix: give the API host a lifetime that ignores signals (`IHostLifetime` whose two methods return `Task.CompletedTask`), and let the desktop process own them with `PosixSignalRegistration`.
- **Quitting.** `Application.Quit()` does not end the GTK loop in this backend, and `GtkMauiApplication` exposes neither a quit nor its `Gtk.Application`. What works, on the dispatcher: `foreach (var w in app.Windows.ToList()) app.CloseWindow(w);`, after which `Run` returns. (`Gio.Application.GetDefault()?.Quit()` was kept as a fallback and was never needed.)
- **Shutdown.** Stop the API after `Run` returns: the socket file is removed and only the database file remains, with no `-wal` or `-shm` files.
- **After a crash** the socket file stays, and the next start fails with "address already in use". Fix: before binding, connect to it. If it refuses, the file is stale and can be deleted; if it answers, another instance is running.
- **Single instance.** The check above refused a second instance cleanly. The real host can do better: with an application id, `Gtk.Application` is already single-instance over D-Bus (a second launch activates the first). That only works if the API starts in `OnStarted`, which runs in the primary instance only, rather than before `Run` as in the spike.
- The database belongs under `$XDG_DATA_HOME`; the spike's temp path landed in a per-`nix-shell` `TMPDIR`.

### Question 4: appearance

- COSMIC publishes the choice through the XDG portal (`org.freedesktop.appearance color-scheme`, 1 dark and 2 light) and themes window frames itself. It does not set GTK's `gtk-application-prefer-dark-theme`, nor GNOME's `color-scheme`.
- GTK 4.22 follows the portal in its newer setting, `gtk-interface-color-scheme`. **WebKitGTK 2.52.6 follows only the older `prefer-dark-theme`.** Without a fix, the view reports light at start with the desktop dark, and never changes live: only the window frame follows (developer: "partly, window frame"). MAUI's `RequestedTheme` stays `Light` too.
- **Workaround (verified):** before the window is created, mirror the new setting into the old one, now and on every change. Measured: a dark start gives `prefers-color-scheme: dark` in the view; five live toggles were each followed within about a second; the developer confirmed it.
  ```csharp
  var settings = Gtk.Settings.GetDefault()!;
  void Apply() => settings.GtkApplicationPreferDarkTheme =
      settings.GtkInterfaceColorScheme == Gtk.InterfaceColorScheme.Dark;
  settings.OnNotify += (_, e) => { if (e.Pspec.GetName() == "gtk-interface-color-scheme") Apply(); };
  Apply();
  ```
- **Startup flash:** measured with a script injected at document start (`WebKit.UserScript`, `InjectionTime.Start`). Until the two ~760 KB theme stylesheets are parsed (about 360 ms), the page is transparent, so WebKit's white default shows: a white flash on every dark start. An inline default background in the host page makes it dark from the first parsed frame (about 100 ms), and the developer saw no flash in three starts:
  ```html
  <style>
      html { background: #f5f5f5; color-scheme: light dark; }
      @media (prefers-color-scheme: dark) { html { background: #121212; } }
  </style>
  ```
- The Settings page's System, Light and Dark switched the whole UI instantly through the unchanged `AppearanceState` and `appearance.js` (logged, and confirmed by eye). The choice is not persisted across restarts on the desktop; that is `IUserSettingsService`'s job.
- Inferred, not tested: since GTK's `prefer-dark-theme` drives the view's `prefers-color-scheme`, the desktop host can apply a *fixed* Light or Dark choice by setting it directly, and keep mirroring only for System. The static host page's media-query links and default background are then right at first paint in all three modes, with no server render.

### Question 5: language

- All rendering and events run on managed thread 1, the GTK main thread. Setting `CultureInfo.DefaultThreadCurrent(UI)Culture` and `CultureInfo.Current(UI)Culture` there is enough for `IStringLocalizer`.
- A re-render of the page alone switches our text and Radzen's own text (German pager, from Radzen's satellite assembly) and keeps component state.
- Rebuilding the root (a `@key` on the `Router`, changed through an event) re-renders everything including the layout, at the cost of component state (a pager returned to page 1). That is the same loss as the web's reload, so it is the simpler, safe choice.
- The process starts in `en-US` (from `LANG`) while `SupportedCultures` lists only `en`, so the language drop-down on the Settings page is empty. Desktop culture resolution needs the parent-culture matching the web's request localization already does.

### What the follow-up changes should take from this

1. **Shared seams** (unchanged in scope, now with concrete items):
   - Extract the UI to a Razor class library.
   - Drop the redundant render mode in `MainLayout`.
   - Remove the layout's hard dependency on `IHostEnvironment`, or document that each host registers one.
   - Move asset paths to `_content/<Library>/`, including the `appearance.js` import.
   - Extract the API composition (`AddOfficeRkyvApi`/`MapOfficeRkyvApi`); this spike was its third copy.
   - Add `IUserSettingsService` for appearance and culture. The web implementation keeps cookies and `/culture/set`. The desktop implementation stores a file under `$XDG_CONFIG_HOME`, resolves the culture with parent matching, switches it in-process with a root rebuild, and applies appearance through GTK's `prefer-dark-theme` as described under question 4.
2. **Desktop host**, as its own change, carrying every workaround above:
   - Wiring: the Blazor host setup and the `MauiCss` fix, or no components in the host project.
   - API lifetime: signal ownership, quitting through `CloseWindow`, and starting the API in `OnStarted` with the socket claim.
   - Data location: the database under `$XDG_DATA_HOME`.
   - Appearance: the scheme bridge and the host-page default background.
   - Diagnostics: console logging.
   - Pin the preview package versions exactly, since the backend "will change between releases".
3. **Dev shell and packaging:** `gtk4`, `webkitgtk_6_0` and `glib` on `LD_LIBRARY_PATH`, preferably in a separate dev shell, so the web-only workflow does not have to download WebKitGTK (about 170 MB unpacked). Packaging (Flatpak or Nix) is still open.

None of the results changes the order of these changes. The one addition is that the settings seam now has a concrete desktop mechanism for each of its two settings.
