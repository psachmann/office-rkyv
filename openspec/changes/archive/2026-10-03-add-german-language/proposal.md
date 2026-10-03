# Proposal

## Why

The UI was built to be localized, but only English ships, so the mechanism has never been exercised with a second language: the language switcher has one entry, and nothing proves that a choice actually changes what a person reads. German is the first language the app's users need besides English.

## What Changes

- Add German (`de`) as a supported UI language in both hosts. All 114 texts in the 11 resource files of `OfficeRkyv.UI` and `OfficeRkyv.Web` get a German translation.
- The language switcher on the settings page lists "English" and "Deutsch".
- A browser that prefers German, or a desktop whose operating system is set to German, gets the German UI without the user choosing anything. Regional variants (`de-DE`, `de-AT`, `de-CH`) resolve to German.
- English stays the default for every other language and when nothing is known.
- German text addresses the user with "du", written in lower case, and uses German quotation marks. The core terms are fixed in a glossary (see design.md).
- Add a check that every translation carries the same texts as the English original, so a text added later cannot silently stay English in the German UI.
- The settings journey can now be tested for real: choosing "Deutsch" reloads the page in German.

No breaking changes. An existing culture cookie or desktop settings file keeps working.

Guideline docs whose conventions this change sets or changes: `docs/ui.md` (Localization — which languages ship, how a translation is added, and the wording rules for German text) and `docs/testing.md` (the translation-completeness check as an example of a unit test).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ui-localization`: the set of shipped languages changes from English only to English and German. The requirement "Only Supported Cultures Are Offered Or Applied" changes (the switcher lists both), and two requirements are added: German is resolved and selectable in both hosts, and every shipped translation is complete.

## Impact

- `src/OfficeRkyv.UI`: `Localization/SupportedCultures.cs` gains `de`; 9 new `.de.resx` files under `Localization/Resources/`.
- `src/OfficeRkyv.Web`: 2 new `.de.resx` files (`ReconnectModal`, `Error`).
- `src/OfficeRkyv.Desktop`: no code change; it picks German up through `SupportedCultures`.
- `tests/OfficeRkyv.UnitTests`: `SupportedCulturesMatchTests` and `DesktopSettingsStoreTests` use `de` and `de-DE` as examples of unsupported languages and must change; `CultureResolutionTests` gains German cases; a new translation-completeness test.
- `tests/OfficeRkyv.FunctionalTests`: `SettingsTests` gains the language-switch journey.
- Build output: each host gains a `de/` satellite assembly for its own resources. Radzen's German satellite assembly is already shipped.
- No API, contract, storage or dependency change.
