# Design

## Context

See proposal.md for motivation. The facts that shape the approach:

- `Workspace.razor` (+ `.razor.cs`) is `[Authorize]`, has two `@page` routes and does six jobs: the "does the user exist" gate (`FindUserById`, sign-out on null), the sidebar entries (a `SectionContent` into `MainLayout`'s `WorkspaceNavigationSection`), one `errorKey` alert, the header row, the document grid with its row actions, and four mutations. The sign-out watchers (`WatchForSignOut`) for four queries are registered in `OnInitialized`.
- The inbox/archive switch is a route parameter, so the page is one component instance that survives the switch. That is why the `SectionContent` (and so the sidebar) does not rebuild today. With two page components it would.
- All data comes from the shared facades (`UsersFacade`, `ArchivesFacade`, `DocumentsFacade`), scoped per circuit or window, so any component can show `Documents.FindInbox` or `Archives.FindArchives` and share one cache entry. `SidebarUser` and `SearchDialog` already do. A query only fetches after someone has called `SetArgs`; today the workspace page does that for `FindUserById`, `FindArchives`, `FindInbox` and, for an archive, `FindArchiveDocuments`.
- `IQuery<,>.CurrentState` gives the present state of a query, so a component can look at data that was already cached before it subscribed (`Success` alone only signals a new result).
- Both routers (`Routes.razor`, `Main.razor`) already set `NotFoundPage` to `NotFound`, and any address with no route shows it. Nothing needs to be added for the old addresses.
- The UI project has no component-test harness (no bUnit): component behaviour is covered by the functional tests; pure logic is covered by the UI-logic level (docs/testing.md).
- The `.resx` files mirror the component's folder and type (`Localization/Resources/<folder>/<Type>.resx`), and `TranslationCompletenessTests` fails when a text lacks its German twin or placeholder.

## Goals / Non-Goals

**Goals:**

- Two small pages, `Inbox` and `Archive`, that say only what is particular to them.
- One owner each for what they share: a nested layout for the user gate, the sidebar entries and the failure alert; `Components/Shared` for the document list, the dialogs and the small pieces.
- The sidebar stays mounted when the visitor moves between the inbox and an archive.
- No "workspace" left in addresses, types, test ids, resource texts, specs or docs.

**Non-Goals:**

- No change to the API, the facades' queries, the query-key kinds (`"inbox"`, `"archives"`, `"archive-documents"`, `"search"`) or what they cache. Only the C# names around them change.
- No change to how a refusal is classified (status code plus reason code), only where its message is shown.
- No new styling: the pages stay on Radzen components and utilities ([ui.md](../../../docs/ui.md#styling)).
- No upload into an archive, no redirects from the old addresses.

## Decisions

### D1: A nested layout, `DocumentsLayout`, owns what the two pages share

`Components/Layout/DocumentsLayout.razor` is a `LayoutComponentBase` inside `MainLayout` (`@layout MainLayout`). `Inbox` and `Archive` declare `@layout DocumentsLayout` and `@attribute [Authorize]`. The layout:

1. sets the query arguments for the signed-in user (`FindUserById`, `FindArchives`, `FindInbox`) in `OnParametersSetAsync`, as the old page did, and registers the sign-out watchers for those three queries and for `FindArchiveDocuments` once, in `OnInitialized`;
2. renders `Transition` on `FindUserById`: loading and failure as before; a null user shows the progress circle while `ISignInService.SignOutAsync()` runs, and `@Body` is not rendered;
3. for a found user, fills the sidebar with `<SectionContent SectionId="MainLayout.NavigationSection"><DocumentNavigation /></SectionContent>`, shows the failure alert, then `@Body`.

Blazor keeps a layout instance while the route changes between pages that use the same layout, so the section content is registered once and the sidebar is not rebuilt (spec: `sidebar-navigation`, "Moving between pages keeps the entries"). `MainLayout.WorkspaceNavigationSection` is renamed `NavigationSection`.

Alternatives considered:

- **Each page registers its own `SectionContent`.** Simplest, but the sidebar is torn down and recreated on every inbox/archive switch, which shows its loading state or flickers.
- **The entries in `MainLayout` itself.** They would then be shown on pages that must not have them (settings, landing, not-found) unless `MainLayout` learned which page is open; the spec says they appear only on the two pages.
- **One page component with a parameter, extracted pieces.** Keeps the instance but does not solve that the archive-only logic stays in the page, which is the point of the change.

Verification: functional. A test holds a handle to the inbox entry's element, goes to an archive and back through the sidebar, and checks that the handle is still attached to the document and that the count is never replaced by a loading state. The not-found page and the settings page show no entries (functional, existing journeys renamed).

### D2: Failures go to one `FailureReporter`, cascaded by the layout

The old page kept one `errorKey` field, set by every mutation and by the download. With the layout owning the alert, the pages and `DocumentList` need a way to set it. `FailureReporter` (a plain class in `Components/Shared`, created by the layout and passed through `<CascadingValue>`) has:

- `Show(Exception)`: classifies the refusal with `DocumentErrors.From` (the renamed `WorkspaceErrors`), sets the message key (the general key, or `ActionFailed`), calls `DocumentErrors.EndSignInIfRefusedAsync` with the `ISignInService`, and, when the refusal means the page is out of date (`ReloadNeeded`), invalidates the archives, inbox, archive-documents and search kinds through `IQueryClient`;
- `Clear()`;
- `Key` and a `Changed` event, which the layout subscribes to in order to re-render the alert.

The layout constructs it with the services it needs (`ISignInService`, `IQueryClient`). A cascading value of a reference type does not re-render its consumers when its state changes, which is what is wanted: only the layout's alert changes. The alert keeps the test id, renamed from `workspace-error` to `page-error`, and its localization comes from `DocumentErrors`' resources.

A small base class, `TrackedComponent` (in `Components/Shared`), gives `DocumentList` and `Archive` the `Track` helper that the old page had: it owns a `CompositeDisposable`, disposes the mutation with the component, calls `Failures.Show` on a failure and the supplied action on success, and marshals through `InvokeAsync`.

Alternatives considered:

- **Each component shows its own alert.** Two alerts can appear on the archive page (one from `DocumentList`, one from the archive actions), and the spec says a refusal is "shown in one place on the page".
- **A scoped service instead of a cascading value.** A scoped service outlives the layout (the circuit or window), so a failure would still be showing after a page change unless every consumer cleared it; the cascading value dies with the layout.
- **Inheritance for the failure state.** Rejected: pages inherit a base only for `Track`, which has no state of its own.

Verification: UI logic. `FailureReporter` is built with a fake `ISignInService` and a real `IQueryClient` and tested like the old `WorkspaceErrors` cases: a refusal sets a key, `Unauthenticated` calls `SignOutAsync` and sets no message, a stale write invalidates the four kinds, `Clear` empties it. Functional: the stale-rename and deleted-archive journeys (renamed, run on both pages for one case).

### D3: `DocumentList` owns the document row actions and their mutations

`Components/Shared/DocumentList.razor` takes `Documents` (the list to show), `Archives` (the user's archives, for the file-into menu), `EmptyTitle` and `EmptyText`. It renders the empty state or the grid, and it owns download, rename, file-into, unfile and delete with their mutations, dialogs and confirmation. Each page wraps it in the `Transition` of its own query (`FindInbox`, `FindArchiveDocuments`) and in a `Transition` on `FindArchives`, so the page already has the archives it hands down; this replaces the old `archiveSnapshot` field, which existed only because the menu was built outside a `Transition`.

`DownloadDocument` shows its failure through `Failures.Show`, and its "saved to" notification stays in the component.

Alternatives considered:

- **The page owns the mutations and `DocumentList` raises events.** Duplicates four handlers in two pages.
- **`DocumentList` loads its own query.** It would need to know which one (inbox or archive), which is what the parameter already says.

Verification: functional (existing filing, rename, delete and download journeys, run against the new pages). The empty state and the failure state are functional too.

### D4: The archive page resolves its address with a pure function, and the page decides what follows

An address is resolved against the user's archive list, not against the facade's `FindArchiveDocuments` (which fetches by id and would also answer for another user's archive with a `NotFound` refusal that the page would have to read). `ArchiveResolution.Resolve(archives, id, seenBefore)` (a static class next to `Addresses`, in `OfficeRkyv.UI.Shared`) returns one of:

| Result | When | The page |
|---|---|---|
| `Found(archive)` | the id is in the list | shows it, remembers `seenBefore = true` |
| `NotFound` | the id is not in the list and was never seen on this page | `Navigation.NotFound()` (the router's not-found page) |
| `Vanished` | the id is not in the list but was seen | `NavigateTo(Addresses.Inbox)` |

The page evaluates it when the archive list arrives (the `Success` stream) **and** in `OnParametersSetAsync` against `FindArchives.CurrentState`, since arriving from the sidebar finds the list already cached and no new `Success` is emitted. `seenBefore` is reset when `ArchiveId` changes, because the same instance is reused when the visitor moves from one archive to another. An archive the user deletes themselves is not "vanished": the delete's success handler navigates to `/inbox` directly, as it did.

A loading or failed list resolves nothing: the page shows the `Transition`'s loading and failure branches, never `NotFound`.

Alternatives considered:

- **Always fall back to the inbox (the old behaviour).** Simpler, but a stale or mistyped archive link silently shows the inbox under an archive's address; rejected in the exploration for the not-found rule.
- **Not-found in both cases.** The user would see a not-found page when an archive they are looking at is deleted from another window, which is a worse outcome than landing on the inbox.
- **Resolve from the `FindArchiveDocuments` refusal.** Ties page state to an error classification, and a document list that loads slowly would delay the decision.

Verification: UI logic for `ArchiveResolution` (found, not found, vanished, `seenBefore` after an id change). Functional: another user's archive id, an unknown id, a deleted-elsewhere archive (an archive deleted through the API client while its page is open, then a refresh of the list) and an own delete returning to `/inbox`.

### D5: Addresses move to `Addresses`; the old ones simply have no route

`WorkspaceAddresses` becomes `Addresses` with `Inbox = "/inbox"` and `Archive(string id) => $"/archives/{id}"`. `Inbox.razor` has `@page "/inbox"` (the constant is not usable in the directive, as before; the unit test pins both strings together) and `Archive.razor` has `@page "/archives/{ArchiveId:guid}"`. Nothing else is registered, so `/workspace…` is an unmatched address and the router shows `NotFound` (spec: `inbox-page`, "The former workspace addresses are gone"). The sign-in places that said `/workspace` say `Addresses.Inbox`: `Home.OnParametersSetAsync`, `AccountEndpoints` (the Web project references the UI library, and `Addresses` is public) and the search dialog's `NavigateTo`.

Because the `{ArchiveId:guid}` constraint makes a non-GUID id unmatched, it shows the router's not-found page with no page code, which is the behaviour the spec asks for.

Verification: unit (`AddressesTests`: the two strings and that an archive address is under `/archives/`); functional (`/workspace`, `/workspace/archives/{id}`, `/users/{id}` and `/archives/not-a-guid` show not-found; signed-out `/inbox` and `/archives/{id}` show the landing page; sign-in and creating a user land on `/inbox`).

### D6: Components, files and names

```
Components/
  Layout/    MainLayout, DocumentsLayout (new), HeaderSearch, SearchDialog, SidebarUser, AppTheme
  Pages/     Home, Settings, NotFound, Inbox (new), Archive (new),
             CreateUserDialog, CreateUserErrors     (moved from Pages/Users, they belong to Home)
  Shared/    DocumentList, DocumentNavigation, UploadDocumentsDialog, DocumentDialog, ArchiveDialog,
             DocumentErrors, FailureReporter, TrackedComponent,
             LoadingIndicator, LoadFailedAlert        (the Transition's loading and failure bodies)
Shared/      Addresses, QueryKinds (was WorkspaceKeys), ArchiveResolution, facades ...   (the C# folder, unchanged)
```

- `Components/Pages/Users/` is removed. `_Imports.razor` gets `@using OfficeRkyv.UI.Components.Shared`.
- `LoadingIndicator` and `LoadFailedAlert` replace the `<RadzenProgressBarCircular …>` and `<RadzenAlert …>` triplets that appear six times in the old markup; `LoadFailedAlert` takes the message as a parameter (each page passes its own localized text), so it needs no resource of its own.
- `WorkspaceKeys` becomes `QueryKinds` (it holds the key kinds and the `Invalidate` helper; "Keys" alone would collide with Fetchling's `QueryKey`). The constants and values do not change.
- `Components/Shared` and the C# `Shared/` folder have the same name on purpose (the user asked for `Shared` under components); the namespaces are `OfficeRkyv.UI.Components.Shared` and `OfficeRkyv.UI.Shared`. docs/ui.md says which holds what: components with markup in the first, plain services and helpers in the second.
- Resource files follow the types: `Workspace.resx` is split by owner (`Inbox`, `Archive`, `DocumentList`, `DocumentNavigation`, `DocumentsLayout`), `WorkspaceErrors.resx` becomes `Shared/DocumentErrors.resx`, and each dialog's file moves to `Components/Shared`. Every key moves to the type that renders it; no key is shared between types. The two texts that name the workspace change: the page `Title` and the load-failed message become the inbox's and the archive's own ("The inbox could not be loaded…").
- The German glossary row for "Workspace" is removed from docs/ui.md. Rename dialogs and others keep their wording.

Alternatives considered:

- **Keep `Pages/Users` and add `Inbox`/`Archive` there.** The folder name already describes nothing (it holds the landing page's dialog and the workspace), and the request names `Components/Shared` as the home of shared pieces.
- **A single `Documents` folder under `Pages`.** Possible, but `Pages/Inbox.razor` and `Pages/Archive.razor` mirror their addresses, like `Home` and `Settings`.

Verification: build (the Razor compiler and the code-behind rule), `TranslationCompletenessTests` (unit) for the moved and split resources, and the functional journeys for the texts.

### D7: Test ids and page objects

Test ids follow the doc's rule (only for elements with no name of their own): `workspace-selection` becomes `page-heading` (the visible heading of either page; the old page object's `Heading` locator for `workspace-heading` never matched anything and is replaced by it), `workspace-error` becomes `page-error`, and `empty-state`, `document-row`, `document-file`, `nav-inbox`, `nav-inbox-count`, `nav-archive`, `no-archives`, `archives-load-failed` and `sidebar-user` stay. `WorkspacePage` is replaced by `InboxPage`, `ArchivePage` and a small shared `DocumentListPage` locator set; `WorkspaceTests` is split into `InboxTests` (upload, empty, old addresses), `ArchiveTests` (archive actions, not-found, vanished) and `DocumentActionsTests` (file, rename, delete, download, stale writes, run on both pages where the spec says so). The sign-in helpers (`OpenNewUsersWorkspaceAsync`, `CreateUserAsync`, `UserCard`) move to a `SignInPage` since several classes use them.

Verification: the functional suite itself; the by-hand desktop check (below) covers what the browser cannot.

### D8: Docs and specs

`docs/ui.md`, `docs/testing.md` and `docs/desktop.md` are updated in the same change (see the list below). `TODOS.md` gets "Upload straight into an archive". At archive time `user-workspace` ends with no requirements; its spec directory is removed when the change is archived (the `REMOVED` entries carry the migration).

## Observability

None. The UI project logs nothing for these pages today (a `[LoggerMessage]` method is only needed where a log is written), and this change adds no log, span or metric. The query and mutation telemetry comes from Fetchling and is unchanged: the query keys' kinds are the same, so the dashboards and `fetchling.*` metrics keep their series. Nothing in [observability.md](../../../docs/observability.md) changes.

## Guideline docs this change must update

- `docs/ui.md`: the paths in "Adding a new localized string" (`Components/Pages/Home.razor` example stays valid), the German glossary (drop "Workspace"), "Signing in" (routing: the two pages and the layout, not `/workspace`; the sidebar; the old address; `WorkspaceErrors.SignedOut` becomes `DocumentErrors.SignedOut`), "Data" (the facades' keys by name `QueryKinds`), "Search" (`WorkspaceAddresses` becomes `Addresses`; "the workspace lists" becomes "the inbox and archive pages"), "Failures become localized messages" (the record's new name and place, the one alert and `FailureReporter`), "Component code" (the base class `TrackedComponent` and that C# stays in code-behind), "Styling" (the workspace page has no stylesheet → the pages), and a new short section on the nested layout and `Components/Shared`.
- `docs/testing.md`: the example class names (`WorkspaceFacadeTests`, `WorkspaceTests`), `WorkspacePage`/`SettingsPage`, `OpenNewUsersWorkspaceAsync` and the sentence that a test "lands in their workspace".
- `docs/desktop.md`: the sign-in paragraph (`/workspace` becomes `/inbox`) and the Ctrl+K hand-check ("on the workspace" becomes "on the inbox page").
- `docs/architecture.md`, `docs/api.md`, `docs/observability.md`: none.

No decision departs from a guideline doc, except one convention it adds: a page may share its failure alert through the layout instead of owning one (D2). The doc's "Pages render a query through `Transition`" and "a part of the page with its own query shows its own loading and failure state" stay true.

## Risks / Trade-offs

- **`Navigation.NotFound()` on the desktop** → the router in `Main.razor` has `NotFoundPage` set, and the call is the same in a `BlazorWebView`, but it has only been exercised on the web. The by-hand desktop check includes opening an archive that has just been deleted from another window (see Migration Plan).
- **A page evaluated before the archive list is loaded** → a loading or failed list resolves nothing (D4); the `NotFound` result needs a loaded list that lacks the id.
- **Cached list on arrival** → covered by reading `CurrentState` in `OnParametersSetAsync` as well as `Success`; otherwise an address typed into an already-loaded app would never resolve.
- **Layout `OnParametersSetAsync` runs on every navigation** → the old page called `SetArgs` on every parameter set too, so repeating it with the same user id is existing behaviour; the sign-out watchers are registered in `OnInitialized`, once per layout instance, so they are not doubled.
- **Cascading failure state outlives a page change inside the layout** → `Inbox` and `Archive` call `Failures.Clear()` when they are initialized and when `ArchiveId` changes, so a refusal from one archive does not stay under the next.
- **`SidebarUser` relies on `FindUserById` having its args set by the page** → the layout sets them now, and the settings page (which shows the sidebar's user too) keeps whatever the cache holds; no behaviour change, noted so the task list does not drop it.
- **A large rename** → mitigated by doing it in one commit per concern and building after each: types and namespaces first, then resources, then tests.
- **Functional tests were failing before this change** (the last run recorded in `tests/**/TestResults` shows many failures, partly from the missing `workspace-heading` id). → The test tasks start by running the suite on the unchanged tree so a failure afterwards can be told from one already there.

## Migration Plan

No data, no API and no stored state change. The old addresses stop working, which is acceptable because nothing has been released.

1. Land `replace-dotnetquery-with-fetchling` first (its remaining tasks are verification).
2. Implement in the order of the task list; each group builds and passes its tests before the next.
3. By hand on the desktop (`mise run desktop`, Development): sign in as a user and see `/inbox`; open an archive and back, and see the sidebar entries stay; create and delete an archive in the open window and see the inbox; stdout shows `Signed in as user …` and the page console lines and no unhandled exception. Opening an archive that was deleted from the web window in the meantime shows the not-found page.
4. Rollback is a revert of the change; nothing persists across it.

## Open Questions

- Whether `DocumentErrors` should split into `ArchiveErrors` and `DocumentErrors`. The existing record handles both with one mapping and one `.resx`, and nothing in the specs depends on the answer; keep one record until a second reader of it needs less.
