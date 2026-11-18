# Proposal

## Why

Razor components in the UI, the web host and the desktop host mix markup with C# in `@code` blocks (16 of 17 components with logic do). Only `Main.razor.cs` uses a code-behind file, and only because the logging source generator does not see code written in a `.razor` file. There is no stated rule, so each new component picks its own style, and code in `@code` blocks gets weaker tooling support (analyzers, source generators, refactoring) than code in a `.cs` file.

## What Changes

- Document the rule in `docs/ui.md`: a component's C# lives in a `<Component>.razor.cs` partial class; `@code` blocks are avoided.
- State what stays in the `.razor` file: markup, directives (`@page`, `@inject`, `@using`, `@inherits`, `@attribute`) and inline expressions; code-behind holds members, parameters, lifecycle methods and handlers.
- Point `docs/observability.md` (log methods in a code-behind file) and `CLAUDE.md` (rules for every session) at the rule.
- Move the existing `@code` blocks into code-behind files so the docs hold now.
- No change in behavior, API or user-visible text.

## Capabilities

### New Capabilities

None. This is a code-structure convention, not system behavior, so the change sets `skip_specs: true`.

### Modified Capabilities

None.

## Impact

- `docs/ui.md`, `docs/observability.md`, `CLAUDE.md`.
- The 17 `.razor` files with `@code` blocks in `src/OfficeRkyv.UI`, `src/OfficeRkyv.Web` and `src/OfficeRkyv.Desktop` (each gains a `.razor.cs`).
- No dependency, API or database impact. Existing functional tests must still pass unchanged.
