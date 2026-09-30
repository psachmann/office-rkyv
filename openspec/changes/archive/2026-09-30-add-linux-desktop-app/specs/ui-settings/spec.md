# Spec Delta

## MODIFIED Requirements

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
