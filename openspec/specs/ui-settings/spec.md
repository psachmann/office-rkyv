# ui-settings Specification

## Purpose

Gives the web UI one place for a visitor's UI preferences, currently the language and the appearance (following the system, or fixed light or dark), reachable from anywhere through the navigation sidebar.

## Requirements

### Requirement: The UI has a settings page
The web UI SHALL provide a settings page at `/settings` that shows the language control and the appearance control. No sign-in SHALL be required to view or use it. All static text on the page SHALL come from the UI's localized resources.

#### Scenario: Opening the settings page
- **WHEN** a visitor opens `/settings`
- **THEN** the page shows the language control and the appearance control with the choices System, Light and Dark

### Requirement: Settings are reachable from the navigation sidebar
The navigation sidebar SHALL contain a "Settings" entry that leads to the settings page. The entry SHALL be placed at the bottom of the sidebar, separate from the other navigation entries at the top, and SHALL stay there whatever page is shown. While the settings page is shown, the entry SHALL be marked as the current page.

#### Scenario: Navigating to settings
- **WHEN** a visitor chooses the Settings entry in the navigation sidebar
- **THEN** the browser shows the settings page, and the Settings entry is marked as the current page

#### Scenario: The entry sits at the bottom
- **WHEN** any page is shown with the navigation sidebar expanded
- **THEN** the Settings entry is at the bottom of the sidebar, below the other navigation entries and separated from them

### Requirement: The appearance can follow the system, or be fixed to light or dark
The settings page SHALL offer an appearance choice with three options: System, Light and Dark.
- System SHALL render the UI light or dark according to the operating system's color-scheme preference (`prefers-color-scheme`), and SHALL follow a change of that preference while a page is open, without a reload.
- Light and Dark SHALL render the UI in that appearance regardless of the operating system's preference.

Changing the choice SHALL take effect immediately, without a page reload, and SHALL apply to every page. Without a stored choice, the appearance SHALL be System.

#### Scenario: Default follows the operating system
- **WHEN** a visitor who has never chosen an appearance opens any page, and their operating system prefers dark
- **THEN** the UI renders in the dark appearance

#### Scenario: System follows an operating-system change live
- **WHEN** the appearance is System and the operating system's preference changes from light to dark while a page is open
- **THEN** the UI changes to the dark appearance without a page reload

#### Scenario: A fixed choice overrides the operating system
- **WHEN** a visitor whose operating system prefers dark chooses Light on the settings page
- **THEN** the UI changes to the light appearance without a page reload, and stays light

#### Scenario: The current choice is shown
- **WHEN** a visitor opens the settings page
- **THEN** the appearance control shows the visitor's current choice, or System if none was made

### Requirement: The appearance choice is remembered
The system SHALL persist the visitor's appearance choice per client, so that later page loads, including full reloads, later visits and restarts, render in the chosen appearance from the first paint, without first showing a different appearance. The web UI SHALL keep the choice in a cookie in the visitor's browser. The desktop app SHALL keep it in the user's settings file. A stored value the system does not recognize SHALL be treated as System.

#### Scenario: Reloading keeps a fixed choice
- **WHEN** a visitor has chosen Dark and then reloads the page
- **THEN** the page renders in the dark appearance from the first paint, whatever the operating system prefers

#### Scenario: Returning to System is remembered too
- **WHEN** a visitor switches from Dark back to System and then reloads the page
- **THEN** the page again follows the operating system's preference

#### Scenario: A tampered cookie falls back to System
- **WHEN** the appearance cookie holds a value that is not one of the three choices
- **THEN** the UI follows the operating system's preference, as for System

#### Scenario: The desktop app keeps the choice across restarts
- **WHEN** a user of the desktop app has chosen Dark and restarts the app
- **THEN** the app renders in the dark appearance from its first frame, whatever the desktop prefers
