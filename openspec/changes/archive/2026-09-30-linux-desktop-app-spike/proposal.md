# Proposal

## Why

The plan for a standalone Linux desktop app (the API inside the app, the existing Blazor UI in a GTK4 window) rests on an experimental backend: .NET MAUI's GTK4 backend in `dotnet/maui-labs`, which Microsoft documents as "experimental, will change between releases, not officially supported". Two follow-up changes are waiting on it: extracting a shared UI library and a settings service, and the desktop host itself. Both restructure the solution around assumptions nobody has checked. A short, throwaway spike answers the open questions first, so that those changes are built on evidence, and so that a dead end costs a spike and not a restructuring.

## What Changes

- **A throwaway spike project** under `spikes/linux-desktop/`, outside the solution file, with its own build props so the repo's root build, tests and pre-push hook are unaffected. It is a GTK4 MAUI app hosting a `BlazorWebView`, and it runs the real Razor components and the real API in one process.
- **Five questions answered with evidence**, each with a pass criterion stated in the design:
  1. Does the existing UI (Radzen, the Material theme, icon font, dialogs, context menu, ES-module JS interop) render and work in `BlazorWebView` on WebKitGTK?
  2. Does the real API run inside the desktop process, with the real gRPC clients reaching it over a unix socket, and does it shut down cleanly?
  3. What does a NixOS dev shell need for GTK4 and WebKitGTK 6 to work from GirCore: packages and environment variables?
  4. Does the System appearance work: does WebKitGTK's `prefers-color-scheme` follow the desktop's light/dark setting, live, and is there a flash of the wrong theme at startup?
  5. Can the UI language be changed in-process, without a reload, and re-render the tree?
- **A decision, recorded in `design.md`**: go, go with workarounds, or no-go for MAUI GTK4, with a Photino.Blazor fallback tried only if a blocker needs it. The recommendation includes the exact versions, Nix packages, environment variables and the few snippets a follow-up change will need.
- **Cleanup**: once the results are recorded, the spike project is deleted. Nothing from `src/`, `tests/`, `flake.nix` or the root props changes.

No product behavior changes, so this change declares `skip_specs: true` and has no spec deltas.

## Capabilities

### New Capabilities
None. The spike changes no behavior; its output is knowledge.

### Modified Capabilities
None.

## Impact

- New, temporary: `spikes/linux-desktop/` (a MAUI GTK4 head project, its own `Directory.Build.props` and `Directory.Packages.props`, an ad hoc Nix shell). Deleted at the end.
- NuGet preview packages, pinned: `Microsoft.Maui.Platforms.Linux.Gtk4` and `Microsoft.Maui.Platforms.Linux.Gtk4.BlazorWebView`, `0.1.0-preview.12.26421.1` (the newest at the time of writing).
- Needs on the machine: a graphical session, GTK 4.12 or newer and WebKitGTK 6. Finding out how to get them on NixOS is part of the spike.
- Out of scope: any product code, the shared UI library, `IUserSettingsService`, the desktop host as a real project, packaging (Flatpak or Nix), file import, and changes to the specs or the dev shell in `flake.nix`. Those belong to the follow-up changes the spike informs.
