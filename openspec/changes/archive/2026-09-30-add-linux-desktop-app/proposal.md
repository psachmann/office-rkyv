# Proposal

## Why

The goal is a standalone Linux desktop app: one process holding the API and its SQLite data, showing the existing Blazor UI in a native window, with no server and no browser. The `linux-desktop-app-spike` change (archived 2026-09-30) proved this works with .NET MAUI's experimental GTK4 backend, and found the handful of workarounds it needs. Its "Spike results" section is the evidence this proposal builds on. Two things stand in the way today. First, the UI and the API composition live inside web-only projects: the UI in the Blazor Server app, and the API composition in the API's `Program.cs`, already copied once into the tests. Second, user settings are tied to cookies and a page reload. The web app stays, as the Aspire-driven development and test harness.

## What Changes

- **Shared UI library.** The pages, dialogs, layout, facades, `.resx` resources and static assets move from `OfficeRkyv.Web` into a new Razor class library, `OfficeRkyv.UI`, used by both hosts. Web-host-only parts stay in `OfficeRkyv.Web`: `App.razor`, the reconnect modal, the error page and `/culture/set`. The redundant render mode on `RadzenComponents` in the layout is removed; it made a `BlazorWebView` throw.
- **Shared API composition.** The API's service and endpoint composition becomes `AddOfficeRkyvApi` / `MapOfficeRkyvApi` in `OfficeRkyv.Api`, used by the API server, the desktop app and the unit tests' pipeline host (which removes the existing copy).
- **User settings behind one service.** `IUserSettingsService` in the UI library holds the appearance and language settings, their persistence and their change notification, replacing `AppearanceState`. The web implementation keeps today's behaviour (cookies, and a reload to change language). The desktop implementation stores settings in a file in the user's config directory, and changes the language in place, without a restart.
- **The desktop app**, `OfficeRkyv.Desktop`: a MAUI GTK4 app hosting the shared UI in a `BlazorWebView`, with the API running in the same process on a private unix socket and the data in the user's data directory. Its appearance follows the desktop's light/dark setting, live, including on desktops like COSMIC that only publish it through the XDG portal. It allows one running instance, shuts down cleanly on window close or a termination signal, and recovers from a crash's leftovers.
- **Developer tooling:** a separate `desktop` dev shell with the GTK4, WebKitGTK and GLib libraries (so the web workflow does not download WebKitGTK), a `just desktop` recipe, and docs.
- **Specs:** a new `desktop-app` capability. `ui-settings` and `ui-localization` are reworded so persistence and language application are per client (web or desktop), with the web behaviour unchanged.

## Capabilities

### New Capabilities
- `desktop-app`: the standalone Linux desktop application: what it contains and runs, where it keeps its data and settings, how it starts, runs as a single instance and shuts down, and how it follows the desktop's appearance and language.

### Modified Capabilities
- `ui-settings`: the appearance choice is remembered per client (a cookie on the web, the user's settings file on the desktop), not only in a cookie.
- `ui-localization`: the language choice is remembered and applied per client. On the web: a cookie and a page reload, as today. On the desktop: the settings file, applied in place. The desktop's initial language comes from the operating system's locale, matched to a supported culture through its parent culture (`en-US` to `en`).

## Impact

- New projects: `src/OfficeRkyv.UI` (Razor class library) and `src/OfficeRkyv.Desktop` (MAUI GTK4 app), both added to `OfficeRkyv.slnx`. The desktop project builds without the GTK libraries, which are only needed to run it, so the root build and the pre-push hook keep working in the default dev shell.
- `src/OfficeRkyv.Web`: most components, resources and assets move out, and the host keeps only what is web-specific. User-visible behaviour of the web app is unchanged. The existing unit and functional tests are the safety net, updated for the moved types and asset paths.
- `src/OfficeRkyv.Api`: the composition becomes reusable extension methods. `Program.cs` and the tests' `PipelineHost` use them.
- New preview dependencies, pinned exactly: `Microsoft.Maui.Platforms.Linux.Gtk4`, `.BlazorWebView` (and `.Essentials` only if needed) at `0.1.0-preview.12.26421.1`. The backend is experimental and unsupported by Microsoft.
- `flake.nix` (new `desktop` dev shell), `justfile`, `CLAUDE.md` (UI and resource paths move to `OfficeRkyv.UI`, and the settings and language mechanics per host).
- Tests: unit tests for the composition, the settings service implementations, culture matching, the socket and single-instance handling, and the desktop's API over a socket. Functional tests stay web-based (Playwright cannot drive WebKitGTK). The desktop is verified by a person on a graphical session for what only an eye can judge.
- Out of scope: packaging and distribution (Flatpak, Nix package, `.desktop` integration beyond what the backend generates), other operating systems, migrating data between web and desktop, file import and other desktop-only features, and adding languages. Adding a language is unchanged: resource files plus `SupportedCultures`.
