# Spec Delta

## MODIFIED Requirements

### Requirement: The landing page lists users
The web UI's root page (`/`) SHALL show every non-deleted user, displaying each user's email and display name. Each listed user SHALL link to that user's workspace at `/users/{id}`. No sign-in SHALL be required to view it.

#### Scenario: Users are listed
- **WHEN** a visitor opens `/` and users exist
- **THEN** the page shows one entry per non-deleted user with that user's email and display name

#### Scenario: A listed user opens their workspace
- **WHEN** a visitor chooses a user in the list
- **THEN** the browser navigates to that user's workspace at `/users/{id}`
