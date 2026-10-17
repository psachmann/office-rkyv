# UI

The conventions of the shared Blazor UI (`src/OfficeRkyv.UI`) and of its web host (`src/OfficeRkyv.Web`): localization, how pages get their data, how failures become messages, user settings, styling and test ids. Read it before changing a page, a dialog, a facade or anything a user reads. The desktop host has its own doc, [desktop.md](desktop.md).

## Localization

The Blazor UI (a shared Razor class library, `src/OfficeRkyv.UI`, used by the web host `src/OfficeRkyv.Web`) sources all static text from `.resx` resources via `IStringLocalizer<T>` — never hardcode user-facing strings directly in `.razor` markup or `@code` blocks.

### Adding a new localized string

1. Find (or create) the resource file for the component that owns the text, in the project that owns the component: `Localization/Resources/<path mirroring the component's folder>/<ComponentName>.resx`. E.g. `src/OfficeRkyv.UI/Components/Pages/Home.razor` → `src/OfficeRkyv.UI/Localization/Resources/Components/Pages/Home.resx`. Web-host-only components (`ReconnectModal`, `Error`) keep theirs in `src/OfficeRkyv.Web/Localization/Resources/…`. This is the zero-config convention `ResourceManagerStringLocalizerFactory` follows for `AddLocalization(o => o.ResourcesPath = "Localization/Resources")` — no extra plumbing needed, it's just file placement.
2. Add the key/value to that `.resx` and to its translation next to it (`<ComponentName>.de.resx`).
3. Inject `@inject IStringLocalizer<ComponentName> Localizer` in the component (or the equivalent constructor injection in C#) and reference the string as `@Localizer["Key"]`.
4. If the string needs inline HTML formatting (e.g. a `<strong>` mid-sentence), store the markup in the resource value and render with `@((MarkupString)Localizer["Key"].Value)` — see `Error.razor` for an example. Only do this for our own trusted, app-authored resource strings, never for anything derived from user input.

English (`en`, the default and the neutral resources) and German (`de`) ship. `OfficeRkyv.UI.Localization.SupportedCultures` is the single source of truth for which cultures are supported — it drives both `RequestLocalizationOptions` and the language switcher on the settings page (`Settings.razor`), so they can never disagree. Adding a language is: drop in the new culture's `.resx` files (same names, `.{culture}.resx` suffix) and add the culture to `SupportedCultures.All`. Use the neutral culture (`de`, not `de-DE`): matching only walks up from a regional variant to its parent, so `de-AT` and `de-CH` find `de`, but a regional culture would never be found from the neutral one.

A text missing from a translation silently falls back to English, so the unit test `TranslationCompletenessTests` compares the compiled resources of `OfficeRkyv.UI` and `OfficeRkyv.Web` for every culture in `SupportedCultures.All`: each resource needs a translation file with exactly the English keys and the same `{0}`-style placeholders. A new text therefore needs an entry in the English file and in every translation file in the same change.

### German wording

- Address the reader with "du", always in lower case ("du", "dein"), and write instructions as du-imperatives ("Gib einen Namen ein."). The standard retry sentence is "Bitte versuche es erneut."
- Put names inside running text in German quotation marks: „{0}“.
- "Office Rkyv" is not translated.
- Use these terms, so the same thing is called the same everywhere:

| English | German |
|---|---|
| Inbox | Eingang |
| Archive | Archiv |
| Document | Dokument |
| User | Benutzer |
| Workspace | Arbeitsbereich |
| File into... | Ablegen in... |
| Home | Startseite |
| Settings | Einstellungen |
| Appearance | Darstellung |
| Language | Sprache |
| Email | E-Mail |
| Display name | Anzeigename |
| Rename / Delete / Cancel | Umbenennen / Löschen / Abbrechen |
| Sign in / Sign out | Anmelden / Abmelden |

### Culture resolution order (web host)

1. An explicit culture cookie (set by the language switcher), if present — always wins.
2. Otherwise, the browser's `Accept-Language` header, matched against `SupportedCultures.All`.
3. Otherwise, `SupportedCultures.Default` (English).

### Why changing language reloads the page (web host)

This is a Blazor Server app: each user has one persistent SignalR circuit, and that circuit's culture is fixed for its entire lifetime — SignalR interactions don't re-run the HTTP middleware pipeline that resolves culture. There is no way to hot-swap a live circuit's culture. The language switcher therefore navigates to `GET /culture/set?culture={code}&redirectUri={uri}`, which sets the culture cookie and redirects with `forceLoad: true`, forcing a real browser reload and a fresh circuit with the new culture. Don't try to "fix" this by making the switcher an in-place Blazor event — it can't work without a real navigation.

### Radzen's own built-in text

Radzen ships its own translations for several cultures (not English, which is its built-in default) via satellite resource assemblies. It resolves its own component text (grid/pager/date-picker chrome, etc.) from a `DefaultUICulture` cascading parameter, cascaded from `MainLayout.razor`, falling back to `CultureInfo.CurrentUICulture` if nothing is cascaded. You don't need to do anything extra for Radzen's own strings to follow the app's resolved culture — just don't remove that cascade.

## User settings

### `IUserSettingsService`: one interface, one implementation per host

The shared UI reads and changes appearance and language only through `IUserSettingsService` (`Current`, the replaying `IObservable<UserSettings> Settings`, `SetAppearanceAsync`, `SetCultureAsync`). The change notification is System.Reactive, not .NET events: subscribe to `Settings`, and keep the subscription in the component's disposables.

- **Web** (`WebUserSettingsService`): appearance is a cookie plus `appearance.js`; language is the culture cookie and a forced reload through `/culture/set` (see the localization section above).
- **Desktop** (`DesktopUserSettingsService`): both are kept in `settings.json`, and a change is saved, put in force and emitted. Appearance reaches the window through `GtkAppearance`; a language change is applied **in place**: the service sets the process culture, and the root component (`Main.razor`) rebuilds its router through a `@key` when the culture stream changes. The web reloads because a Blazor Server circuit's culture is fixed; a `BlazorWebView` has no such limit, so the desktop doesn't pay for a reload. Component state on the page is lost either way.

The appearance choice is `System`, `Light` or `Dark` (`AppearancePreference` in `src/OfficeRkyv.UI/Shared/Appearance.cs`). Both Radzen themes are always linked and a media query decides which applies, so switching never waits for a stylesheet.

## Signing in

A person says who they are by choosing themselves from the landing page: a card per user, and creating a user signs in as them. There is no secret in this version; passkeys are the intended successor (`TODOS.md`), and what is built here is the seam they plug into.

### One interface to read, one to act

- **Reading.** The UI reads the signed-in user only through Blazor's `AuthenticationStateProvider` and the cascaded `Task<AuthenticationState>` (`AddOfficeRkyvUi` adds `AddAuthorizationCore` and `AddCascadingAuthenticationState`). The user id is the `NameIdentifier` claim, read through `SignedInUser.IdOf(ClaimsPrincipal)` in `OfficeRkyv.UI.Auth`; `SignedInUser.PrincipalFor` makes the principal both hosts produce.
- **Acting.** Signing in and out goes through `ISignInService` (`SignInAsync(Guid userId)`, `SignOutAsync()`), which each host implements, as for `IUserSettingsService` and for the same reason: the web needs a real navigation to change a cookie, the desktop changes in place. Register an implementation in every host.
- **Routing.** Both routers (`Routes.razor`, `Main.razor`) use `AuthorizeRouteView`, whose `NotAuthorized` is `RedirectToLanding`. The workspace is `[Authorize]`; the landing page sends a signed-in visitor on to `/workspace` (in `OnParametersSetAsync`, so a sign-in made in place reaches it too); settings and not-found need no user. The workspace is `/workspace` and `/workspace/archives/{id}`: a user's id is never part of an address, and the old `/users/{id}` shows the not-found page.
- **The sidebar** shows the signed-in user's name and a sign-out button above Settings (`SidebarUser`, through `AuthorizeView`).
- **Every gRPC client carries the user.** `CallerTokenCredentials.AddTo(IHttpClientBuilder, key)` registers call credentials that issue a caller token per call, for the user in the `AuthenticationStateProvider` of the scope the client was resolved in (the circuit on the web, the window on the desktop; checked by `CallerTokenCredentialsTests`). Nobody signed in, no header, and the API refuses a call that needs a caller. Both hosts register their clients through it.

### Web: a cookie, set by an endpoint that redeems a ticket

A Blazor Server circuit cannot set a cookie, so a sign-in is a real request, as the language switch is (`/culture/set`). `WebSignInService` asks the singleton `SignInTickets` for a ticket (32 random bytes, base64url, valid 60 seconds, usable once, bound to sign-in or sign-out) and navigates with `forceLoad` to `/account/sign-in?ticket=…` or `/account/sign-out?ticket=…` (`AccountEndpoints`). The endpoint redeems it, sets or clears the cookie (ASP.NET Core cookie authentication: `HttpOnly`, `Secure`, `SameSite=Lax`, sliding, 14 days) and redirects to `/workspace` or `/`. A missing, used, expired or wrong-action ticket changes nothing and redirects to `/`. The ticket is what stops another site from signing a visitor in: only the app's own circuit can mint one. Because a sign-in is a reload, a circuit never outlives its user.

The cookie scheme's login redirect goes to `/`, not to its default `/Account/Login`: `[Authorize]` on a routable component makes the authorization middleware challenge a signed-out request for `/workspace` before Blazor renders it, and the answer is the landing page.

### Desktop

See [desktop.md](desktop.md#signing-in): the same two interfaces, implemented in memory.

### A sign-in the API stops accepting

`UNAUTHENTICATED` is `WorkspaceErrors.SignedOut`. A refusal that carries it, from a query or a mutation or a dialog, ends the sign-in (`WorkspaceErrors.EndSignInIfRefusedAsync` calls `ISignInService.SignOutAsync()`), and the landing page follows. `FindUserById` answering "no user" for the signed-in id does the same. The workspace never presents it as an empty inbox.

## Data: one facade per gRPC service

A page never calls a gRPC client. It injects a facade from `src/OfficeRkyv.UI/Shared` (`UsersFacade`, `ArchivesFacade`, `DocumentsFacade`), which wraps the service's client in DotNetQuery queries and mutations.

- **Queries are shared.** A facade exposes each query as a property and is registered scoped, so there is one per circuit (web) or per window (desktop), and every component showing the same key shares one cache entry.
- **Requests carry no owner.** The API acts for the caller (see [Signing in](#signing-in)). The workspace queries are still asked with the signed-in user's id, but only to key the cache, so that a desktop sign-out and sign-in as another user never serves the first user's lists.
- **Mutations are not shared.** A `Create…Mutation()` method returns a fresh instance that the calling component owns and disposes, so its state reports only what that component started.
- **Pages render a query through `Transition`** (from DotNetQuery.Blazor), which gives the loading, failure and success branches. A part of the page with its own query shows its own loading and failure state; an empty-list text is never shown for a list that did not load.
- **A mutation refreshes by kind, not by key.** Workspace queries are keyed `[kind, id]` through `WorkspaceKeys` (`ForArchives`, `ForInbox`, `ForArchiveDocuments`, and `ForSearch` for `[search, ownerId, query]`), and a mutation's `OnSuccess` calls `queryClient.Invalidate(WorkspaceKeys.Inbox, …)`, which marks every query of those kinds stale. `MutationOptions.InvalidateKeys` matches whole keys only, so it is used only where the key has one part (`UsersFacade`). Every document mutation refreshes the search kind too, since a change moves or renames what a search finds.
- **Results that settle later get a second refresh.** Deleting an archive returns its documents to the inbox after the call has answered (see [api.md](api.md)), so `ArchivesFacade` invalidates the inbox and the search again after `CascadeSettleDelay`.

### Search: state kept in the address

The workspace search (`Workspace.razor`, `DocumentsFacade.SearchDocuments`) keeps its query in the page address, `?q=`, read with `[SupplyParameterFromQuery]`. A state that a reload, the back button or a pasted link should keep belongs in the address; the page reacts to the address in `OnParametersSet`, as it does to the selected archive.

- **The address is the source of truth.** The field writes to it and the page reads from it. The field follows the address when something other than the field changed it (a navigation entry, back, a reload), tracked by what the address was last known to hold (`knownQuery`, `knownUserId`, `knownArchiveId`).
- **Typing is debounced, with `Subject.Throttle`** (System.Reactive, 300 ms): one search per pause, not one per key. Each keystroke carries a version, and an address change that did not come from the field bumps it, so typing that was still waiting is dropped and does not attach `?q=` to the page the visitor just opened.
- **The first query adds a history entry; every change after it replaces it** (`NavigationOptions.ReplaceHistoryEntry`), so back leaves the search instead of stepping through letters.
- **The page uses the server's word rule.** `SearchTerms` is in `OfficeRkyv.Contracts`; a query with no word the server would match shows a hint and sends nothing.
- **The field lives outside the list's `Transition`s**, so a refetch of a list never recreates it and takes the focus away while typing.

### Files: upload and download

A document is a file, so two operations on the facade are not queries or mutations.

- **`DocumentsFacade.UploadAsync`** streams one file to the API (a header, then 64 KB chunks) and reports the bytes sent through a callback. It is a method and not a DotNetQuery mutation because a mutation has no progress to report. It invalidates the inbox and archive-content queries when a document was created, and throws a refusal exactly as the typed client throws every refusal, so `WorkspaceErrors` explains it. The upload dialog (`UploadDocumentsDialog`) calls it once per file, one file after another, and shows each file's own state; a refused file does not stop the next.
- **`DocumentsFacade.OpenDownloadAsync`** returns the document's file as a forward-only `Stream`. The first message is read when it is opened, so a refusal (unknown or deleted document) is thrown there and not while saving.
- **Saving is the host's job: `IFileSaver`** (`OfficeRkyv.UI.Files`) takes a file name, a content type and a stream. The web host (`WebFileSaver`) hands the stream to the browser as a download; the desktop (`DesktopFileSaver`) writes it where the user chooses. It returns where the file went when the host can say, and the page shows that in a notification. A cancelled save writes nothing and throws nothing. Register an implementation in every host, as for `IUserSettingsService`.
- **A size limit is refused before sending.** `FileLimits` in `OfficeRkyv.Contracts` carries the default limit and the chunk size, so the dialog can say no quickly. The server's configured limit stays the authority.

What the UI library registers itself is in `AddOfficeRkyvUi` (Radzen, localization, authorization, the facades). The host adds what differs per host: DotNetQuery and its options, the gRPC clients and their addresses (each through `CallerTokenCredentials`), its `IUserSettingsService`, and who is signed in (an `AuthenticationStateProvider` and an `ISignInService`).

## Failures become localized messages

The API answers a refused call with a gRPC status and a stable code (see [api.md](api.md)). The UI chooses its text from the status code and that code, never from the server's own message, which is English-only and not written for users.

- One small record per form or page turns an exception into resource keys: `CreateUserErrors` and `WorkspaceErrors` in `src/OfficeRkyv.UI/Components/Pages/Users`. The component localizes the keys.
- A field violation is shown under its field; the first violation per field wins. A violation on `content` or `file_name` belongs to a file of the upload dialog (`WorkspaceErrors.File`), not to a form field. Everything else is one general message.
- An unknown code degrades to a generic text. It is never shown raw and never dropped.
- A refusal that means the page is out of date (`Aborted`, `NotFound`, a deleted archive or document) also asks for a reload of the affected lists (`WorkspaceErrors.ReloadNeeded`). Another user's archive or document is `NotFound` too, so it reads the same.
- `Unauthenticated` ends the sign-in instead of showing a message (`WorkspaceErrors.SignedOut`; see [Signing in](#a-sign-in-the-api-stops-accepting)).

Adding a code to `OfficeRkyv.Contracts` therefore needs a case in the matching record and a key in its `.resx`.

## Styling

Use Radzen components and Radzen's utility classes before writing CSS. The workspace page has no stylesheet of its own. The existing exceptions are the layout's scoped `MainLayout.razor.css` and the global `wwwroot/app.css`; add to them only what Radzen cannot express.

## Test ids

Give a `data-testid` only to an element that has no name of its own, such as a navigation entry, a count badge or a table row. Buttons, fields and dialogs are found by role and accessible name, as a person finds them, so they need a good accessible name instead. See [testing.md](testing.md) for how the functional tests use both.

## Decided in

- [add-ui-localization](../openspec/changes/archive/2026-09-22-add-ui-localization/design.md): resources per component, culture resolution, the reload on language change.
- [landing-page-user-management](../openspec/changes/archive/2026-09-24-landing-page-user-management/design.md) (D4): DotNetQuery queries and mutations rendered through `Transition`.
- [structured-error-codes](../openspec/changes/archive/2026-09-28-structured-error-codes/design.md) (D6): typed gRPC clients, messages chosen from codes.
- [add-archives-and-inbox](../openspec/changes/archive/2026-09-29-add-archives-and-inbox/design.md) (D8): one facade per service, invalidation by kind.
- [add-settings-page](../openspec/changes/archive/2026-09-29-add-settings-page/design.md): the appearance choice and the two linked themes.
- [add-linux-desktop-app](../openspec/changes/archive/2026-09-30-add-linux-desktop-app/design.md) (D2, D4): what lives in the UI library, and `IUserSettingsService`.
- [move-workspace-nav-into-sidebar](../openspec/changes/archive/2026-10-01-move-workspace-nav-into-sidebar/design.md) (D6, D7): a section's own loading and failure states, and no CSS for the workspace.
- [add-document-file-upload](../openspec/changes/archive/2026-10-10-add-document-file-upload/design.md) (D11, D12): uploading outside the mutation helper, and saving a file as the host's job.
- [add-document-title-search](../openspec/changes/archive/2026-10-17-add-document-title-search/design.md) (D5, D6): the search query kept in the address, typing debounced, and the search kind refreshed by every document change.
- [add-sign-in](../openspec/changes/archive/2026-10-17-add-sign-in/design.md) (D5, D6, D9 to D12): the sign-in seam, the web tickets and cookie, the token on every call, the landing cards, a sign-in the API stops accepting, the addresses.
