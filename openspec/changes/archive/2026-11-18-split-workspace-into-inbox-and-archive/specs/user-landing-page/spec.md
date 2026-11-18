# Spec Delta

## ADDED Requirements

### Requirement: The landing page lists the users to sign in as
The UI's root page (`/`) SHALL show every non-deleted user as a card displaying the user's display name and email, ordered by display name ignoring case. Each card SHALL be a single action, named by the user's display name, that signs in as that user (see the `sign-in` capability). No sign-in SHALL be required to view the page.

#### Scenario: Users are listed
- **WHEN** a signed-out visitor opens `/` and users exist
- **THEN** the page shows one card per non-deleted user with that user's display name and email

#### Scenario: Cards are ordered by display name
- **WHEN** the users `bob`, `Alice` and `Carol` exist and a signed-out visitor opens `/`
- **THEN** the cards appear in the order `Alice`, `bob`, `Carol`

#### Scenario: A listed user opens their inbox
- **WHEN** a signed-out visitor chooses Alice's card
- **THEN** the visitor is signed in as Alice and her inbox page opens at `/inbox`

## MODIFIED Requirements

### Requirement: A user can be created from either state
The landing page SHALL offer a create-user action both when users exist and when none do. Both SHALL open the same create-user form, asking for an email and a display name. No sign-in SHALL be required to create a user. When the user is created, the form SHALL close, the visitor SHALL be signed in as the new user, and the new user's inbox page SHALL open.

#### Scenario: Create while users already exist
- **WHEN** users exist and a visitor chooses the create-user action
- **THEN** the create-user form opens

#### Scenario: Successful creation signs in
- **WHEN** a visitor submits the form with a valid, unused email and the display name `Dana`
- **THEN** the form closes and the inbox page opens at `/inbox`, with `Dana` shown in the sidebar

#### Scenario: Successful creation updates the list
- **WHEN** a user `Dana` has been created and a signed-out visitor opens `/`
- **THEN** `Dana` is among the cards

## REMOVED Requirements

### Requirement: The landing page lists users
**Reason**: Replaced by "The landing page lists the users to sign in as", whose scenario names the inbox page instead of the workspace.
**Migration**: See "The landing page lists the users to sign in as" in this capability.
