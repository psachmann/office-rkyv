# Tasks

Steps marked **(look)** need the developer's eyes on the graphical session (design D5): ask for the observation, do not guess it. Everything else can be checked from the shell and the spike's own stdout log.

## 1. Isolated spike project (design D1)

- [x] 1.1 Create `spikes/linux-desktop/` with its own `Directory.Build.props` (net10.0, no warnings-as-errors) and `Directory.Packages.props` (central versions, with `Microsoft.Maui.Platforms.Linux.Gtk4` and `.BlazorWebView` pinned to `0.1.0-preview.12.26421.1`), and a MAUI GTK4 head project generated from the `maui-linux-gtk4 -b` template with its package versions pinned by hand (dotnet/maui-labs#519). Verify `dotnet build` inside the spike directory succeeds, and that `dotnet build` at the repo root still builds only the projects in `OfficeRkyv.slnx`, with nothing from `spikes/` in its output.

## 2. Nix shell and a first window (question 3, design D6)

- [x] 2.1 Add an ad hoc `shell.nix` (or flake) in the spike directory, starting from the candidate packages (`gtk4`, `webkitgtk_6_0`, `gobject-introspection`, `glib-networking`), and iterate on packages and environment variables until the template's window starts from a clean shell with no manual library fiddling. Verify the process stays running and logs its GTK activation without a loader or typelib error, and **(look)** ask the developer to confirm a window is visible. Record the minimal package set, the environment variables, and each error met on the way, for the results.

## 3. The real UI in the view (question 1, design D2)

- [x] 3.1 Add the host page (`wwwroot/index.html`) and root components: a `Router` over the `OfficeRkyv.Web` assembly using its `MainLayout`, with Radzen (`AddRadzenComponents`), localization (`ResourcesPath = "Resources"`) and DotNetQuery registered, and the static assets the pages need (Radzen's `_content` files, the icon font, `app.css`, `appearance.js`) reachable. If referencing the Web assembly proves unworkable, use the scratch page built from the same Radzen components (D2 fallback) and record why. Verify the Settings page renders, the spike's stdout log shows the ES-module import of `appearance.js` resolved (call `applyAppearance` from the page and log the theme links' media), and **(look)** the developer confirms the Material theme and icon glyphs render (no missing-glyph boxes, no unstyled layout).

## 4. The API in the same process (question 2, design D3, D4)

- [x] 4.1 Start a `WebApplication` inside the desktop process, composed as `PipelineHost` does (Fisher on a SQLite file under the XDG data directory or a temp path, Wolverine in `Solo` mode, gRPC, the error mapping, the four services from the `OfficeRkyv.Api` project reference), listening on a unix socket over HTTP/2, and stopped when the window closes. Verify a `ListUsers` call over the socket from inside the process succeeds and is logged.
- [x] 4.2 Register the four gRPC clients as `Program.cs` does, but with a `SocketsHttpHandler` whose `ConnectCallback` dials the socket instead of the `https://api-svc` address. If `AddWolverineGrpcClient` does not expose the handler, record that and the workaround used. Verify the real facades fetch data through it (the Home page's user list loads, empty).
- [x] 4.3 Observe shutdown and a second instance. Close the window, and start a second instance while the first is running. Verify and record: the socket file is removed on close, what SQLite files remain beside the database, and what the second instance does (socket path clash, database lock, or both).

## 5. The real pages, end to end (questions 1 and 2)

- [x] 5.1 In the window, from the real Home page: create a user, open their workspace through the link, create an archive and a document, file the document through the "File into..." menu, delete the archive through the confirm dialog, and try a blank title to see the refusal. Verify each step works as it does in the web app, with the facade calls logged to stdout, and **(look)** the developer confirms dialogs, the context menu, the data grid, the select bar and the drop-down look and behave properly (focus, scrolling, keyboard). Record any component that misbehaves.

## 6. Appearance (question 4)

- [x] 6.1 With the desktop set to dark, then to light, record what `matchMedia('(prefers-color-scheme: dark)').matches` reports inside the view (logged to stdout), and change the desktop setting while the app runs. Verify the value follows the desktop live, and **(look)** the developer confirms the page changes with it in System mode. Record the desktop environment and session type (Wayland or X11) used.
- [x] 6.2 With the desktop dark, start the app several times and **(look)** check whether any light or blank frame shows before the theme applies. If it does, try the media-query theme links in `index.html` and a default background in the host page, and record which removes it. Also verify `applyAppearance` from the Settings page switches the applied theme between System, Light and Dark at runtime (logged, and confirmed by eye).

## 7. Language in the same process (question 5)

- [x] 7.1 Add a throwaway two-language resource (`en`, `de`) and a page that shows one localized string, `CultureInfo.CurrentUICulture` and the managed thread id, with a button that switches the culture (`DefaultThreadCurrentUICulture` and the current thread) and re-renders the root. Verify the text changes without restarting, and record the thread and culture `IStringLocalizer` sees before and after, whether a component rendered before the switch updates after the root re-render, and whether Radzen's own text (the `DefaultUICulture` cascade, for example the data grid's pager) follows.

## 8. Fallback (conditional)

- [x] 8.1 Only if question 1, 2 or 3 failed without a workaround: try Photino.Blazor on the failing question or questions only, time-boxed, and record whether it clears them. Otherwise record "not needed" in the results and mark this task done. Verify the results section states which.

## 9. Record and clean up (design D8)

- [x] 9.1 Write a "Spike results" section in `design.md`: for each of the five questions, pass, workaround or fail with the evidence; the exact package versions, Nix packages and environment variables; the short snippets a follow-up needs (the socket handler, the root-component and host-page setup, the culture switch); the decision under the D7 rule; and the recommended follow-up changes, noting any the results change. Verify the section covers all five questions and states the decision.
- [x] 9.2 Delete `spikes/` and any scratch files the spike created. Verify `git status` shows changes only under `openspec/`, and `dotnet build` at the repo root still succeeds.
- [x] 9.3 Run `openspec validate linux-desktop-app-spike --strict`. Verify it passes.
