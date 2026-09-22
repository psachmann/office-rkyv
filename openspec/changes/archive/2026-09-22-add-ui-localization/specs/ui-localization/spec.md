# Spec Delta

## Purpose

Resolves which language the web UI renders in, from browser detection and an explicit user override, and ensures the app's own text and Radzen's built-in component text both follow that resolved language.

## ADDED Requirements

### Requirement: Default UI Language
When no culture cookie is present and the browser's `Accept-Language` header does not match a supported culture, the system SHALL render the UI in English.

#### Scenario: No cookie, no matching browser language
- **WHEN** a request has no culture cookie and its `Accept-Language` header names only cultures the system does not support
- **THEN** the UI renders in English

### Requirement: Browser Language Detection
When no explicit culture cookie is present, the system SHALL resolve the UI culture from the browser's `Accept-Language` header among the cultures it supports.

#### Scenario: No cookie, browser sends a supported language
- **WHEN** a request has no culture cookie and its `Accept-Language` header's highest-preference supported culture is a culture the system supports
- **THEN** the UI renders in that culture

### Requirement: Cookie Override Takes Precedence
When a culture cookie is present, the system SHALL use it instead of the browser's `Accept-Language` header, regardless of what the header requests.

#### Scenario: Cookie and browser language disagree
- **WHEN** a request carries a culture cookie for one supported culture and an `Accept-Language` header favoring a different culture
- **THEN** the UI renders in the culture named by the cookie

### Requirement: Persisted Language Selection
Selecting a language through the language switcher SHALL persist that choice in a cookie, so that subsequent requests use it without depending on the browser's `Accept-Language` header.

#### Scenario: User selects a language
- **WHEN** a user selects a language from the language switcher
- **THEN** a culture cookie recording that choice is set
- **AND** the current page reloads rendered in the selected culture

#### Scenario: Later request reuses the persisted choice
- **WHEN** a subsequent request is made after a language cookie was set
- **THEN** the UI renders in the cookie's culture even if the browser's `Accept-Language` header requests a different supported culture

### Requirement: Language Switcher Is Always Available
The header SHALL present a control for changing the UI language, regardless of how many languages are currently available.

#### Scenario: Single supported language
- **WHEN** only one culture is supported by the system
- **THEN** the header still shows the language switcher control, listing that one culture as the current selection

### Requirement: UI Text Is Sourced From Resources
All static UI text in the application shell, main layout, reconnect dialog, home page, error page, and not-found page SHALL be sourced from culture-specific resource files rather than hardcoded in markup.

#### Scenario: Rendering any of the covered views
- **WHEN** the application shell, main layout, reconnect dialog, home page, error page, or not-found page is rendered
- **THEN** every piece of static text displayed comes from a resource lookup for the resolved culture, with no hardcoded UI string remaining in that view's markup

### Requirement: Radzen Built-In Text Follows Resolved Culture
Text rendered by built-in Radzen components (for example grid, pager, and date-picker chrome) SHALL reflect the UI culture resolved for the request.

#### Scenario: Resolved culture has Radzen-provided translations
- **WHEN** the resolved UI culture is one Radzen ships built-in translations for
- **THEN** Radzen's own component text renders in that culture

#### Scenario: Resolved culture is English
- **WHEN** the resolved UI culture is English
- **THEN** Radzen's own component text renders in English

### Requirement: Only Supported Cultures Are Offered Or Applied
The system SHALL only ever resolve to, or list in the language switcher, a culture whose resource files actually exist. This change ships only English.

#### Scenario: Browser or cookie requests an unshipped culture
- **WHEN** a request's `Accept-Language` header or culture cookie names a culture the system has no resources for
- **THEN** the system falls back to English rather than erroring or rendering partially-localized content

#### Scenario: Switcher lists only shipped languages
- **WHEN** the language switcher is rendered
- **THEN** it lists only English
