# Tasks

## 1. Sidebar: test entries out, a section in (design D1, D8)

- [x] 1.1 Remove the `Greeter` and `Error` items from the top `RadzenPanelMenu` in `MainLayout.razor`, and `NavGreeter` and `NavError` from `MainLayout.resx`. Verify the build, and that no `NavGreeter` or `NavError` reference is left in `src`.
- [x] 1.2 Add a static section id to `MainLayout` and a `SectionOutlet` for it in the sidebar, between the top menu and the bottom-pinned Settings menu. Verify in the running web app that Home and Settings look as before, with Settings still at the bottom.
- [x] 1.3 Check the two assumptions the design rests on, in the running web app's page source, before building on them: a `RadzenPanelMenuItem`'s `Template` renders inside the item's link, and the link of the item matching the address carries `aria-current="page"`. If either is not so, use the fallback in the design's risks, and note which in this task. _Checked in the running app: the `Template` renders inside the item's `<a>` (and replaces the item's own icon and text, so the template carries the text), and the link of the matching item has `aria-current="page"`. No fallback needed._

## 2. Workspace: navigation into the sidebar (design D2, D3, D4, D6)

- [x] 2.1 In `Workspace.razor`, render the navigation in a `SectionContent` for the layout's section, only when the user was found: a `RadzenPanelMenu` named by `NavLabel`, with the inbox item (`Match="NavLinkMatch.All"`, count as a `RadzenBadge` in its `Template`) and one item per archive, carrying the `nav-inbox`, `nav-inbox-count` and `nav-archive` test ids inside their templates. Verify in the running web app that the entries appear in the sidebar, that exactly the opened one is marked, and that they are gone on Home and Settings. _Note: the section holds a `<nav>` named by `NavLabel` with two panel menus (inbox, archives) around the "Archives" heading row, because a heading with a button cannot sit inside a menu's `<ul>`._
- [x] 2.2 Put the "Archives" overline and the `New archive` button in one row above the archive items, with "No archives yet." under it for an empty list, using Radzen's stack and utility classes only. Verify that creating an archive from the sidebar adds its entry without a reload.
- [x] 2.3 Give the sidebar its own states: no count badge until the inbox has loaded, and for the archives a progress indicator while loading and the `LoadFailed` text on failure, never "No archives yet." for a list that did not load. Verify by reading the three branches in the markup, and by a reload of a workspace showing no `0` before the count arrives.

## 3. Workspace: one column, archive actions in the head (design D5, D7)

- [x] 3.1 Remove the `<nav class="workspace-nav">` and the `.workspace` and `.workspace-main` wrappers from the page; the main area becomes a `RadzenStack`. Add rename and delete buttons for the selected archive to the head of the main area, with the accessible names `Rename archive {name}` and `Delete archive {name}`, shown only while an archive is selected. Remove what is no longer used (`IsSelected`, and `archiveBeingDeleted` if `OnArchiveDeleted` is simpler without it). Verify in the running web app: no actions with the inbox selected; renaming keeps the archive selected and updates the sidebar and the head; deleting ends on the inbox.
- [x] 3.2 Delete `Workspace.razor.css`, and remove resource keys in `Workspace.resx` that nothing uses any more. Verify that no `workspace-` class is left in `src`, and that the build has no warnings about the removed file.

## 4. Tests

- [x] 4.1 Update `WorkspacePage`: `InboxEntry` and `ArchiveEntry` are the sidebar links containing the test ids (so `aria-current` and clicks keep working), and `RenameArchiveAsync` and `DeleteArchiveAsync` select the archive first. Change `SettingsTests` to compare the Settings entry with `Home` instead of `Error`. Verify the existing workspace and settings functional tests pass (the settings sidebar test is known to fail now and then on an untouched tree: run it again before looking for a cause).
- [x] 4.2 Add functional tests for the new behaviour, and verify they pass:
  - a workspace's inbox and archive entries are inside the sidebar, above the Settings entry, and the page holds no other navigation list;
  - going from a workspace to the landing page and to the settings page leaves no inbox or archive entries in the sidebar;
  - an unknown user shows no inbox or archive entries;
  - with the inbox selected no rename or delete action for an archive is offered, and with an archive selected both are.

## 5. Verification

- [x] 5.1 Run `just desktop` and check by hand, with the app's stdout as evidence: the workspace's entries appear in the sidebar and select correctly, an archive can be created, renamed and deleted, the entries disappear on Home and Settings, and a language change on the settings page followed by opening a workspace still shows them.
- [x] 5.2 Check by hand in the web app: an archive with a very long name, a narrow window (the entries hide with the sidebar and come back with the toggle), and the dark appearance. Note anything that looks wrong for the user instead of adding CSS for it. _Checked in the web app: a long name wraps in the sidebar (nothing cut off); under a narrow window Radzen collapses the sidebar with its entries; dark appearance shows the selection and the badge correctly. Nothing looked wrong. The rename button uses `ButtonStyle.Base`, as `Light` was nearly invisible on the page background._
- [x] 5.3 Run `just format`, `just test` and `openspec validate move-workspace-nav-into-sidebar --strict`. Verify all three pass.
