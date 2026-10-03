# Design

## Context

See proposal.md for the motivation. What shapes the approach:

- `AddOfficeRkyvUi` calls `AddLocalization(o => o.ResourcesPath = SupportedCultures.ResourcesPath)`. That is the only `AddLocalization` call; both hosts go through it. `LocalizationOptions.ResourcesPath` is one value for the whole process, so it applies to every assembly that owns a localized component: `OfficeRkyv.UI` and `OfficeRkyv.Web`.
- `ResourceManagerStringLocalizerFactory` builds the embedded resource name as `<assembly root namespace>.<ResourcesPath with separators turned into dots>.<type name without the root namespace>`. Neither project declares `.resx` items or `LogicalName`s in its `.csproj`; the embedded name comes from the folder path alone.
- A lookup that finds no resource does not throw. `IStringLocalizer` returns the key as the text. A wrong path therefore shows up as keys on screen (`PageTitle` instead of "Settings"), not as a build or startup failure.
- Two UI-logic tests (`CreateUserErrorsTests`, `WorkspaceErrorsTests`) open a `ResourceManager` on a hardcoded base name `OfficeRkyv.UI.Resources.Components.Pages.Users.…`.

## Goals / Non-Goals

**Goals:**
- All `.resx` files of a project live under that project's `Localization/` folder.
- Placement stays convention-only: a new component's resource file is found by where it is put, with no per-file configuration.

**Non-Goals:**
- Renaming, merging or re-keying resource files.
- Moving `SupportedCultures` or changing its namespace.
- Adding a culture.
- Giving the web host its own resources path.

## Decisions

### 1. Target folder is `Localization/Resources/`, not `Localization/` itself

Files go to `Localization/Resources/Components/…` and the resources path becomes `Localization/Resources`.

Alternative considered: resources path `Localization`, files at `Localization/Components/…`. Rejected: it mixes the `.resx` tree with the code in that folder (`SupportedCultures.cs`), and puts a second `Components/` folder directly under `Localization/` whose embedded names (`OfficeRkyv.UI.Localization.Components.…`) look like a code namespace. Keeping the `Resources` segment keeps "everything below here is `.resx`" true.

If the flatter layout is preferred, only the constant and the `git mv` targets change; nothing else in this design depends on the choice.

### 2. The web host follows the same relative path

`src/OfficeRkyv.Web/Resources/` moves to `src/OfficeRkyv.Web/Localization/Resources/`, although the web host has no localization code of its own.

Alternative considered: leave the web host's two files where they are and mark the assembly with `[ResourceLocation("Resources")]`. Rejected: it is per-assembly plumbing for two files, and `docs/ui.md` would have to explain two layouts.

### 3. Keep the path in `SupportedCultures.ResourcesPath`, written with a forward slash

The constant changes to `"Localization/Resources"`. The factory replaces both separator characters with dots, so the forward slash is correct on Linux and elsewhere.

### 4. Move with `git mv`, no `.csproj` changes

The SDK's default globs pick up `.resx` files wherever they are, and the embedded name follows the folder. `git mv` keeps the history of each file.

### 5. Verification

- **UI logic** (`CreateUserErrorsTests`, `WorkspaceErrorsTests`): the two `EveryKeyItCanProduce_ExistsInTheResourceFile` tests open the resource by embedded name. After updating the base name they prove the UI library's files are embedded under the new name. They do not go through `IStringLocalizer`, so they do not prove the configured path matches.
- **Functional** (`LandingPageTests`, `WorkspaceTests`, `SettingsTests`): these find elements by their rendered English text, through the real `IStringLocalizer` with the real path. A mismatch between the constant and the folder renders keys and fails them. This is the check for the UI library.
- **By hand, web host**: `ReconnectModal` and `Error` are the only resources in `OfficeRkyv.Web` and no functional test reads their text. Open the error page in the running web app and read the text.
- **By hand, desktop**: the desktop host loads the same UI assembly, so the functional result carries over; one look at the running app confirms it.

No new test is added. A test that compares embedded names against the constant would guard a convention that the functional tests already exercise on every run.

### Observability

No effect on traces, metrics or logs.

### Guideline docs to update

- `docs/ui.md` — "Adding a new localized string", step 1: the folder, the example path, the web host path and the quoted `AddLocalization` call.

No other guideline doc names the folder. No decision here departs from a guideline doc; the change replaces the convention in `docs/ui.md` and updates it in the same change.

## Risks / Trade-offs

- [A missed or mistyped path fails silently, as keys on screen] → The functional tests assert on rendered text; the web-host-only files get a by-hand check.
- [Stale `obj/` output keeps the old embedded names and hides a mistake, or embeds both] → Build clean once after the move.
- [`Localization/Resources/Components/Pages/Users/…` is a deeper path] → Accepted; it is the cost of one folder for all localization.
- [The archived changes under `openspec/changes/archive` still name `Resources/`] → Left as they are; they record what was decided then.
