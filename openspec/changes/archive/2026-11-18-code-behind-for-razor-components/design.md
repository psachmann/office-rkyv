# Design

## Context

See proposal.md. Today 17 components have an `@code` block and one has a `.razor.cs` (`src/OfficeRkyv.Desktop/Components/Main.razor.cs`, log methods only). The desktop doc already forbids `.razor.css` in the desktop project; that does not affect `.razor.cs`.

## Goals / Non-Goals

**Goals:**
- One stated rule, with a precise boundary between markup and code.
- Existing components follow it, so the docs describe what holds now.

**Non-Goals:**
- Changing component behavior, splitting components, or reworking markup.
- Forbidding inline expressions in markup (`@(...)`, `@if`, `@foreach`, event lambdas of one call). Those are markup logic.
- Enforcing the rule with an analyzer. Review and the doc carry it for now.

## Decisions

**D1. Code-behind partial class, not `@inherits` base class.** `Foo.razor.cs` declares `public partial class Foo` in the component's namespace. Alternative: a base class via `@inherits`. Rejected: it adds a type per component and hides the component's own members from the generator-visible partial.

**D2. What stays in `.razor`.** Directives and markup. `@inject` stays in the `.razor` file (visible next to the markup that uses it); a `[Inject]` property in code-behind is also acceptable for a component that already has a code-behind file. Pick `@inject` unless the injected member is needed by a source generator. Rationale: minimal churn, and the injected services read as the component's dependencies.

**D3. Namespace and accessibility.** The partial class uses the namespace the Razor compiler derives from the folder; keep `using`s in the `.cs` file. Members keep their current accessibility. `Main.razor.cs` stays as it is.

**D4. Migrate existing components in the same change.** Per CLAUDE.md the docs state conventions that hold now, so a rule that 16 files break would be untrue. Migration is mechanical (move the block's content, no edits to logic), done one component at a time with a build between.

**D5. Tiny blocks are not exempt.** Even a one-line `OnParametersSet` moves. A threshold would reintroduce per-author judgment, which is the problem.

**D6. Markup templates stay in `@code`.** A member that returns markup (`RenderFragment X => @<tag>...`) cannot be C#, so it cannot move to a `.cs` file. Two files have such members: `SearchDialog.Results`, and `Workspace.WorkspaceNavigation` and `Workspace.DocumentList`. They stay in the `.razor` file's `@code` block, and the rest of those files moves. Alternatives: a child component per template (splits the pages and changes their component tree), or inlining the markup where it is rendered (restructures two large pages). Both would widen a change that is meant to move code. The rule in `docs/ui.md` names this exception.

**D7. Analyzer warnings on moved code are reported, not fixed here.** Code in `.razor` files is generated and the analyzers skip it, so moving it into `.razor.cs` makes the analyzers see it for the first time. The build passes with 7 new warnings: `S3881` (Dispose pattern) on six components and `S5693` (HTTP content length) on `UploadDocumentsDialog`. Fixing them changes code beyond the move, so they are tracked in [TODOS.md](../../../../TODOS.md).

## Risks / Trade-offs

- [Members split from the markup that uses them] → names stay the same; the IDE navigates between the pair.
- [Move breaks a build, e.g. missing `using` the `.razor` imported via `_Imports.razor`] → build after each component; add explicit `using`s in the `.cs` file.
- [Doc states a rule nothing enforces] → accepted for now; the analyzer is a possible later change, to be added to TODOS.md if wanted.
- [Desktop experimental backend] → the move does not touch desktop workarounds; `Main.razor` is moved only if its `@code` block holds logic, and the workaround notes in `docs/desktop.md` are re-read first.
