# Proposal

## Why

A user's workspace shows two navigation columns side by side: the app's sidebar, and the workspace page's own list of the inbox and archives. The app's sidebar still carries two entries left over from scaffolding (`Greeter`, `Error`), one of which leads nowhere on the desktop. Putting the workspace's navigation into the one sidebar gives the documents the full width, and lets the page's hand-written layout CSS go.

## What Changes

- **The inbox and archives move into the app's sidebar.** While a user's workspace is open, the sidebar lists the inbox (with its count) and that user's archives, between `Home` and the bottom-pinned `Settings`. On every other page the sidebar shows no workspace entries. The workspace page no longer has a navigation column of its own. **BREAKING** (UI only): the entries are no longer in the page, and with the sidebar collapsed they are hidden with it.
- **Creating an archive** stays next to the archive list, so it moves into the sidebar with it.
- **Renaming and deleting an archive act on the selected archive only**, from the head of the main area, next to the archive's name. They are not offered while the inbox is selected. **BREAKING** (UI only): the per-entry rename and delete buttons are gone; an archive has to be selected first.
- **The test entries leave the sidebar**: `Greeter` and `Error` are removed from the navigation, together with their texts. The pages themselves stay reachable by address.
- **`Workspace.razor.css` is deleted.** The entries, the count and the selection marking come from Radzen's components.

Out of scope: removing the Greeter page, its gRPC service and its contract; remembering a "current user" so that the sidebar shows a workspace on other pages (sign-in will bring that); any change to the API, the contracts or the data.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `user-workspace`: the navigation list of the inbox and archives is in the app's navigation sidebar instead of the page; renaming and deleting an archive apply to the selected archive.

`ui-settings` is unchanged: its `Settings` entry stays at the bottom, below and apart from the other entries, which now include the workspace's while one is open.

## Impact

- `src/OfficeRkyv.UI/Components/Layout/MainLayout.razor` and its `.resx`: the `Greeter` and `Error` entries and their texts go; the sidebar gets a place for a page to put its own entries.
- `src/OfficeRkyv.UI/Components/Pages/Users/Workspace.razor` and its `.resx`: the navigation is rendered into the sidebar, the main area's head gets the rename and delete actions, and the two-column markup goes.
- `src/OfficeRkyv.UI/Components/Pages/Users/Workspace.razor.css`: deleted.
- Tests: `WorkspacePage` (how the entries and the archive actions are found), `WorkspaceTests` (new cases for where the entries and actions are), and `SettingsTests` (its "below the other entries" check refers to the `Error` entry).
- Both hosts render the shared UI, so the web host and the desktop app both change. The desktop is checked by hand.
- No new packages, no API, contract or data changes.
