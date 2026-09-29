# Design

## Context

- `MainLayout.razor` has a `RadzenHeader` holding the sidebar toggle, the app title and a `RadzenDropDown` language switcher. Choosing a language navigates to `GET /culture/set?culture=…&redirectUri=…` with `forceLoad: true` (see CLAUDE.md: a Blazor Server circuit's culture is fixed, so a real reload is required). The sidebar is a `RadzenSidebar` with one `RadzenPanelMenu` (Home, Greeter, Error).
- `App.razor` renders statically, and only `Routes` and `HeadOutlet` are interactive. The theme is hard-coded as `<RadzenTheme Theme="material" />` in the head.
- Radzen.Blazor 11.5.1 ships the light and dark Material stylesheets as static assets (`_content/Radzen.Blazor/css/material-base.css`, `material-dark-base.css`, plus WCAG variants). `RadzenTheme` renders the stylesheet for one named theme. Its `ThemeService` is used only by `RadzenTheme` and `RadzenAppearanceToggle`, and Radzen's `CookieThemeService` reads its cookie through JS interop, which only runs once the circuit is up.
- The UI is Blazor Server with prerendering. All interactive root components on a page share one circuit and one DI scope.

## Goals / Non-Goals

**Goals:**
- A `/settings` page with the language control (moved from the header) and a System/Light/Dark appearance choice.
- System follows the OS `prefers-color-scheme`, including live changes of the OS setting.
- Every choice applies live, with no reload, and a stored choice renders correctly from the first paint of every later load, with no flash of the other appearance.
- A Settings entry pinned to the bottom of the sidebar.

**Non-Goals:**
- Server-side, per-user preferences. There is no authentication, so preferences are per browser (cookies).
- Changing how the language is applied. The reload through `/culture/set` stays as it is.
- Themes other than Material light and dark.

## Decisions

### D1. The preference is `system`, `light` or `dark`; the themes are `material` and `material-dark`

An `AppearancePreference` enum (`System`, `Light`, `Dark`) plus a small `Appearance` helper in `Web/Shared` holds:
- the cookie name and its stored values (`system`, `light`, `dark`);
- `Parse(string? stored)`, which maps anything missing, unknown or tampered to `System`, because the cookie is client-controlled input;
- `Stylesheets(preference)`, the two theme stylesheets and the media query each one gets (D2).

The preference, not a theme name, is what gets stored, because System is not a theme.

### D2. An app-owned, non-interactive `AppTheme` replaces `RadzenTheme`; the media query picks the theme

The server cannot know `prefers-color-scheme` on the first request, since only some browsers send it as a client hint, and only after opting in. So the choice between light and dark is made by the browser, through the media query on each stylesheet link. `AppTheme` always renders **both** themes' stylesheets, and the preference only decides their media:

| Preference | `material` (light) | `material-dark` |
|---|---|---|
| System | `(prefers-color-scheme: light)` | `(prefers-color-scheme: dark)` |
| Light | applies always | `not all` (off) |
| Dark | `not all` (off) | applies always |

- For **System**, the browser applies the matching sheet at first paint, and switches by itself when the OS setting changes, with no script and no circuit involved.
- For **Light** and **Dark**, the switched-off sheet is still downloaded (browsers fetch non-matching stylesheets at low priority), so it is already there when the choice changes.

`App.razor` reads the preference cookie from its cascaded `HttpContext` during the static render, parses it with `Appearance.Parse`, and renders `<AppTheme Preference="@preference" />` in place of `<RadzenTheme Theme="material" />`. The links are marked `data-theme`, and `AppTheme` reproduces what `RadzenTheme` rendered (the icon-font preload, then the stylesheet link; captured from the running app in task 1.1), only with both themes and the media attributes.

**`AppTheme` is deliberately not interactive.** Measured in a browser: an interactive component in the head is thrown away and re-created when the circuit starts, so the browser re-fetches the stylesheets and the page renders unstyled for a moment on every load, visibly so on a slow connection. A static component is rendered once and never touched. The same measurement showed that adding and removing a `<link>` on a live switch leaves an unstyled gap while the new sheet downloads, which is why the switched-off sheet stays linked and only its media changes.

*Alternative:* keep `RadzenTheme`, and resolve System through JS `matchMedia` after the circuit starts. Rejected, because a dark-OS visitor in System mode would see a light first paint on every load, which is exactly the flash the spec forbids.
*Alternative:* `Sec-CH-Prefers-Color-Scheme` client hints. Rejected, because they are Chromium-only and missing on the first request.
*Alternative:* link only the active sheet for Light and Dark. Rejected, because of the unstyled gap on a live switch. The cost of linking both is that a visitor on a fixed choice also downloads the other theme's stylesheet (about 760 KB uncompressed each; System visitors already download both).

### D3. Live switching and persistence go through a scoped `AppearanceState` and one JS function

A scoped `AppearanceState` (one per circuit) holds the current preference and a change event.
- The preference the page was rendered with reaches the circuit as a parameter of `Routes` (`<Routes @rendermode="InteractiveServer" Appearance="@preference" />`), which starts the state. `Routes` is the first interactive root, so the settings page shows the right choice however it is reached, including by in-app navigation, where it is not prerendered.
- The settings page shows it through a `RadzenSelectBar` with three localized options (System, Light, Dark), which reads as a row of buttons. A selection calls `AppearanceState.SetAsync(preference)`.
- `SetAsync` updates the state and raises the event, then calls `applyAppearance` in a small JS module (`wwwroot/js/appearance.js`). It sets `link.media` on the `data-theme` links to the values from `Appearance.Stylesheets(preference)`, which are passed in from C# so the mapping has one source of truth, and writes the cookie: one year, `SameSite=Lax`, `path=/`.

*Alternative:* a `/theme/set` endpoint and a reload, mirroring `/culture/set`. Rejected, because an appearance change, unlike a culture change, can be applied live.

### D4. The language control moves unchanged into the settings page

The `RadzenDropDown` bound to `SupportedCultures.All` and its `ChangeCulture` navigation move from `MainLayout.razor` into `Settings.razor`, and get a visible localized label. The redirect URI is still the current page, which on the settings page is `/settings`, so the reload lands back on settings. `/culture/set` and `SupportedCultures` do not change. The header keeps the sidebar toggle and the app title. The `CascadingValue` of `CultureInfo.CurrentUICulture` for Radzen's own text stays in `MainLayout` (CLAUDE.md).

### D5. Settings pinned to the bottom of the sidebar

The sidebar content becomes a full-height flex column containing two `RadzenPanelMenu`s: the existing one (Home, Greeter, Error) at the top, and a second one holding only `RadzenPanelMenuItem Text="Settings" Icon="settings" Path="/settings"` at the bottom (`margin-top: auto`, with a top border as the separator). The styles go in `MainLayout.razor.css` using `::deep`, because the elements belong to Radzen components. `RadzenPanelMenuItem` already marks the item for the current path as selected, which gives "marked as the current page" without extra code.

## Risks / Trade-offs

- [Both stylesheets are downloaded whatever the choice] → The price of a live switch with no unstyled gap (D2). System visitors already paid it. The sheets are static and revalidated by the browser, so it is a first-visit cost.
- [A visitor switches very quickly after the page loads, before the low-priority download of the switched-off sheet has finished] → The switch then waits for that download, as the browser would anyway; it is not worse than adding a link. Accepted.
- [The default changes from light to System] → Intended, and stated in the proposal. Dark-OS visitors see dark by default.
- [Moving the language control is a visible UI change] → Intended; the spec delta for `ui-localization` records it.
- [A cookie per preference] → Two small cookies (culture, appearance) are acceptable. Merging them would couple independent preferences for no gain.
