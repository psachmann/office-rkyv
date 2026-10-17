# user-landing-page Specification

## Purpose

Gives the web UI a landing page where anyone can see who the users are and add a new one, with the same page serving both the very first visit (no users yet) and everyday use.

## Requirements

### Requirement: The landing page lists users
The UI's root page (`/`) SHALL show every non-deleted user as a card displaying the user's display name and email, ordered by display name ignoring case. Each card SHALL be a single action, named by the user's display name, that signs in as that user (see the `sign-in` capability). No sign-in SHALL be required to view the page.

#### Scenario: Users are listed
- **WHEN** a signed-out visitor opens `/` and users exist
- **THEN** the page shows one card per non-deleted user with that user's display name and email

#### Scenario: Cards are ordered by display name
- **WHEN** the users `bob`, `Alice` and `Carol` exist and a signed-out visitor opens `/`
- **THEN** the cards appear in the order `Alice`, `bob`, `Carol`

#### Scenario: A listed user opens their workspace
- **WHEN** a signed-out visitor chooses Alice's card
- **THEN** the visitor is signed in as Alice and her workspace opens at `/workspace`

### Requirement: The landing page has an empty state
When no users exist, the landing page SHALL show an empty state that explains there are no users yet and offers an action to create the first one, instead of an empty list with no guidance.

#### Scenario: First visit with no users
- **WHEN** a visitor opens `/` and no users exist
- **THEN** the page shows the empty-state message and a create-user action

### Requirement: A user can be created from either state
The landing page SHALL offer a create-user action both when users exist and when none do. Both SHALL open the same create-user form, asking for an email and a display name. No sign-in SHALL be required to create a user. When the user is created, the form SHALL close, the visitor SHALL be signed in as the new user, and the new user's workspace SHALL open.

#### Scenario: Create while users already exist
- **WHEN** users exist and a visitor chooses the create-user action
- **THEN** the create-user form opens

#### Scenario: Successful creation signs in
- **WHEN** a visitor submits the form with a valid, unused email and the display name `Dana`
- **THEN** the form closes and the workspace opens at `/workspace`, headed `Dana`

#### Scenario: Successful creation updates the list
- **WHEN** a user `Dana` has been created and a signed-out visitor opens `/`
- **THEN** `Dana` is among the cards

### Requirement: The form gives client-side hints
The create-user form SHALL indicate a missing email, a missing display name, and an email that is obviously not an address before submitting, and SHALL NOT submit while such a hint is shown. These hints SHALL be advisory only; the server's decision is final.

#### Scenario: Missing fields are flagged before submit
- **WHEN** a visitor tries to submit the form with both fields empty
- **THEN** both fields are flagged and no request is sent

### Requirement: Server-side errors are shown in the user's language
When the server rejects a create request, the form SHALL stay open with the entered values preserved and SHALL show a message from the application's own localized resources. It SHALL NOT display the server's error text. In particular:
- invalid input SHALL be shown next to each field the server named, where that field is on the form
- an email already in use SHALL be shown next to the email field as an "already in use" message
- any other failure, including the server being unreachable, SHALL be shown as a general "could not create user" message

#### Scenario: Duplicate email
- **WHEN** a visitor submits an email that differs only in letter case from an existing user's email
- **THEN** the form stays open and the email field shows the localized "already in use" message

#### Scenario: Server-only validation failure
- **WHEN** the server rejects the email that the client-side hints accepted
- **THEN** the form stays open and the email field shows the localized invalid-email message

#### Scenario: Unexpected failure
- **WHEN** the create request fails for a reason other than invalid input or a duplicate email
- **THEN** the form stays open and shows the localized general failure message

### Requirement: Failure to load the list is visible
If the list of users cannot be loaded, the landing page SHALL show a localized error message instead of the list or the empty state, so a load failure is never mistaken for "no users".

#### Scenario: API unreachable on load
- **WHEN** a visitor opens `/` and the users cannot be retrieved
- **THEN** the page shows the localized load-error message and not the empty state
