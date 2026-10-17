# Spec Delta

## MODIFIED Requirements

### Requirement: Each user has a workspace page
The UI SHALL provide the signed-in user's workspace at `/workspace`, headed with that user's display name, and the same workspace with an archive selected at `/workspace/archives/{id}`. The workspace SHALL show only the signed-in user's inbox, archives and documents. A user's id SHALL NOT be part of a workspace address. A signed-out visitor SHALL be shown the landing page instead (see the `sign-in` capability). When the address names an archive that is not one of the signed-in user's live archives, the workspace SHALL show the inbox.

#### Scenario: Opening a user's workspace
- **WHEN** Alice is signed in and opens `/workspace`
- **THEN** the page shows Alice's display name, her inbox and her archives

#### Scenario: Unknown user
- **WHEN** a visitor opens the old address `/users/{id}` of any user
- **THEN** the not-found page is shown and no workspace

#### Scenario: An archive of another user in the address
- **WHEN** Bob is signed in and opens `/workspace/archives/{id}` with the id of Alice's archive
- **THEN** Bob's inbox is selected and none of Alice's documents are shown
