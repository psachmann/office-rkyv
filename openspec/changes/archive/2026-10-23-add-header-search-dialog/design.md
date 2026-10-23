# Design

## Context

See `proposal.md` for the motivation and scope, and the two spec deltas (`header-search`, `user-workspace`) for the behaviour.

Today the search is a part of `Workspace.razor`: a `RadzenTextBox`, a `Subject.Throttle` (300 ms), the query in the address (`?q=` read with `[SupplyParameterFromQuery]`, with `knownQuery`, `knownUserId`, `knownArchiveId` and `typingVersion` to tell the field's own address changes from other ones), a results view that replaces the list, and a `DocumentList` fragment whose rows carry download, rename, file and delete. The data side is a query on `DocumentsFacade` (`SearchDocuments`, keyed `[search, ownerId, query]`, refreshed by every document mutation), `SearchTerms` in `OfficeRkyv.Contracts` (the server's word rule), and `WorkspaceErrors.From(error).Query` for a refused search. None of that changes.

The header (`MainLayout.razor`) is a `RadzenHeader` with the sidebar toggle and the title. `RadzenComponents` is in the layout, so `DialogService` works on every page and in both hosts; the workspace already opens dialogs with it. `SidebarUser` is the precedent for a signed-in-only layout part: a component in `Components/Layout` inside `AuthorizeView`. The shared UI loads scripts as ES modules through `IJSRuntime` from `_content/OfficeRkyv.UI/js/…` (the web host's `appearance.js`); the desktop page serves the same static web assets (`_content/Radzen.Blazor/…` is loaded that way in its `index.html`).

Constraints from the guideline docs: no hardcoded user-facing text, resources in `en` and `de` beside the component, German "du" wording (`docs/ui.md`); a page never calls a gRPC client, it uses a facade; Radzen components and utility classes before CSS; `data-testid` only for elements without a name of their own; log only through `[LoggerMessage]` (nothing in this change logs).

## Goals / Non-Goals

**Goals:**

- Search from any page, in one gesture: the header control, or Ctrl+K / Cmd+K.
- A dialog that is quick to use: focused field, results under it, and a keyboard path from typing to a result.
- Less code on the workspace page, not more.

**Non-Goals:**

- Renaming, filing and deleting from a result (they stay on the workspace lists).
- Previewing a document, highlighting a match, paging past 50 results, searching the words inside files (all in `TODOS.md`).
- Any change to the API, the proto, the facade's query or the telemetry.
- Showing the chosen document marked in the list it lands on (added to `TODOS.md`).

## Decisions

### D1. The header control is a button that looks like a field

`HeaderSearch.razor` in `Components/Layout`, rendered by `MainLayout` in the middle of the header inside `AuthorizeView`, as `SidebarUser` is. It is a `RadzenButton`-like element with a search icon, the text "Search documents..." and the shortcut ("Ctrl+K" / "Strg+K") in a key-cap, and its accessible name is "Search documents" ("Dokumente durchsuchen" in German, the wording the workspace's old search field had). It is a button and not an input because a click on a field would have to hand the focus to a second field in a dialog, which loses the focus or the first keystroke in some browsers, and because a screen reader should announce "opens a dialog", not an editable field. The button sets `aria-haspopup="dialog"` and `aria-keyshortcuts="Control+K Meta+K"`.

The header becomes a three-part row: title and toggle on the left, the control in the middle, an empty part on the right that balances the left, so the control is centred on the window and not between its neighbours. That layout is a CSS grid (`1fr minmax(0, 28rem) 1fr`) in `MainLayout.razor.css`, with `::deep` for the Radzen header, because Radzen's stack cannot centre against both neighbours. Below roughly 40rem the text and key-cap are hidden and the button is an icon; its accessible name stays.

Alternative considered: a real `RadzenTextBox` in the header that opens the dialog on focus. Rejected for the focus hand-over above.

### D2. The dialog is opened through `DialogService`, and it owns its state

`SearchDialog.razor` is opened with `DialogService.OpenAsync<SearchDialog>` from `HeaderSearch`, with the title "Search documents" (it gives the dialog its accessible name, as the other dialogs have a title), a fixed width, closing on Escape and on a click outside, and a top offset so the result list grows downwards without the dialog jumping. The options (checked against Radzen 11 in task 1.1): `Width` (a fixed width), `Top` (the offset), `CssClass = "search-dialog"` (so the shortcut script can tell this dialog from others), `CloseDialogOnEsc` (true, already the default), `CloseDialogOnOverlayClick = true` (default is false), `ShowTitle` and `ShowClose` left at their defaults (the title is the dialog's accessible name), and `AutoFocusFirstElement` (true by default; the dialog still focuses the field itself, see Risks).

The dialog holds everything the search needs and nothing is kept elsewhere:

- `searchText`, a `Subject<string>` throttled with `Throttle(SearchPause)` (300 ms, the value the workspace used) as in the old page, and the same `SearchTerms.From(text).Count > 0` test that decides between sending a query and showing the hint. The field writes `searchText` on `oninput`; the throttled value is trimmed and given to `Documents.SearchDocuments.SetArgs(new DocumentSearch(userId, query))`. The user id comes from the cascaded `AuthenticationState`, as in `Workspace.razor`.
- **No address, no version counter.** The old page needed `knownQuery`, `knownUserId`, `knownArchiveId` and `typingVersion` because two things could write the query: the field and the address. In the dialog only the field writes it, so a typed value that arrives after a pause is always for the current text. The dialog's subscriptions are in a `CompositeDisposable`, so a dialog closed while typing is still resting does nothing afterwards.
- **A dialog starts empty.** `SearchDocuments` is a query of the scoped facade and keeps its last arguments, so the dialog renders the query only when the current text has a searchable word; an empty field renders the hint and never last time's results. The result cache by key stays useful: the same words typed again show the cached list while it refetches.
- States are rendered like the old results view: hint, `Transition` over `SearchDocuments` (loading, failure, content), no-results and "more matched" messages. The texts move from `Workspace.resx` to a `SearchDialog.resx` with the same wording where it still fits, and `de` beside it. The failure branch uses `WorkspaceErrors.From(error).Query` for a refused query (`too_short`, `too_long`) and the general load-failed text otherwise.
- `WatchForSignOut(Documents.SearchDocuments)` moves with the query: a refusal that carries `UNAUTHENTICATED` ends the sign-in through `WorkspaceErrors.EndSignInIfRefusedAsync`, as before. Closing the dialog on sign-out follows from the sign-out navigating away.

The archive names for "where it is" come from `Archives.FindArchives`, the query the sidebar already shares; the dialog sets its args with the user id and renders results inside a `Transition` over it, as the old results did.

### D3. A result opens its location; its only other action is a download

A result is one list item with two controls: a wide "open" button with the title, the file name (with size, through `FileSize.Format`) and the location, and, beside it, a small download button named "Download {title}" (the existing `DownloadAria` wording). Choosing the open button closes the dialog (`DialogService.Close()`) and navigates to `/workspace` for an inbox document and `/workspace/archives/{id}` for an archive. A small static `WorkspaceAddresses` (`Inbox`, `Archive(string id)`, the id as the DTOs carry it) replaces the two private members `InboxPath` and `ArchivePath` of `Workspace.razor`, so the page and the dialog build the same addresses. A document in the archive that is already open just closes the dialog: the navigation is to the same address. `DialogService` also closes its dialogs whenever the address changes (task 1.2), so the explicit `Close()` matters only for that same-address case, where no navigation happens.

The download uses `DocumentsFacade.OpenDownloadAsync` and `IFileSaver` exactly as `Workspace.DownloadDocument` does, and the dialog stays open. A refusal is mapped with `WorkspaceErrors.From(error).General` (and `ReloadNeeded` invalidates the same kinds the page invalidates) and is shown in an alert inside the dialog, apart from the search failure state, so a refused download is not shown as a failed search. A successful desktop save shows the same "saved to" notification the workspace shows. Sharing the download between the page and the dialog is not worth a new service for twelve lines; if a third caller appears, move it to the facade then.

Rename, file and delete are not in the dialog. The old spec line "a result offers the same actions as any listed document" is dropped with the removed requirement. The cost is that a user lands on a list and must find the document in it again; it is the smaller change, it keeps confirm dialogs from stacking on a dialog, and it is noted in `TODOS.md` as "show which document was chosen".

Test id: only the location text (`search-result-location`), which has no name of its own, as `document-location` had on the page. Everything else is found by role and name.

### D4. Keyboard: native focus, handled in the page, no ARIA active-descendant

The keys are handled by `wwwroot/js/search-dialog.js`, a listener on the dialog's container that the dialog sets up after its first render, and not through .NET: a round trip to the server for every arrow press would be felt on the web, and the script can cancel the arrow keys' own action in the field. Enter in the field clicks the first result's open button. ArrowDown in the field moves the focus to the first result's open button (marked `data-search-result`); on a result, ArrowDown and ArrowUp move to the next and previous one, and ArrowUp on the first goes back to the field. Enter on a focused open button is the button's native activation. Real focus is used instead of a "marked" row with `aria-activedescendant` because that pattern needs the results to be a `listbox` of `option`s, and a download button cannot sit inside one. Typing after moving to a result goes nowhere; Up returns to the field.

The dialog does not use `role="combobox"`; the list is a Radzen stack with `role="list"` and `listitem` rows and a name.

The script call can arrive after the dialog is gone (Escape right after opening), when the container no longer exists. The dialog catches that `JSException`: left alone it ends the whole circuit (found by the functional test of Escape).

### D5. The shortcut is a document-level listener in a small module

`wwwroot/js/search-shortcut.js` in the UI library exports `register(dotNetReference)`, which adds a `keydown` listener on the document in the capture phase and a matching `unregister()`. The listener reacts to `key` `k`/`K` with `ctrlKey` or `metaKey` and no `altKey` or `shiftKey`, calls `preventDefault()` immediately (a handler that answers later through .NET would be too late to stop Firefox's Ctrl+K), and then calls `dotNetReference.invokeMethodAsync('OnShortcut', anotherDialogIsOpen)`. `anotherDialogIsOpen` is computed in the script from the page: whether a dialog element is in the document (task 1.2: Radzen gives every dialog `role="dialog"` by default and a confirm dialog `role="alertdialog"`, so the script looks for `[role="dialog"], [role="alertdialog"]` and reports any dialog, the search dialog included; `OnShortcut` therefore asks its own open dialog first, then the flag; the functional test of the shortcut with the upload dialog open confirms it against the real markup). It is computed there because Radzen's `DialogService` exposes no list of open dialogs.

`HeaderSearch` imports the module in `OnAfterRenderAsync(firstRender: true)`, registers a `DotNetObjectReference<HeaderSearch>`, and disposes both with the component. The component is inside `AuthorizeView`, so a signed-out page has no listener and the browser keeps its own Ctrl+K. `OnShortcut` does three things: another dialog open → nothing; the search dialog open (the component tracks its own open dialog through the task `OpenAsync` returns) → focus its field; otherwise open it. A second dialog opened by the shortcut with a confirm dialog open under it would hide an unsaved choice, hence the guard.

The script logs one line to the browser console when it registers and when the shortcut fires (`search shortcut registered`, `search shortcut pressed`). In the desktop app's Development run the page's console reaches stdout, which is the evidence for the by-hand check. This is the page console and not the application's logging, so the `[LoggerMessage]` rule does not apply.

The web host needs no change; the module is a static web asset of the UI library. The desktop serves `_content/OfficeRkyv.UI/…` the same way as `app.css`, and the page's own relative `import` resolves against its `<base href="/">` as in the web (to be seen once in the app: task 7.2).

The key-cap shows `Ctrl+K` in English and `Strg+K` in German from resources. It does not switch to `⌘K` on a Mac: the label would need the platform, and `aria-keyshortcuts` names both.

### D6. What leaves `Workspace.razor`

The field, `SearchResults`, `HasSearchableWord`, `IsSearching`, `searchText`, `knownQuery`, `knownUserId`, `knownArchiveId`, `typed`, `typingVersion`, `ApplySearch`, `OnSearchInput`, `SearchFailureText`, the `[SupplyParameterFromQuery]` property, the `WatchForSignOut(Documents.SearchDocuments)` call and the branch in the markup that chose between the search and the list. `LocationName` leaves with the results (the dialog has its own; the list only shows it with `locations`, which only the search passed, so the parameter and the third grid column go too). `UserId` stays: the page uses it for its other queries. An old address with `?q=` opens the workspace and the unread parameter is ignored.

`DocumentList` loses its `locations` and `emptyTestId` parameters if nothing else uses them. The `Search` key kind stays in the invalidation lists: the dialog's query is still the facade's `SearchDocuments`.

### D7. Tests

| Decision | Level |
|---|---|
| Search and the facade (keys, invalidation by kind), the word rule, the refusal mapping | unchanged: unit, pipeline and UI-logic tests already cover them |
| `WorkspaceAddresses` builds `/workspace` and `/workspace/archives/{id}` | unit (new `WorkspaceAddressesTests`) |
| New and changed resource keys have an `en` entry and a `de` entry with the same placeholders | unit (`TranslationCompletenessTests`, already) |
| Control shown while signed in, not signed out; opens the dialog with the field focused | functional (`SearchTests`, new, page object `SearchDialogPage`) |
| Typing finds documents of inbox and archive with their location; hint, no match, Escape and reopening empty; choosing a result lands on the right entry from the settings page; keyboard path; download; Ctrl+K from the workspace and settings, again while open, not with the upload dialog open, not signed out | functional (same class) |
| The query is not in the address | functional (assert `?q=` absent from the URL) |
| German labels | functional (one test with the German language set, as the settings tests do) |
| The shortcut and the dialog's focus in the desktop app | by hand (task 7.2) |

The old search tests in `WorkspaceTests` (about ten) and the locators in `WorkspacePage` go: what they covered (debounce, address, back button, clearing) no longer exists, and what survives (finding across inbox and archives, hint, no match) is in `SearchTests` against the dialog. The "wait for a document that only the results show" note in `docs/testing.md` stays true for the dialog and is reworded.

### D8. Effect on traces, metrics and logs

None. The RPC, its handler and the spans are unchanged, so the rule that the query never appears in a span or a log (`docs/observability.md`) is unchanged and `ApiTelemetryTests` still guards it. The query no longer travels in the page address. The shortcut's two console lines carry no query.

## Guideline docs to update

- `docs/ui.md`: replace "Search: state kept in the address" with the dialog (own state, debounce, the states it shows, the result's two actions) and add the header control and the shortcut module under a new short section. The line in "Data" that says the page asks the facade for the search stays but names the dialog.
- `docs/testing.md`: reword the debounced-search note for the dialog; mention `SearchTests` and `SearchDialogPage`.
- `docs/desktop.md`: say that the shortcut and the dialog's focus are checked by hand, with the two console lines as evidence.
- `docs/api.md`, `docs/architecture.md`, `docs/observability.md`: none.

`TODOS.md` loses "A search box on every page" and gains "Show which document was chosen" (a result lands on the list without marking the document). `openspec/specs/user-workspace` and the new `header-search` spec are updated by archiving.

**Departures from a guideline doc.** `docs/ui.md` says a state that a reload, the back button or a pasted link should keep belongs in the address, and `add-document-title-search` (D5, D6) put the search there. This change departs from it for the search on purpose: a dialog is a transient surface, and a restored dialog on reload or back would be surprising; the answer to "keep my search" is the next Ctrl+K. The earlier decision is superseded for the UI only; D1 to D4 of that design (the engine, the RPC, the limit) stand.

## Risks / Trade-offs

- **Radzen may take the focus after the dialog opens**, so the field is not focused. Mitigation: focus it in `OnAfterRenderAsync` on the first render and, if the dialog's own focusing wins, after a `Task.Yield`; the functional test asserts the focus, so it shows up in a run.
- **The shortcut reaches the app only once the circuit is interactive.** Before that the browser's Ctrl+K applies. Acceptable: the control is also not usable before then.
- **Ctrl+K in other places** (a rich-text field, the browser's address bar when the page has no focus) is not ours; inside this app's pages there is no field that uses it. The script does not special-case text fields, because a palette shortcut is expected to work while typing.
- **Landing on a list without the document marked.** Noted above and in `TODOS.md`; the document's location is also in the result, so the user knows which list.
- **The dialog is the only user of the facade's single `SearchDocuments` query**, so two open dialogs would share it. The shortcut and the control both go through `HeaderSearch`, which tracks one open dialog, so there is never a second one.
- **Desktop WebKitGTK may not deliver Ctrl+K to the page**, or may treat it as a window accelerator. Only the app shows it, hence the by-hand task; if it fails, the control still works, and the fix would be a desktop workaround listed in `docs/desktop.md`.

## Open Questions

- Whether the key-cap should show `⌘K` on a Mac: left out until someone asks (D5).
- Whether a result should open the document once a preview exists: tied to the "In-app preview" item in `TODOS.md`.
