# Tasks

## 1. Appearance plumbing (design D1, D2, D3)

- [x] 1.1 Capture the head markup `<RadzenTheme Theme="material" />` renders today (from the running app's page source). Add `AppearancePreference`/`Appearance` (cookie name, `Parse`, stylesheet(s) per preference) and the non-interactive `AppTheme` component reproducing that markup for both themes, with the media query each gets (design D2). Replace `RadzenTheme` in `App.razor` with `<AppTheme Preference=… />`, fed from the cookie read off the cascaded `HttpContext`. Verify manually that the head markup matches the captured `RadzenTheme` markup apart from the second theme and the media attributes, that a Dark cookie applies only the dark stylesheet in the prerendered HTML and stays dark after the circuit connects, and that System applies each stylesheet by its media query. If `AppTheme` cannot reproduce `RadzenTheme`, stop and take the design's fallback back to the user.
- [x] 1.2 Add the scoped `AppearanceState` and `wwwroot/js/appearance.js` (`applyAppearance`: flips the theme links' media and writes the cookie), register the state in `Program.cs`, and start it from a `Routes` parameter fed by `App.razor` (design D3). Verify with the build.
- [x] 1.3 Add unit tests for `Appearance.Parse` (missing, each of the three values, and an unknown or tampered value, which gives System) and for the stylesheets and media each preference maps to. Verify they pass.

## 2. Settings page (design D3, D4)

- [x] 2.1 Add `Components/Pages/Settings.razor` at `/settings` with `Resources/Components/Pages/Settings.resx` (title, heading, language label, appearance label, System/Light/Dark option labels). Move the language `RadzenDropDown` and its `ChangeCulture` navigation from `MainLayout.razor` into the page with a visible label, and remove it from the header, keeping the `CultureInfo` cascade in `MainLayout`. Verify the build and that choosing English on `/settings` reloads `/settings`.
- [x] 2.2 Add the appearance `RadzenSelectBar` (System, Light, Dark) bound to `AppearanceState`, kept in sync via its change event and unsubscribed on dispose. Give the page's controls `data-testid`s. Verify manually that each choice changes the whole UI without a reload and without a visible unstyled flash, that System follows an OS light/dark change live, and that a reload keeps the choice.

## 3. Navigation (design D5)

- [x] 3.1 Split the sidebar into a top `RadzenPanelMenu` (Home, Greeter, Error) and a bottom-pinned `RadzenPanelMenu` holding a localized "Settings" item (`Icon="settings"`, `Path="/settings"`, key `NavSettings` in `MainLayout.resx`), with the flex-column layout and separator in `MainLayout.razor.css`. Verify manually that the entry sits at the bottom on a short and a long page, and is marked selected on `/settings`.

## 4. Verification

- [x] 4.1 Add a `SettingsPage` page object and Playwright functional tests, using `Page.EmulateMediaAsync` for the OS color scheme and asserting on which theme stylesheet applies (the links' media, and the resulting page background). Cover:
  - reaching settings from the sidebar entry, which is then selected;
  - the header having no language control, and the language control being on the settings page;
  - with no cookie and the OS emulated as dark, the page renders dark, and it turns light when the emulated OS changes to light without a reload;
  - choosing Dark with the OS emulated as light gives dark, and still dark after a reload;
  - switching back to System and reloading follows the emulated OS again.

  Verify they pass with `dotnet test` on the functional test project.
- [x] 4.2 Run the full unit test suite and `openspec validate add-settings-page --strict`. Verify both pass.
