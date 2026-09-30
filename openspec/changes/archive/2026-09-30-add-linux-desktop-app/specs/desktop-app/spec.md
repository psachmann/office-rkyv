# Spec Delta

## Purpose

Provides office-rkyv as a standalone Linux desktop application: one program that holds the user's data and the service behind it, and shows the same UI as the web app in a native window, with no server to run and no browser to open.

## ADDED Requirements

### Requirement: The desktop app is standalone
The desktop app SHALL run as a single program on a Linux desktop, showing the UI in a native window and serving it from an API running inside the same program. It SHALL NOT require a separate server, a network connection, or a browser. The API SHALL only be reachable by the user running the app, not over the network.

#### Scenario: Starting the app
- **WHEN** a user starts the desktop app on a Linux desktop session
- **THEN** a window opens showing the app's landing page, and no other program has to be started first

#### Scenario: The API is private to the user
- **WHEN** the desktop app is running
- **THEN** its API accepts connections only through a socket in the user's private runtime directory, and listens on no network port

### Requirement: The desktop app offers the same workspace as the web app
The desktop app SHALL offer the same pages and features as the web UI: the user list and user creation, each user's workspace (inbox, archives, documents, filing, renaming and deleting), and the settings page, with the same behaviour, validation and localized messages as specified for the web UI.

#### Scenario: Working with documents on the desktop
- **WHEN** a user creates an archive and a document on the desktop, files the document into the archive, and then deletes the archive
- **THEN** the document returns to the inbox, as it does in the web app

### Requirement: The desktop app keeps the user's data
The desktop app SHALL store its data in the user's data directory (`$XDG_DATA_HOME/office-rkyv`, defaulting to `~/.local/share/office-rkyv`), and SHALL keep it across restarts.

#### Scenario: Data survives a restart
- **WHEN** a user creates a user and a document, closes the app, and starts it again
- **THEN** the user and the document are still there

### Requirement: Only one instance runs at a time
The desktop app SHALL run at most one instance per user. Starting it again while it is running SHALL NOT start a second copy of the API or open the data a second time.

#### Scenario: Starting it twice
- **WHEN** the desktop app is running and the user starts it again
- **THEN** no second API is started and the data is not opened a second time, and the already running instance keeps working

### Requirement: The desktop app shuts down cleanly
Closing the app's window, or sending the app a termination or interrupt signal, SHALL end the program after stopping the API, leaving no socket and no running process behind. A socket left behind by a crash SHALL NOT prevent the next start.

#### Scenario: Closing the window
- **WHEN** the user closes the app's window
- **THEN** the program exits, and its socket is removed

#### Scenario: A termination signal
- **WHEN** the running app receives a termination signal
- **THEN** it shuts down the same way as when its window is closed

#### Scenario: Starting after a crash
- **WHEN** the app was killed without shutting down, leaving its socket behind, and the user starts it again
- **THEN** the app starts normally

### Requirement: The desktop app follows the desktop's appearance
With the appearance set to System, the desktop app SHALL render light or dark according to the desktop's light/dark setting, including a setting published only through the XDG desktop portal, and SHALL follow a change of that setting while it runs, without a restart. A fixed Light or Dark choice SHALL override the desktop's setting. The window SHALL NOT show a different appearance before the chosen one, for example a white frame on a dark start.

#### Scenario: Dark desktop at start
- **WHEN** the desktop is set to dark, the appearance is System, and the user starts the app
- **THEN** the app renders dark from its first frame

#### Scenario: The desktop changes while the app runs
- **WHEN** the appearance is System and the user switches the desktop from dark to light while the app runs
- **THEN** the app turns light without a restart

#### Scenario: A fixed choice on a dark desktop
- **WHEN** the desktop is dark and the user has chosen Light in the app's settings
- **THEN** the app renders light, also after a restart

### Requirement: The desktop app remembers its settings in the user's config directory
The desktop app SHALL store the user's appearance and language choices in a settings file in the user's config directory (`$XDG_CONFIG_HOME/office-rkyv`, defaulting to `~/.config/office-rkyv`), and SHALL apply them at the next start. A missing, unreadable or invalid settings file SHALL be treated as no choice made, and SHALL NOT prevent the app from starting.

#### Scenario: Settings survive a restart
- **WHEN** a user chooses Dark on the settings page and restarts the app
- **THEN** the app starts dark, and the settings page shows Dark

#### Scenario: A broken settings file
- **WHEN** the settings file contains something the app cannot read
- **THEN** the app starts with the default settings
