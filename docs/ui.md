# UI

The conventions of the shared Blazor UI (`src/OfficeRkyv.UI`) and of its web host (`src/OfficeRkyv.Web`): localization, how pages get their data, how failures become messages, user settings, styling and test ids. Read it before changing a page, a dialog, a facade or anything a user reads. The desktop host has its own doc, [desktop.md](desktop.md).

## Localization

The Blazor UI (a shared Razor class library, `src/OfficeRkyv.UI`, used by the web host `src/OfficeRkyv.Web`) sources all static text from `.resx` resources via `IStringLocalizer<T>` — never hardcode user-facing strings directly in `.razor` markup or `@code` blocks.

### Adding a new localized string

1. Find (or create) the resource file for the component that owns the text, in the project that owns the component: `Localization/Resources/<path mirroring the component's folder>/<ComponentName>.resx`. E.g. `src/OfficeRkyv.UI/Components/Pages/Home.razor` → `src/OfficeRkyv.UI/Localization/Resources/Components/Pages/Home.resx`. Web-host-only components (`ReconnectModal`, `Error`) keep theirs in `src/OfficeRkyv.Web/Localization/Resources/…`. This is the zero-config convention `ResourceManagerStringLocalizerFactory` follows for `AddLocalization(o => o.ResourcesPath = "Localization/Resources")` — no extra plumbing needed, it's just file placement.
2. Add the key/value to that `.resx`.
3. Inject `@inject IStringLocalizer<ComponentName> Localizer` in the component (or the equivalent constructor injection in C#) and reference the string as `@Localizer["Key"]`.
4. If the string needs inline HTML formatting (e.g. a `<strong>` mid-sentence), store the markup in the resource value and render with `@((MarkupString)Localizer["Key"].Value)` — see `Error.razor` for an example. Only do this for our own trusted, app-authored resource strings, never for anything derived from user input.

Only English (`en`) ships today. `OfficeRkyv.UI.Localization.SupportedCultures` is the single source of truth for which cultures are supported — it drives both `RequestLocalizationOptions` and the language switcher on the settings page (`Settings.razor`), so they can never disagree. Adding a second language is: drop in the new culture's `.resx` files (same names, `.{culture}.resx` suffix) and add the culture to `SupportedCultures.All`.

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

## Data: one facade per gRPC service

A page never calls a gRPC client. It injects a facade from `src/OfficeRkyv.UI/Shared` (`UsersFacade`, `ArchivesFacade`, `DocumentsFacade`), which wraps the service's client in DotNetQuery queries and mutations.

- **Queries are shared.** A facade exposes each query as a property and is registered scoped, so there is one per circuit (web) or per window (desktop), and every component showing the same key shares one cache entry.
- **Mutations are not shared.** A `Create…Mutation()` method returns a fresh instance that the calling component owns and disposes, so its state reports only what that component started.
- **Pages render a query through `Transition`** (from DotNetQuery.Blazor), which gives the loading, failure and success branches. A part of the page with its own query shows its own loading and failure state; an empty-list text is never shown for a list that did not load.
- **A mutation refreshes by kind, not by key.** Workspace queries are keyed `[kind, id]` through `WorkspaceKeys` (`ForArchives`, `ForInbox`, `ForArchiveDocuments`), and a mutation's `OnSuccess` calls `queryClient.Invalidate(WorkspaceKeys.Inbox, …)`, which marks every query of those kinds stale. `MutationOptions.InvalidateKeys` matches whole keys only, so it is used only where the key has one part (`UsersFacade`).
- **Results that settle later get a second refresh.** Deleting an archive returns its documents to the inbox after the call has answered (see [api.md](api.md)), so `ArchivesFacade` invalidates the inbox again after `CascadeSettleDelay`.

What the UI library registers itself is in `AddOfficeRkyvUi` (Radzen, localization, the facades). The host adds what differs per host: DotNetQuery and its options, the gRPC clients and their addresses, and its `IUserSettingsService`.

## Failures become localized messages

The API answers a refused call with a gRPC status and a stable code (see [api.md](api.md)). The UI chooses its text from the status code and that code, never from the server's own message, which is English-only and not written for users.

- One small record per form or page turns an exception into resource keys: `CreateUserErrors` and `WorkspaceErrors` in `src/OfficeRkyv.UI/Components/Pages/Users`. The component localizes the keys.
- A field violation is shown under its field; the first violation per field wins. Everything else is one general message.
- An unknown code degrades to a generic text. It is never shown raw and never dropped.
- A refusal that means the page is out of date (`Aborted`, `NotFound`, a deleted archive or document) also asks for a reload of the affected lists (`WorkspaceErrors.ReloadNeeded`).

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
