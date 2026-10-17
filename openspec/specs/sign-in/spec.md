# sign-in Specification

## Purpose
Lets a person say who they are before working with documents, and keeps the app's pages and the API's answers to that one signed-in user, on the web and on the desktop. In this version a person signs in by choosing themselves from the list of users, with no password.

## Requirements

### Requirement: Choosing a user signs in as that user
The landing page SHALL sign the visitor in as the user they choose and SHALL then show that user's workspace. Signing in SHALL NOT ask for a password or any other secret.

#### Scenario: Signing in from the landing page
- **WHEN** a signed-out visitor chooses Alice on the landing page
- **THEN** the workspace opens at `/workspace`, headed with Alice's display name and showing her inbox and archives

#### Scenario: Two browsers, two users
- **WHEN** one browser signs in as Alice and another signs in as Bob
- **THEN** each browser shows its own user's workspace and none of the other user's documents

### Requirement: Creating a user signs in as the new user
When a user is created from the landing page, the visitor SHALL be signed in as the new user and SHALL be shown the new user's workspace, without choosing the user from the list first.

#### Scenario: Creating the first user
- **WHEN** a visitor creates a user `Alice` on an empty landing page
- **THEN** the workspace opens at `/workspace`, headed `Alice`, with an empty inbox

### Requirement: The signed-in user is shown and can sign out
While a user is signed in, the navigation sidebar SHALL show that user's display name and SHALL offer a sign-out action. Signing out SHALL end the sign-in and SHALL show the landing page. While nobody is signed in, the sidebar SHALL show neither.

#### Scenario: Signing out
- **WHEN** Alice is signed in and chooses sign-out in the sidebar
- **THEN** the landing page is shown, and opening `/workspace` afterwards shows the landing page again instead of Alice's workspace

#### Scenario: Nobody signed in
- **WHEN** a signed-out visitor opens the landing page
- **THEN** the sidebar shows no user name and no sign-out action

### Requirement: Pages follow the sign-in state
A signed-out visitor who opens a workspace address (`/workspace` or `/workspace/archives/{id}`) SHALL be shown the landing page. A signed-in visitor who opens the landing page (`/`) SHALL be shown their workspace. The settings page SHALL be available whether or not a user is signed in.

#### Scenario: A workspace address while signed out
- **WHEN** a signed-out visitor opens `/workspace`
- **THEN** the landing page is shown

#### Scenario: The landing page while signed in
- **WHEN** Alice is signed in and opens `/`
- **THEN** Alice's workspace is shown at `/workspace`

#### Scenario: Settings while signed out
- **WHEN** a signed-out visitor opens `/settings`
- **THEN** the settings page is shown

### Requirement: How long a sign-in lasts
In the web UI, a sign-in SHALL last across page reloads and new tabs of the same browser until the visitor signs out. In the desktop app, a sign-in SHALL last while the app runs, and the app SHALL start signed out.

#### Scenario: Reloading the workspace
- **WHEN** Alice is signed in on the web and reloads `/workspace`
- **THEN** Alice's workspace is shown again without signing in

#### Scenario: Restarting the desktop app
- **WHEN** Alice signed in on the desktop, and the app is closed and started again
- **THEN** the landing page is shown and nobody is signed in

### Requirement: A sign-in that the API no longer accepts ends
When the API refuses a call because it does not accept the signed-in user (for example, the user has been deleted), the UI SHALL end the sign-in and SHALL show the landing page. It SHALL NOT present the refusal as an empty inbox or as a user without archives.

#### Scenario: The signed-in user is deleted elsewhere
- **WHEN** Alice is signed in and her user is deleted through the API, and her workspace then loads its lists
- **THEN** the sign-in ends and the landing page is shown

### Requirement: Another site cannot sign a visitor in
In the web UI, a sign-in SHALL be started only from the app's own pages. Opening a sign-in address that the app did not issue for this visitor, from a link or a form elsewhere, SHALL NOT sign anyone in.

#### Scenario: A forged sign-in link
- **WHEN** a signed-out visitor opens a sign-in address with a made-up or already used value
- **THEN** nobody is signed in and the landing page is shown
