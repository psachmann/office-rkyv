# Design

## Context

See `proposal.md` for the motivation. What shapes the approach:

- The sidebar is in `MainLayout.razor`, in the shared UI library, and is rendered by both hosts. The layout knows nothing about users: the user of a workspace exists only in the page's address (`/users/{UserId}` and `/users/{UserId}/archives/{ArchiveId}`), as parameters of `Workspace.razor`.
- `Workspace.razor` owns everything the navigation needs: the `FindInbox` and `FindArchives` queries, the delete-archive mutation, the dialogs, the error alert, and the fallback to the inbox when the selected archive is gone.
- The navigation is plain HTML (`<nav class="workspace-nav">` with `<a>` and `RadzenButton` children) styled by `Workspace.razor.css`. That file styles nothing else: the two-column layout, the entries, the selection marking and the count.
- The web host puts one interactive render mode on the whole `Routes`; the desktop renders the same components in a `BlazorWebView`. In both, the layout and the page are in one render tree.
- The functional tests find the entries by `data-testid` (`nav-inbox`, `nav-inbox-count`, `nav-archive`), assert `aria-current="page"` on them, and find the archive actions by their accessible names (`New archive`, `Rename archive {name}`, `Delete archive {name}`). `SettingsTests` measures the Settings entry against the `Error` entry.

## Goals / Non-Goals

**Goals:**

- One navigation column. The workspace's entries look and behave like the sidebar's other entries.
- The workspace page keeps owning its state: no query, mutation, dialog or error handling moves into the layout.
- `Workspace.razor.css` is deleted and nothing replaces it.

**Non-Goals:**

- The layout learning about users, routes or workspaces.
- A sidebar that shows a workspace on pages that have no user in their address.
- Restyling the documents grid, the dialogs or the landing page.
- Removing the Greeter page, service or contract, or the web host's Error page.

## Decisions

### D1: The layout offers a section; the page fills it

`MainLayout` renders a `SectionOutlet` in the sidebar, between the top menu (`Home`) and the bottom-pinned Settings menu. `Workspace` renders its navigation in a `SectionContent` for that outlet. The outlet is identified by a static object on the layout (`SectionId`), not by a string name, so a typo cannot silently render nothing.

The markup moves to another place on the screen but stays in `Workspace.razor`, so every handler it calls stays where it is. The section empties by itself when the page is left, which gives "no workspace entries on other pages" for free.

Alternatives considered:

- **A navigation component owned by the layout**, which reads the address to find the user. The layout would have to parse routes, and creating an archive and its failures would be split between two components.
- **A remembered "current user"**, so the sidebar is the same everywhere. It is what sign-in will provide, and it is a much larger change than removing a column.

### D2: The page brings its own panel menu

The section's content is a complete `RadzenPanelMenu` of its own, not bare `RadzenPanelMenuItem`s placed inside the layout's menu. A panel menu item finds its menu through a cascading value, and the content of a section gets its cascading values from where the outlet is, not from the page. Depending on that would tie the page to the inside of the layout's markup. The sidebar already holds two separate panel menus (top and Settings), so a third is the established shape.

The `SectionContent` is rendered only once the user is known to exist (the branch that shows the workspace today), so a not-found page puts nothing in the sidebar.

### D3: Entries are `RadzenPanelMenuItem`s

- **Inbox**: `Path` is `/users/{UserId}`, with `Match="NavLinkMatch.All"`. Without it the inbox would also be marked while an archive is open, as an archive's address starts with the inbox's.
- **Archives**: one item each, `Path` is `/users/{UserId}/archives/{id}`.
- Radzen marks the item whose path matches the address and its link carries `aria-current="page"`; the settings tests already rely on that for `Home` and `Settings`. The hand-written `IsSelected` marking and `.workspace-entry-selected` go.
- The inbox's count is a `RadzenBadge` in the item's `Template`, next to the text.
- The `data-testid`s (`nav-inbox`, `nav-inbox-count`, `nav-archive`) go on elements inside the `Template`. The tests then take the sidebar link that contains them. This does not depend on where Radzen puts unmatched attributes of the item itself.
- The menu gets the existing `NavLabel` text ("Inbox and archives") as its accessible name.

