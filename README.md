# office-rkyv
Your digital paperless office archive.

## Running it

Everything runs from the Nix dev shell (`nix develop`, or direnv); `just` lists the recipes.

### Web app (development and test harness)

`just dev` starts the whole app (API, web UI and their dependencies) through the Aspire AppHost, with a throwaway database per run. The Playwright functional tests drive this host.

### Linux desktop app (experimental)

`just desktop` runs the standalone desktop app: one process holding the API, its SQLite data and the same UI as the web app, in a native GTK4 window. There is no server and no browser.

```sh
just desktop            # enters `nix develop .#desktop` (GTK4, WebKitGTK and GLib), then runs the app
```

It is built on .NET MAUI's experimental GTK4 backend (`Microsoft.Maui.Platforms.Linux.Gtk4`, a preview that Microsoft does not support), so expect rough edges. Only running it needs the `desktop` dev shell; building it does not.

- **Data:** `$XDG_DATA_HOME/office-rkyv/office-rkyv.db` (by default `~/.local/share/office-rkyv/`). Unlike the web app's per-run database, this is your only copy.
- **Settings:** `$XDG_CONFIG_HOME/office-rkyv/settings.json` (by default `~/.config/office-rkyv/`), holding the appearance (`system`, `light` or `dark`) and the language. Without a file the appearance follows the desktop and the language follows the system locale.
- **API socket:** `$XDG_RUNTIME_DIR/office-rkyv/api.sock`, in a directory only you can open. The API is reachable only through it, and only inside the app.
- **One instance:** starting it again opens another window in the running app, which shares its API and data; no second API starts. After a crash, the next start removes the stale socket on its own.
- **Quitting:** closing the window, SIGTERM and SIGINT all end the app and stop the API cleanly.
- `DOTNET_ENVIRONMENT=Development` (what `just desktop` sets unless you override it) also logs the API and the page's console to the terminal, and enables the WebView's developer tools.

## Documentation

The conventions of the codebase are in [docs/](docs/), one topic per file:

- [architecture.md](docs/architecture.md): the projects, how a request travels, the two hosts, the tooling. Start here.
- [api.md](docs/api.md): feature slices, event sourcing, validation, error codes.
- [ui.md](docs/ui.md): localization, facades, user settings, styling.
- [desktop.md](docs/desktop.md): the desktop host and its workarounds.
- [testing.md](docs/testing.md): the test levels and which to pick.
- [observability.md](docs/observability.md): tracing, metrics, logging.
