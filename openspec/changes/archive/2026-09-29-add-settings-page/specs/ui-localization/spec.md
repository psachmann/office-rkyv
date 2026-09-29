# Spec Delta

## MODIFIED Requirements

### Requirement: Persisted Language Selection
Selecting a language through the language switcher SHALL persist that choice in a cookie, so that subsequent requests use it without depending on the browser's `Accept-Language` header. After the choice is stored, the page the language was changed on SHALL reload in the selected culture.

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

### Requirement: Language Switcher Is Always Available
The settings page SHALL present a control for changing the UI language, regardless of how many languages are currently available. The header SHALL NOT contain a language control.

#### Scenario: Single supported language
- **WHEN** only one culture is supported by the system
- **THEN** the settings page still shows the language switcher control, listing that one culture as the current selection

#### Scenario: The header has no language control
- **WHEN** any page is shown
- **THEN** the header contains no language control, and the language is changed from the settings page