Alternative considered: keeping the plain `<a>` elements and moving them as they are. They would need the CSS file to stay, and would not look like the entries around them.

### D4: "New archive" is a button in the archives heading

The "Archives" overline and the `New archive` button form one row above the archive items, laid out with a horizontal `RadzenStack` and Radzen's spacing utility classes. It stays a button with the name `New archive`, so its role and the tests' way of finding it do not change. "No archives yet." stays a caption under that row.

Alternative considered: a panel menu item with a click handler. It needs no layout at all, but it would present an action as a navigation entry.

### D5: Rename and delete are in the main area's head, for the selected archive

The head of the main area shows the selection's name and `New document` today. When an archive is selected it also shows rename and delete buttons for that archive; when the inbox is selected it does not. They keep the accessible names `Rename archive {name}` and `Delete archive {name}`, the dialogs and the confirmation are unchanged.

Buttons cannot stay in the entry: a panel menu item is a link, and a button inside a link is invalid. `archiveBeingDeleted` exists because any archive could be deleted from the list; as only the selected one can be deleted now, deleting always ends on the inbox. The field can go if that makes `OnArchiveDeleted` simpler.

### D6: The sidebar shows its own loading and failure states

The navigation is nested inside the inbox and archives `Transition`s today, so it does not exist until both have loaded. In the sidebar:

- The inbox entry is always there once the user is known. Its count badge appears only when the inbox has loaded: no badge while loading or after a failure, never a `0` that is not known.
- The archive items come from their own `Transition` on `FindArchives`: a small progress indicator while loading, the existing `LoadFailed` text on failure, and "No archives yet." only for a loaded, empty list.

The main area keeps its `Transition`s as they are. Both places use the same query instances, which is what the facades are built for (one cache entry per key, shared by every component showing it).

### D7: No CSS for the workspace

What the file did, and what does it now:

| Rules | Replaced by |
|---|---|
| `.workspace`, `.workspace-nav`, `.workspace-main` | Nothing: one column, a `RadzenStack` |
| `.workspace-entry`, `.workspace-entry a`, `.workspace-entry-selected` | `RadzenPanelMenuItem` |
| `.workspace-section` | Horizontal `RadzenStack` (D4) |
| `.workspace-count` | `RadzenBadge` |

If something turns out to be impossible with Radzen's components, parameters and utility classes, the answer is not a new rule in a new file: stop and bring it back to the user.

`MainLayout.razor.css` keeps its `.app-sidebar` rules. They still pin Settings to the bottom, below whatever the section holds.

### D8: The test entries go, the test's reference changes

`Greeter` and `Error` are removed from the top menu, with `NavGreeter` and `NavError` from `MainLayout.resx`. `SettingsTests` compares the Settings entry with `Home` instead of `Error`.

## Risks / Trade-offs

- [Radzen's item markup is assumed, not read: that `Template` renders inside the item's link, and that the link carries `aria-current`] → The first implementation task checks both in the running app before anything else is built on them. If `Template` is not inside the link, the test ids move to the item and the tests find the link under it.
- [Sections are not used in this codebase yet, and the desktop's `BlazorWebView` cannot be driven by Playwright] → Checked by hand with `just desktop`, with the app's stdout as evidence, as for every desktop change.
- [With the sidebar collapsed, or on a narrow window where Radzen collapses it, the inbox and archives are hidden behind the toggle; today they wrap above the documents] → Accepted: it is how the sidebar's other entries behave. `New document`, rename and delete stay in the page.
- [A long archive name is cut or wraps differently in a panel menu item than in today's entry (`overflow-wrap: anywhere`)] → Looked at by hand with a long name. A cut-off name is acceptable, as the main area's head shows it in full when selected.
- [Renaming or deleting an archive now takes a click more: select first] → Accepted by the user.
- [The settings sidebar test is known to fail now and then on an untouched tree] → Run it again before blaming this change.

## Migration Plan

One change in the shared UI library, no data or contract involved. Rolling back is reverting the commit.
