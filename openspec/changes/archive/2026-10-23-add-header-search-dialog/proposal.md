# Proposal

## Why

Search is the feature the product is named for, but today it is a field on the workspace page: it is only there while a workspace is open, and its results push the inbox or archive list out of the way. A signed-in user should be able to find a document from anywhere in the app, in one gesture, without losing the page they are on. A search box in the header that opens a search dialog, reachable by a keyboard shortcut, does that. `TODOS.md` already holds this as "A search box on every page", and the sign-in change that it waited for is in place.

## What Changes

- The header gets a search control in its centre, shown only while a user is signed in. It looks like a search field, with the shortcut written in it, but is a button: activating it opens the search dialog.
- **The search dialog** holds a search field, focused when the dialog opens, and under it the results. Results update as the user types, after a short pause. It shows a hint under 3 characters, a no-results message, a "more matched" message when the server cut the list at 50, and a failure message that is never presented as no results. Esc closes it.
- **Each result** shows the document's title, its file name and where it is: the inbox or the archive's name. Choosing a result closes the dialog and goes to that inbox or archive. A download button on the row hands the document's file to the user without leaving the dialog.
- **Keyboard**: the shortcut Ctrl+K (Cmd+K on a Mac) opens the dialog from any page while a user is signed in, on the web and in the desktop app. Pressing it with the dialog already open keeps the field focused. In the dialog, Enter opens the first result, and Down and Up move the focus through the results, so a search can be done without the mouse.
- **The workspace page loses its own search**: the search field, the results view, the debouncing and the `?q=` address handling are removed from `Workspace.razor`. The query is no longer kept in the page address: the dialog is transient, so a reload or a pasted link does not restore a search.
- The row actions of a result (rename, file into, delete) are not in the dialog; they stay on the workspace lists. This replaces "a result offers the same actions as any listed document".
- New and changed texts exist in English and German.

Not in this change: previewing a document from a result (still postponed, see `TODOS.md`), highlighting which part matched, paging beyond the first 50 results, searching the words inside files. `TODOS.md` loses "A search box on every page".

Guideline docs whose conventions this change sets or changes:

- `docs/ui.md`: replaces "Search: state kept in the address" with the dialog (its own query, debounce and states), and adds a global keyboard shortcut handled by a small script owned by the UI library.
- `docs/testing.md`: the note about waiting for a debounced search and the address changes to the dialog.
- `docs/desktop.md`: the shortcut is observed by hand on the desktop; the doc says so.
- `docs/api.md`, `docs/architecture.md`, `docs/observability.md`: none. The RPC, the facade query and the telemetry rules are unchanged.

## Capabilities

### New Capabilities

- `header-search`: the header's search control, the search dialog with its states and results, what choosing or downloading a result does, and the keyboard shortcut.

### Modified Capabilities

- `user-workspace`: the requirement "Documents can be searched from the workspace" and the requirement "The search is kept in the page address" are removed, and the sentence in "The workspace shows the inbox and the user's archives" that makes the main area show a search is taken out.

## Impact

- `src/OfficeRkyv.UI`: a header search control in `MainLayout.razor` and a new search dialog component with its resources (`en`, `de`); a small script in `wwwroot/js` for the global shortcut; `Workspace.razor` loses its search field, results, debounce and address handling; the search texts that move to the dialog leave `Workspace.resx` and `Workspace.de.resx`. The facade query, `WorkspaceKeys` and `DocumentSearch` stay as they are; the dialog uses `DocumentsFacade.SearchDocuments`.
- `src/OfficeRkyv.Web`, `src/OfficeRkyv.Desktop`: no code change expected. Both load the script through the UI library's static web assets, and the dialog needs `IFileSaver`, which both register.
- `src/OfficeRkyv.Api`, `src/OfficeRkyv.Contracts`: none.
- `tests/OfficeRkyv.UnitTests`: UI-logic tests for what moves out of the page; `TranslationCompletenessTests` covers the new resources.
- `tests/OfficeRkyv.FunctionalTests`: the search tests in `WorkspaceTests` and the locators in `WorkspacePage` are redone against the header and the dialog, plus the shortcut.
- By hand: the shortcut and the dialog's focus in the desktop app.
- `openspec/specs/user-workspace`, `docs/ui.md`, `docs/testing.md`, `docs/desktop.md`, `TODOS.md` updated.
- No new dependency, no stored data, no telemetry change.

## Assumptions made

- **A result takes you to where the document is filed** and does not offer rename, file or delete. This was the recommended option of the exploration; the alternative (the full row actions inside the dialog) would mean extracting the document list and its mutations from `Workspace.razor` and stacking confirm dialogs on a dialog.
- **The shortcut is Ctrl+K, and Cmd+K on a Mac.** It is on the web and the desktop alike. On the web the script cancels the browser's own handling of the key.
