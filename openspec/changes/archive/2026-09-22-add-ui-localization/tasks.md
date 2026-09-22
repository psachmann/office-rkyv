# Tasks

## 1. Localization Infrastructure

- [x] 1.1 Add `builder.Services.AddLocalization(o => o.ResourcesPath = "Resources")` in `src/OfficeRkyv.Web/Client.cs` and verify `dotnet build` succeeds.
- [x] 1.2 Configure `RequestLocalizationOptions` in `Client.cs` (`SupportedCultures`/`SupportedUICultures` = `["en"]`, `DefaultRequestCulture` = `en`, `RequestCultureProviders` = `[CookieRequestCultureProvider, AcceptLanguageHeaderRequestCultureProvider]` in that order) and call `app.UseRequestLocalization(...)` ahead of `UseAntiforgery`/component mapping; verify a request with no cookie and an `Accept-Language: fr` header still resolves to `en` (only supported culture), confirming the provider chain runs and falls back correctly.
- [x] 1.3 Cascade `CultureInfo.CurrentCulture` into `App.razor`'s `<html lang="...">` (replacing the hardcoded `"en"`) and verify the rendered HTML's `lang` attribute matches the resolved culture.

## 2. Culture Switching Endpoint & Cookie Persistence

- [x] 2.1 Add a minimal `GET /culture/set?culture={code}&redirectUri={uri}` endpoint in `Client.cs` that writes the culture cookie via `CookieRequestCultureProvider.MakeCookieValue` and redirects to `redirectUri`; verify by calling it directly (e.g. `curl -i`) and checking the response has a `Set-Cookie` header for the requested culture and a redirect status.
- [x] 2.2 Verify precedence end-to-end: a request carrying the culture cookie resolves to the cookie's culture even when its `Accept-Language` header requests a different supported culture (add a functional test once a second supported culture exists, or assert against the `RequestLocalizationOptions` provider order for `en` today).

## 3. Resource Extraction — App Shell & Layout

- [x] 3.1 Create `Resources/Components/Layout/MainLayout.resx` (nav labels "Home"/"Error", the `#blazor-error-ui` banner text and "Reload" link) and inject `IStringLocalizer<MainLayout>` into `MainLayout.razor`, replacing the hardcoded strings; verify by rendering the layout and confirming no literal English string remains in the `.razor` file outside attributes that aren't user-facing text (e.g. `Icon="home"`).
- [x] 3.2 Create `Resources/Components/Layout/ReconnectModal.resx` for all reconnect/pause/resume strings ("Rejoining the server...", "Rejoin failed...", "Retry", "The session has been paused...", "Resume", etc.) and inject `IStringLocalizer<ReconnectModal>`; verify no hardcoded English text remains in `ReconnectModal.razor`.

## 4. Resource Extraction — Pages

- [x] 4.1 Create `Resources/Components/Pages/Home.resx` (page title, "Hello, world!", "Welcome to your new app.") and inject `IStringLocalizer<Home>` into `Home.razor`; verify rendered output is unchanged in English and sourced from the resource file.
- [x] 4.2 Create `Resources/Components/Pages/Error.resx` covering the page title, headings, body paragraphs, the copy-to-clipboard tooltip, and both notification messages ("Copied"/"Request ID copied..." and "Copy failed"/"Could not copy...") and inject `IStringLocalizer<Error>` into `Error.razor`; verify no hardcoded English string remains, including inside the `@code` block's `NotificationMessage` calls.
- [x] 4.3 Create `Resources/Components/Pages/NotFound.resx` (page title, heading, body text, "Go to home" button) and inject `IStringLocalizer<NotFound>` into `NotFound.razor`; verify no hardcoded English string remains.

## 5. Header Language Switcher

- [x] 5.1 Add a static list of supported UI cultures (currently just English) accessible to `MainLayout`, and a `RadzenDropDown`-based switcher in `MainLayout`'s `RadzenHeader` showing the current culture and listing the supported ones; verify it renders in the header on every page that uses `MainLayout`.
- [x] 5.2 Wire the switcher's selection-changed handler to `NavigationManager.NavigateTo($"/culture/set?culture={{code}}&redirectUri={{currentUri}}", forceLoad: true)`; verify selecting a language triggers a full page reload (not a SignalR-only update) and the resulting page carries the new culture cookie.

## 6. Radzen Culture Cascade

- [x] 6.1 Add a `CascadingValue` for `DefaultUICulture` in `MainLayout.razor`, set from the request's resolved `CultureInfo.CurrentUICulture`, so descendant `RadzenComponent`s resolve their built-in text from it explicitly rather than only from ambient thread culture; verify a Radzen component under `MainLayout` reports the expected `UICulture` (e.g. via a temporary debug assertion or a functional test asserting the cascaded value).

## 7. Verification

- [x] 7.1 ~~Add~~ Verify a `ProjectReference` to `OfficeRkyv.Web` from `tests/OfficeRkyv.FunctionalTests/OfficeRkyv.FunctionalTests.csproj`; verify `dotnet build` succeeds for the test project. Correction: the reference was already present (the task description's "not currently present" was based on a mixed-up read of `FunctionalTests.csproj`/`UnitTests.csproj` during planning — it's actually `UnitTests.csproj` that has no project references). No edit needed here.
- [x] 7.2 Add a TUnit functional test that boots the app and asserts: (a) no cookie + unsupported `Accept-Language` resolves to `en`, (b) a culture cookie for `en` set alongside a conflicting `Accept-Language` header still resolves to `en` per the cookie; verify with `dotnet test`.
- [x] 7.3 Manually run the app (`dotnet run` / dev server), confirm each of the six updated views (`App`, `MainLayout`, `ReconnectModal`, `Home`, `Error`, `NotFound`) renders identical English text to before this change, and confirm the header language switcher is visible and clicking it round-trips through `/culture/set` with a full page reload.

## 8. Documentation

- [x] 8.1 Add `CLAUDE.md` at the project root documenting the localization conventions this change establishes — resource file layout (mirror the component path under `Resources/`, one `.resx` per component that owns text), how to add a new localized string (inject `IStringLocalizer<T>`, add the key to that component's `.resx`), the culture resolution order (cookie overrides `Accept-Language`, default `en`), that language changes require a full page reload via `/culture/set` (not a Blazor event) and why, the `DefaultUICulture` cascade Radzen components rely on, and that only English ships today even though the switcher and provider chain are in place; verify the file exists and accurately reflects what was implemented.
