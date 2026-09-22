# Proposal

## Why

The Blazor UI currently has every user-facing string (page titles, nav labels, error text, Radzen built-in text) hardcoded in English directly in `.razor` markup. The app is still an early scaffold with only a handful of pages, which is the cheapest point to introduce a localization pattern — retrofitting it later would mean touching every page again. Establishing the pattern now, even while shipping English-only, means all UI built from this point forward follows it instead of accumulating more hardcoded strings.

## What Changes

- Add ASP.NET Core request localization (`IStringLocalizer`/`.resx`) to `OfficeRkyv.Web`, replacing hardcoded strings in `App.razor`, `MainLayout.razor`, `ReconnectModal.razor`, `Home.razor`, `Error.razor`, and `NotFound.razor`.
- Resolve the active culture per request in this order: cookie override (if present) → browser `Accept-Language` header → default culture (English).
- Persist an explicit user language choice in a cookie (`CookieRequestCultureProvider`), including the reconnect/circuit-restart handling that Blazor Server's cookie-based culture switching requires (culture is fixed for the lifetime of a circuit; changing it triggers a full page reload).
- Add a language switcher UI element in `MainLayout`'s header, visible even though only English is available at launch, so the mechanism is in place before a second language ships.
- Localize Radzen's own built-in component text (grid/pager/date-picker strings, etc.) via Radzen's localization hook, not just app-authored strings.
- Ship only `en` (English) resources at launch. No other language files are added in this change.

## Capabilities

### New Capabilities
- `ui-localization`: culture resolution (browser detection + cookie override), resource-based string lookup for app UI text, Radzen component localization, and a header language switcher.

### Modified Capabilities
(none — no existing specs)

## Impact

- **Affected project**: `src/OfficeRkyv.Web` only (`OfficeRkyv.Api` and gRPC contracts are untouched — no server-rendered or gRPC-sourced text is localized in this change).
- **New/changed files**: `Program.cs`/`Client.cs` startup (localization middleware registration), `Resources/` (resource files for existing pages/components), `MainLayout.razor` (switcher UI), a small circuit/cookie handling piece for Blazor Server culture switching, all five existing `.razor` files listed above (hardcoded strings replaced with localized lookups).
- **New dependency**: none required beyond built-in ASP.NET Core localization (`Microsoft.Extensions.Localization`, already part of the shared framework) and Radzen's existing localization API (already available via the installed `Radzen.Blazor` package).
- **No breaking changes**: default behavior (English) is unchanged for existing users; this only changes how the strings are sourced internally.
