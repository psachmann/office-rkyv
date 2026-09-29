# Proposal

## Why

The web UI's only user preference, the language, sits as a dropdown in the header, and the UI always renders in Radzen's light `material` theme. There is no place for preferences to live, and no way to switch to a dark theme. A settings page gives preferences one home as more of them arrive, and keeps the header free for app chrome.

## What Changes

- **Settings page** at `/settings`, holding the user's UI preferences. No sign-in is required, like the rest of the UI.
- **Appearance: System, Light or Dark**: the settings page offers a three-way appearance choice. "System" follows the operating system's light/dark preference (`prefers-color-scheme`), including when the OS setting changes while a page is open. "Light" and "Dark" fix the appearance regardless of the OS. The choice applies immediately, without a page reload, and is remembered in a cookie across reloads and visits. Without a stored choice, the appearance is "System". **Behavior change**: today the UI is always light, so a visitor whose OS prefers dark will now see the dark appearance by default.
- **Language moves to settings**: the language switcher leaves the header and becomes a control on the settings page. It behaves as today: choosing a language stores the culture cookie and reloads the page in that language, landing back on the settings page. **BREAKING** (UI only): the header no longer shows a language control.
- **Navigation**: the left navigation sidebar gets a "Settings" entry, pinned to the bottom of the sidebar, apart from the other entries at the top.

## Capabilities

### New Capabilities
- `ui-settings`: the settings page, how it is reached from the navigation sidebar, and the System/Light/Dark appearance preference and its persistence.

### Modified Capabilities
- `ui-localization`: the language switcher is on the settings page instead of the header, and choosing a language returns the user to the page they changed it on.

## Impact

- `src/OfficeRkyv.Web/Components/Layout/MainLayout.razor`: the header loses the language dropdown; the sidebar gains a bottom-pinned Settings entry.
- `src/OfficeRkyv.Web/Components/App.razor`: the hard-coded `<RadzenTheme Theme="material" />` is replaced by an app-owned theme component that renders the stylesheet(s) for the stored appearance preference, and a small JS module writes the preference cookie.
- New `Components/Pages/Settings.razor` with `Resources/Components/Pages/Settings.resx`.
- Tests: a unit test pinning the theme cookie and theme names, and a Playwright functional test for the settings page and the navigation entry.
- No API, contract, or data changes. No new packages: Radzen 11.5.1, already referenced, ships the light (`material`) and dark (`material-dark`) stylesheets.
- Out of scope: per-user server-side preferences (there is no authentication), themes other than Material light and dark, and adding languages beyond English.
