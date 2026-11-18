# Proposal

## Why

`Workspace.razor` is one page with two routes: `/workspace` and `/workspace/archives/{id}`. A route parameter decides whether it shows the inbox or an archive, so the page carries both views' queries, mutations, header buttons and error state in 212 lines of markup and 300 of code-behind. Half of it applies to only one view (renaming and deleting an archive, the "new document" upload), and the parts both views share (the document grid, the document dialogs, the user gate) cannot be reused or tested on their own. The inbox and an archive are two different places in the product, so they should be two pages with the shared parts in components. The name "workspace" also stops carrying meaning once the page is gone, so it leaves the addresses, the code and the specs. Nothing has been released, so no old address needs to keep working.

## What Changes

- **Two pages replace the workspace page**: `/inbox` shows the signed-in user's unfiled documents, and `/archives/{id}` shows the documents of one archive. `/workspace` and `/workspace/archives/{id}` no longer exist and show the not-found page, like `/users/{id}`. **BREAKING** (addresses; nothing has been released).
- **Signing in lands on `/inbox`**: the landing page, the web sign-in endpoint and the desktop sign-in send a signed-in visitor there.
- **An archive address that does not resolve shows not-found**: an id that is not one of the signed-in user's live archives. Until now the page silently showed the inbox. An archive that disappears while it is open, or that the user deletes, still returns to `/inbox`.
- **The new-document action is on the inbox page only.** An upload always lands in the inbox, so the button next to an archive's name suggested the wrong thing. Uploading straight into an archive is added to `TODOS.md`.
- **A nested layout** holds what both pages share: the sidebar entries (inbox with its count, the archives, new archive), the check that the signed-in user still exists, and the one place a refusal is shown. The sidebar stays in place when the user moves between the inbox and an archive, which it would not if each page registered its own sidebar content.
- **Shared components move to `Components/Shared`**: the document list with its row actions (download, rename, file into, delete) and their mutations, the three dialogs (`UploadDocumentsDialog`, `DocumentDialog`, `ArchiveDialog`), the sidebar entries, and the small loading and failure pieces now repeated around every `Transition`.
- **"Workspace" leaves the code**: `WorkspaceAddresses` becomes `Addresses`, `WorkspaceKeys` becomes `QueryKinds` and `WorkspaceErrors` becomes `DocumentErrors`; `Components/Pages/Users` is dissolved (`CreateUserDialog` goes with the landing page); the test ids `workspace-selection` and `workspace-error` and the `WorkspacePage` / `WorkspaceTests` test types are renamed. The German glossary row "Workspace / Arbeitsbereich" is dropped.
- **The pages are headed by what they show**: "Inbox" or the archive's name. The signed-in user's name is in the sidebar. The old specs said the workspace was "headed with that user's display name"; no markup ever did, and the test page object waits for a `workspace-heading` test id that nothing renders. The specs and the page object are corrected to what the UI does.
- The user-facing texts move with their components into resource files that mirror the new folders, in English and German. Their wording does not change, except where a text names the workspace.

Not in this change: uploading into an archive, a bulk action, any change to the API, the facades' queries or the cache keys' values, or to the search dialog beyond the addresses it navigates to.

Guideline docs whose conventions this change sets or changes:

- `docs/ui.md`: the component folders (`Pages`, `Layout`, `Shared`), the nested layout as the owner of what two pages share, the addresses, "Signing in" (routing, sidebar, the address list), the renamed types and the removal of `Pages/Users`, the German glossary.
- `docs/testing.md`: the renamed test and page-object types, and the sign-in landing on the inbox.
- `docs/desktop.md`: the sign-in sends the visitor to `/inbox`, and the by-hand check now names the inbox page.
- `docs/api.md`, `docs/architecture.md`, `docs/observability.md`: none. No RPC, project dependency, log or telemetry changes.

## Capabilities

### New Capabilities

- `inbox-page`: the `/inbox` page: its documents, the empty state, uploading new documents, and the old workspace addresses.
- `archive-page`: the `/archives/{id}` page: an archive's documents, an address that does not resolve, renaming and deleting the archive, what happens when the archive goes away.
- `document-actions`: what a user can do with a listed document on either page (download, rename, file into an archive or back to the inbox, delete) and how a refused action is explained.
- `sidebar-navigation`: the inbox and archive entries in the navigation sidebar, the count, creating an archive, and where the entries appear.

### Modified Capabilities

- `user-workspace`: every requirement is removed. Each moves to one of the four capabilities above, with its wording changed from "workspace" to the page it now belongs to. The capability ends with no requirements.
- `sign-in`: the page a sign-in opens is `/inbox`; the addresses that need a signed-in user are `/inbox` and `/archives/{id}`; the old workspace addresses are not found.
- `user-landing-page`: opening a user, or creating one, shows `/inbox`.
- `header-search`: a result goes to the inbox or archive page; the scenarios that name the workspace name the pages.
- `desktop-app`: the signed-in user's inbox and archives replace "workspace" in the feature list and in the sign-in scenario.

## Impact

- `src/OfficeRkyv.UI`: `Components/Pages/Users/*` replaced by `Pages/Inbox`, `Pages/Archive`, a nested layout in `Components/Layout`, and the components in `Components/Shared`; `Localization/Resources/…` files re-homed to match, `Workspace.resx` split by owner; `Shared/WorkspaceAddresses`, `WorkspaceKeys` renamed; `Components/Pages/Home.razor.cs`, `Components/Layout/SearchDialog.razor.cs`, `Components/Layout/MainLayout.razor` (the section outlet's id) and `Auth/ISignInService.cs` updated for the new addresses and names.
- `src/OfficeRkyv.Web`: the sign-in endpoint redirects to `/inbox`; the router comments name the new pages.
- `src/OfficeRkyv.Desktop`: the router comment; the sign-in's behaviour comes from the landing page, so no logic changes.
- `src/OfficeRkyv.Api`, `src/OfficeRkyv.Contracts`: none.
- `tests/OfficeRkyv.UnitTests`: `WorkspaceAddressesTests`, `WorkspaceErrorsTests`, `WorkspaceFacadeTests` and `WorkspaceErrorsOverTheWireTests` renamed with the types they cover; new UI-logic tests for what the pages decide; `TranslationCompletenessTests` covers the moved resources.
- `tests/OfficeRkyv.FunctionalTests`: `WorkspacePage` and `WorkspaceTests` renamed and reworked to the two pages; the other test classes that open "the workspace" follow; new journeys for the old addresses, an unknown archive id and the sidebar staying put.
- By hand: the desktop window shows `/inbox` after a sign-in, the sidebar stays when switching between pages, and an archive deleted in the open window returns to the inbox.
- `docs/ui.md`, `docs/testing.md`, `docs/desktop.md`, `TODOS.md`, and the five specs above.
- No new dependency, no stored data, no telemetry change.
- **Order of work**: `replace-dotnetquery-with-fetchling` is in progress and touches `Components/_Imports.razor` and the facade tests; its remaining tasks (4.1 to 4.4) are verification only. Land it first.

## Assumptions made

- **The old addresses are plain not-found**, with no redirect: the router has no route for them, so no code is needed and a test pins it.
- **The error alert is cascading state owned by the nested layout.** The page-level `errorKey` field is replaced by a small cascaded service that the pages and `DocumentList` call; the alert is shown above the page body.
- **`WorkspaceErrors` is renamed `DocumentErrors`** rather than just dropping the prefix, since "Errors" alone says nothing. It explains refusals of documents, archives and the signed-out case alike, and stays one record (see the design's open question).
