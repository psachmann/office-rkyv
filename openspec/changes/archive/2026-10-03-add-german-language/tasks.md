# Tasks

## 1. Translations

- [x] 1.1 Add `MainLayout.de.resx`, `Home.de.resx`, `NotFound.de.resx` and `Settings.de.resx` beside their English files in `src/OfficeRkyv.UI/Localization/Resources/Components/`, following the wording rules and glossary in design.md decision 3; verify each has the same `<data name>` keys as its English file
- [x] 1.2 Add `ArchiveDialog.de.resx`, `CreateUserDialog.de.resx`, `DocumentDialog.de.resx`, `Workspace.de.resx` and `WorkspaceErrors.de.resx` in `src/OfficeRkyv.UI/Localization/Resources/Components/Pages/Users/`; verify the keys match and every `{0}` placeholder is kept, with names in „…“
- [x] 1.3 Add `ReconnectModal.de.resx` and `Error.de.resx` in `src/OfficeRkyv.Web/Localization/Resources/Components/`; verify the `<strong>` markup in `DevModeDescription` and `DevModeWarning` is kept and XML-escaped, and `RejoinFailedSuffix` keeps its leading space
- [x] 1.4 Add `de` after `en` in `SupportedCultures.All`; verify `just build` succeeds without warnings and `de/OfficeRkyv.UI.resources.dll` and `de/OfficeRkyv.Web.resources.dll` exist in the Web project's output

## 2. Unit tests

- [x] 2.1 Add the translation-completeness test (unit level) in `tests/OfficeRkyv.UnitTests`: for every embedded resource of the UI and Web assemblies and every non-default culture in `SupportedCultures.All`, the culture's resource set exists without parent fallback, has the same keys as English, and the same `{n}` placeholders per value; verify it passes, and fails when a key is temporarily removed from one `.de.resx`
- [x] 2.2 Update `SupportedCulturesMatchTests` (unit level): `de` and `de-DE` are supported and match `de`, plus `de-AT` and `de-CH`; `fr-FR` and `ja-JP` stay the unsupported examples; verify the class passes
- [x] 2.3 Update `DesktopSettingsStoreTests` (unit level): the unsupported stored culture example becomes `fr`; an operating system set to `de-DE` resolves to German; a stored `de` under an English operating system stays German; verify the class passes
- [x] 2.4 Extend `CultureResolutionTests` (unit level): `Accept-Language: de-DE` with no cookie resolves to `de`; a German cookie with an English `Accept-Language` resolves to `de`; verify the class passes
- [x] 2.5 Run `dotnet test --project tests/OfficeRkyv.UnitTests`; verify all unit, pipeline and UI-logic tests pass

## 3. Functional test

- [x] 3.1 Add `ChooseLanguageAsync` to `SettingsPage` and a functional test in `SettingsTests`: choosing "Deutsch" reloads the settings page with the heading "Einstellungen" and "Deutsch" selected, and choosing "English" brings the English heading back; correct the outdated comment in `ChoosingALanguage_ReturnsToTheSettingsPage`
- [x] 3.2 Run the functional tests (only when no AppHost is already running); verify all pass, including the tests that assert English text. A failure of the settings sidebar test alone is the known flake: rerun before investigating

## 4. Guideline docs

- [x] 4.1 Update `docs/ui.md`, Localization: English and German ship; a new text needs an entry in each language's file, enforced by the completeness test; add the German wording rules and glossary; verify no sentence still says only English ships
- [x] 4.2 Update `docs/testing.md`: add the completeness test to the unit-level examples; verify the named test class exists

## 5. By hand

- [x] 5.1 Web host: in the running app choose "Deutsch" on the settings page, then read the landing page, a workspace (inbox, an archive, the create, rename and delete dialogs) and a URL that does not exist; verify all text is German, grid and pager text from Radzen is German, and nothing wraps or clips badly
- [x] 5.2 Desktop: run `just desktop`, choose "Deutsch" on the settings page; verify the page turns German without a restart, `~/.config/office-rkyv/settings.json` holds `"culture": "de"`, the app is still German after a restart, and stdout shows no exception or error line during the switch
- [x] 5.3 Run `just format`; verify it leaves no changes
