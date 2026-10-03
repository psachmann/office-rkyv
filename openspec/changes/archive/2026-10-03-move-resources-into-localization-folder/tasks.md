# Tasks

## 1. Move the resource files

- [x] 1.1 `git mv src/OfficeRkyv.UI/Resources src/OfficeRkyv.UI/Localization/Resources`; verify `git status` shows 10 renames and `src/OfficeRkyv.UI/Resources` no longer exists
- [x] 1.2 `git mv src/OfficeRkyv.Web/Resources src/OfficeRkyv.Web/Localization/Resources`; verify `git status` shows 2 renames (`ReconnectModal.resx`, `Error.resx`) and `src/OfficeRkyv.Web/Resources` no longer exists

## 2. Point the code at the new folder

- [x] 2.1 Set `SupportedCultures.ResourcesPath` to `"Localization/Resources"` in `src/OfficeRkyv.UI/Localization/SupportedCultures.cs`; verify `grep -rn '"Resources"' src` finds nothing
- [x] 2.2 Update the UI-logic tests `CreateUserErrorsTests` and `WorkspaceErrorsTests`: change the `ResourceManager` base names to `OfficeRkyv.UI.Localization.Resources.Components.Pages.Users.…`; verify `grep -rn 'OfficeRkyv.UI.Resources' src tests` finds nothing

## 3. Update the guideline doc

- [x] 3.1 Update `docs/ui.md`, "Adding a new localized string" step 1: folder `Localization/Resources/<path mirroring the component's folder>/<ComponentName>.resx`, the `Home.resx` example path, the web host path `src/OfficeRkyv.Web/Localization/Resources/…`, and the quoted `ResourcesPath`; verify `grep -n 'Resources' docs/ui.md` shows only the new paths

## 4. Verify

- [x] 4.1 Remove the `bin` and `obj` folders of `OfficeRkyv.UI` and `OfficeRkyv.Web`, then run `just build`; verify it succeeds without warnings about resources
- [x] 4.2 Run `just test`; verify the UI-logic tests from 2.2 and the functional tests (`LandingPageTests`, `WorkspaceTests`, `SettingsTests`) pass. A failure in the settings sidebar functional test alone is the known flake: rerun before investigating
- [x] 4.3 By hand, web host: in the running web app open `/Error` and a URL that does not exist; verify both show English sentences and no bare resource keys
- [x] 4.4 By hand, desktop: run `just desktop`, open the home page and the settings page; verify headings and labels are English text and not resource keys, and that stdout shows no exception or error line while the pages load
- [x] 4.5 Run `just format`; verify it leaves no changes
