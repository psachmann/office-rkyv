# Spec Delta

## MODIFIED Requirements

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
