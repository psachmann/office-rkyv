# Proposal

## Why

Localization in `src/OfficeRkyv.UI` is split across two top-level folders: `Localization/` holds the code (`SupportedCultures`) and `Resources/` holds the `.resx` files it serves. Putting the resources under `Localization/` keeps everything about localization in one place per project and leaves one fewer top-level folder beside `Components/`, `Settings/` and `Shared/`.

## What Changes

- Move the `.resx` tree of the UI library from `src/OfficeRkyv.UI/Resources/` to `src/OfficeRkyv.UI/Localization/Resources/`, keeping the sub-path that mirrors the component folders (`Components/Layout/…`, `Components/Pages/…`).
- Move the `.resx` tree of the web host from `src/OfficeRkyv.Web/Resources/` to `src/OfficeRkyv.Web/Localization/Resources/` in the same way. The resources path is one process-wide setting, so both projects must use the same relative folder.
- Change the configured resources path (`SupportedCultures.ResourcesPath`) from `Resources` to `Localization/Resources`.
- Update the two UI-logic tests that open a resource file by its embedded name.
- No user-visible change: every text, key and culture stays as it is. No resource file is renamed, added or removed.

Guideline docs whose conventions this change sets or changes: `docs/ui.md` (Localization — where a component's resource file lives). No other guideline doc is affected.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This is a file-layout refactor; `ui-localization` describes which text is localized and how the culture is resolved, and names no folder. The change sets `skip_specs: true`.

## Impact

- `src/OfficeRkyv.UI`: 10 `.resx` files move; `Localization/SupportedCultures.cs` changes one constant.
- `src/OfficeRkyv.Web`: 2 `.resx` files move (`ReconnectModal`, `Error`); a `Localization/` folder appears there for the first time.
- `src/OfficeRkyv.Desktop`: no file changes; it consumes the UI library's resources through `AddOfficeRkyvUi`.
- `tests/OfficeRkyv.UnitTests`: `CreateUserErrorsTests` and `WorkspaceErrorsTests` hardcode the embedded resource base name `OfficeRkyv.UI.Resources.…`.
- `docs/ui.md`: the "Adding a new localized string" steps name the old folder.
- Embedded resource names change (`OfficeRkyv.UI.Resources.*` → `OfficeRkyv.UI.Localization.Resources.*`). Nothing outside this repository reads them.
