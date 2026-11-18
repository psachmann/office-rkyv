# Tasks

## 1. The rule in the docs

- [x] 1.1 Add a "Component code" section to `docs/ui.md` (rule, what stays in `.razor`, `Foo.razor.cs` partial class, no exemption for small blocks) and update the localization sentence that mentions `@code` blocks; verify by reading that the section matches design D1-D5.
- [x] 1.2 Update `docs/observability.md` (the code-behind line) to reference the rule, and add a one-line rule to `CLAUDE.md` "Rules for every session" linking `docs/ui.md#component-code`; verify the anchor resolves.

## 2. Move the `@code` blocks

- [x] 2.1 Move the blocks of `src/OfficeRkyv.UI/Components/Layout/` (`SidebarUser`, `HeaderSearch`, `AppTheme`, `MainLayout`, `SearchDialog`) and `src/OfficeRkyv.UI/Auth/RedirectToLanding` into `.razor.cs` partial classes; verify `mise run build` succeeds.
- [x] 2.2 Move the blocks of `src/OfficeRkyv.UI/Components/Pages/` (`Home`, `Settings`) and `Pages/Users/` (`CreateUserDialog`, `ArchiveDialog`, `DocumentDialog`, `UploadDocumentsDialog`, `Workspace`); the markup templates of `Workspace` stay in `@code` (design D6); verify `mise run build` succeeds.
- [x] 2.3 Move the blocks of `src/OfficeRkyv.Web/Components/` (`Pages/Error`, `Routes`, `App`); verify `mise run build` succeeds.
- [x] 2.4 Re-read `docs/desktop.md` workarounds, then move the `@code` block of `src/OfficeRkyv.Desktop/Components/Main.razor` into the existing `Main.razor.cs`; verify build and, if a desktop is available, `mise run desktop` starts and renders.
- [x] 2.5 Confirm only the markup templates remain in `@code` (design D6): `grep -rln "^@code" src --include=*.razor` lists only `SearchDialog.razor` and `Workspace.razor`, and each of their `@code` blocks holds only members that return markup.
