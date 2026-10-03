# Spec Delta

## MODIFIED Requirements

### Requirement: Only Supported Cultures Are Offered Or Applied
The system SHALL only ever resolve to, or list in the language switcher, a culture whose resource files actually exist. The system ships English and German.

#### Scenario: Browser or cookie requests an unshipped culture
- **WHEN** a request's `Accept-Language` header or culture cookie names a culture the system has no resources for
- **THEN** the system falls back to English rather than erroring or rendering partially-localized content

#### Scenario: Switcher lists only shipped languages
- **WHEN** the language switcher is rendered
- **THEN** it lists English and German, each under its own name ("English", "Deutsch"), and no other language

## ADDED Requirements

### Requirement: The UI Is Available In German
The system SHALL offer its UI in German in both the web UI and the desktop app. German SHALL be resolved for the neutral culture `de` and for every regional variant of it (for example `de-DE`, `de-AT`, `de-CH`). When German is the resolved culture, the app's own static text SHALL be German.

#### Scenario: The browser prefers a regional variant of German
- **WHEN** a request has no culture cookie and its `Accept-Language` header's highest-preference supported culture is `de-DE`
- **THEN** the UI culture resolved for the request is German

#### Scenario: A German cookie overrides an English browser
- **WHEN** a request carries a culture cookie for German and an `Accept-Language` header favoring English
- **THEN** the UI culture resolved for the request is German

#### Scenario: Choosing German on the settings page
- **WHEN** a user whose UI is English selects "Deutsch" in the language switcher on the settings page
- **THEN** the settings page reloads with its heading and labels in German, and the language switcher shows "Deutsch" as selected

#### Scenario: Choosing English again
- **WHEN** a user whose UI is German selects "English" in the language switcher
- **THEN** the settings page reloads with its heading and labels in English

#### Scenario: The operating system is set to German
- **WHEN** the desktop app starts with no stored language choice and the operating system's locale is `de-DE`
- **THEN** the desktop app resolves its UI culture to German

#### Scenario: A stored German choice is kept by the desktop app
- **WHEN** the desktop app starts with a settings file that names German and an operating system set to English
- **THEN** the desktop app resolves its UI culture to German

### Requirement: Shipped Translations Are Complete
Every language the system ships besides English SHALL provide a translation for every static text the English UI has, so that no English text appears in a UI rendered in that language. A translation of a text with placeholders SHALL carry the same placeholders as the English text.

#### Scenario: Every English text has a German counterpart
- **WHEN** the texts of each resource of the UI library and of the web host are compared between English and German
- **THEN** the German resource exists and has exactly the same set of text keys as the English one

#### Scenario: Placeholders survive translation
- **WHEN** an English text contains a placeholder such as `{0}`
- **THEN** its German translation contains the same placeholders
