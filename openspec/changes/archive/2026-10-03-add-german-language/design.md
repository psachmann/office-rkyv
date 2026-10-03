# Design

## Context

See proposal.md for the motivation and specs/ui-localization/spec.md for the requirements. What shapes the approach:

- `SupportedCultures.All` is the single list of shipped cultures. It feeds the web host's request localization, the `/culture/set` endpoint's check, the settings page's dropdown and the desktop host's matching. Today it is `[en]`.
- Resources are found by file placement: `Localization/Resources/<component path>/<Component>.resx`. A translation is the same file name with `.de.resx`; the SDK compiles those into a `de/` satellite assembly per project. No `.csproj` change is needed.
- A key missing from a translation does not fail: the lookup falls back to the neutral (English) resource. A missing *file* behaves the same way.
- The settings dropdown shows each culture's `NativeName`, so "Deutsch" appears without a resource text.
- Radzen's German satellite assembly is already in the build output (`de/Radzen.Blazor.resources.dll`), and `MainLayout` cascades the resolved culture to it.
- Existing unit tests use `de` and `de-DE` as their examples of an unsupported language (`SupportedCulturesMatchTests`, `DesktopSettingsStoreTests`).
- `SettingsTests.ChoosingALanguage_ReturnsToTheSettingsPage` only calls the culture endpoint, because with one language the dropdown could not trigger a change.

## Goals / Non-Goals

**Goals:**
- German is a complete, selectable language in the web UI and the desktop app.
- A text added to the English resources later cannot be forgotten in German without a test failing.
- The wording rules for German are written down once, so later texts match.

**Non-Goals:**
- Translating API-side text. The API sends codes, not text; that stays.
- Regional German variants with their own wording (`de-CH` without "ß", and so on). They resolve to `de`.
- Localizing dates, numbers or sorting beyond what the culture gives for free.
- A third language, or tooling for translators.
- Changing the default language.

## Decisions

### 1. The culture is neutral `de`, listed after `en`

`SupportedCultures.All` becomes `[en, de]`. `Default` stays `All[0]`, so English remains the default without further code.

Neutral `de` mirrors the existing neutral `en`. Regional variants reach it through parent matching: ASP.NET Core's providers fall back to the parent culture on the web, and `SupportedCultures.Match` walks up the parent chain on the desktop.

Alternative considered: `de-DE`. Rejected: `Match` only walks up, never down, so a browser or OS reporting `de-AT` or plain `de` would get English.

Verification: unit (`SupportedCulturesMatchTests`, `CultureResolutionTests`, `DesktopSettingsStoreTests`).

### 2. One `.de.resx` beside each English file

Eleven new files, each next to its English original with the same name and a `.de.resx` suffix: nine in `OfficeRkyv.UI`, two in `OfficeRkyv.Web`. The English files stay the neutral resources and are not renamed to `.en.resx`.

Alternative considered: one shared resource file per language. Rejected: it departs from the per-component convention in `docs/ui.md` for no gain at this size.

### 3. Wording rules and glossary for German

Confirmed with the user during exploration:

- Address the user with "du", in lower case: "du", "dein", "deines". Imperatives in the du-form: "Gib einen Namen ein."
- German quotation marks around names in running text: „{0}“.
- "Bitte versuche es erneut." is the standard retry sentence.
- The product name "Office Rkyv" is not translated.

| English | German |
|---|---|
| Inbox | Eingang |
| Archive | Archiv |
| Document | Dokument |
| User | Benutzer |
| Workspace | Arbeitsbereich |
| File into... | Ablegen in... |
| Settings | Einstellungen |
| Appearance | Darstellung |
| Language | Sprache |
| Email | E-Mail / E-Mail-Adresse |
| Display name | Anzeigename |
| Rename / Delete / Cancel | Umbenennen / Löschen / Abbrechen |

"Eingang" over "Posteingang": the latter suggests email; "Eingang" matches the paper tray where things arrive before they are filed ("ablegen").

