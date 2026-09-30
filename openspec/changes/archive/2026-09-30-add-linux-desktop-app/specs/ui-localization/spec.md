# Spec Delta

## MODIFIED Requirements

### Requirement: Persisted Language Selection
Selecting a language through the language switcher SHALL persist that choice per client, so that later use applies it without depending on the browser's or the operating system's language. The web UI SHALL keep the choice in a cookie, so that subsequent requests use it without depending on the browser's `Accept-Language` header, and after the choice is stored, the page the language was changed on SHALL reload in the selected culture. The desktop app SHALL keep the choice in the user's settings file, and SHALL apply it to the running app without a restart, keeping the user on the page where the language was changed.

#### Scenario: User selects a language
- **WHEN** a user selects a language from the language switcher
- **THEN** a culture cookie recording that choice is set
- **AND** the current page reloads rendered in the selected culture

#### Scenario: Later request reuses the persisted choice
- **WHEN** a subsequent request is made after a language cookie was set
- **THEN** the UI renders in the cookie's culture even if the browser's `Accept-Language` header requests a different supported culture

#### Scenario: Changing the language from the settings page
- **WHEN** a user selects a language on the settings page
- **THEN** the browser reloads the settings page, rendered in the selected culture

#### Scenario: Changing the language in the desktop app
- **WHEN** a user of the desktop app selects a language on the settings page
- **THEN** the app shows the settings page in the selected culture without restarting, and uses that culture again after a restart

## ADDED Requirements

### Requirement: Desktop Language Detection
When the user has not chosen a language, the desktop app SHALL resolve its UI culture from the operating system's locale among the cultures it supports, matching a specific culture to its parent when the specific one is not supported (for example `en-US` to `en`), and SHALL fall back to English when neither matches. The language switcher SHALL show the resolved culture as its current selection.

#### Scenario: The operating system uses a regional variant
- **WHEN** the operating system's locale is `en-US` and only `en` is supported
- **THEN** the desktop app renders in English, and the language switcher shows English as selected

#### Scenario: The operating system uses an unsupported language
- **WHEN** the operating system's locale names a language the app has no resources for
- **THEN** the desktop app renders in English