Texts that carry markup (`Error.resx`: `DevModeDescription`, `DevModeWarning`) keep their `<strong>` elements, XML-escaped as in the English file. `ReconnectModal.resx`'s `RejoinFailedPrefix` and `RejoinFailedSuffix` are two halves around a number; the German halves keep the same split and the leading space of the suffix.

These rules go into `docs/ui.md`, since they are a convention later texts must follow.

### 4. A completeness test over the compiled resources

A unit test in `tests/OfficeRkyv.UnitTests` enumerates the embedded resources of the `OfficeRkyv.UI` and `OfficeRkyv.Web` assemblies. For each one and for each culture in `SupportedCultures.All` other than the default, it loads that culture's resource set without falling back to the parent, and asserts that:

- the set exists (the `.de.resx` file is there),
- its keys equal the English keys, in both directions,
- each value has the same `{n}` placeholders as the English value.

Working on the compiled assemblies, rather than parsing `.resx` files from the source tree, also proves the satellite assemblies are built and copied. Driving it from `SupportedCultures.All` means a third language is covered without touching the test.

Alternative considered: no test, relying on review. Rejected: the fallback to English makes a forgotten text invisible to every other test.

Verification: unit. It is a rule about values, with no host.

### 5. Existing tests that treat German as unsupported

- `SupportedCulturesMatchTests`: `de` and `de-DE` move from the "unsupported matches the default" cases to the "supported" cases, expecting `de`. `fr-FR` and `ja-JP` remain as the unsupported examples.
- `DesktopSettingsStoreTests`: the stored-but-unsupported example `de` becomes `fr`; the operating-system example `de-DE` moves out of "resolves to English" into a new case expecting German; a new case covers a stored `de` under an English operating system.
- `CultureResolutionTests`: new cases for `Accept-Language: de-DE` and for a German cookie over an English header.

Tests that construct their own culture lists, or use `de` only as a value passed through (`WebUserSettingsServiceTests`), are unaffected.

Verification: unit and UI logic, as those tests already are.

### 6. The language switch becomes a functional journey

`SettingsTests` gets a test that opens the settings page, picks "Deutsch" in the dropdown, and expects the German heading ("Einstellungen") and "Deutsch" as the selection; then picks "English" and expects the English heading again. It uses the same retry-until-interactive pattern `SettingsPage.ChooseAppearanceAsync` uses, in a new `ChooseLanguageAsync` on the page object.

The existing `ChoosingALanguage_ReturnsToTheSettingsPage` stays as the check of the endpoint's redirect and cookie; its comment, which says the dropdown offers only English, is corrected.

The other functional tests assert English text. They keep passing because Playwright's browser context reports `en-US` and each test starts without a culture cookie.

Verification: functional. It is what a person sees and does on a page.

### 7. Desktop

No desktop code changes. German reaches the desktop through `SupportedCultures`, and the resolution logic is covered by the unit tests in decision 5. That the running app actually shows German after choosing it, without a restart, is checked by hand.

### Observability

No effect on traces, metrics or logs. Log messages are not user-facing text and stay English.

### Guideline docs to update

- `docs/ui.md`, Localization: replace "Only English (`en`) ships today" with the two shipped languages; state that a new text needs an entry in every language's file and that the completeness test enforces it; add the German wording rules and glossary from decision 3.
- `docs/testing.md`: name the completeness test among the unit-test examples.

No decision departs from a guideline doc.

## Risks / Trade-offs

- [German text is longer and may wrap or clip in buttons, the sidebar and dialog titles] → Look at the workspace, the dialogs and the settings page in German during the by-hand check; shorten the wording before touching layout.
- [A developer with a German browser or OS now gets German during development without asking] → Intended behaviour; the settings page switches back, and the choice is remembered.
- [The translations are written without a native-speaker review step in the process] → The user reads German; the by-hand task includes reading the main pages. Wording fixes are resource-only edits.
- [The functional tests assume an English browser] → True today through Playwright's default locale. If a test ever needs to be independent of it, set the context's locale explicitly; not needed for this change.
- [The completeness test fails for a deliberate English-only text] → None exists today. If one appears, it still needs a German entry with the same value, which keeps the rule simple.
