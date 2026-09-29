# Spec Delta

## ADDED Requirements

### Requirement: Finding a user by id
The system SHALL return a non-deleted user by id, with the same fields as the user list. When no user has that id, or that user has been deleted, it SHALL report that no user was found, without an error.

#### Scenario: Lookup by id finds the user
- **WHEN** a user Alice exists and a client looks up Alice's id
- **THEN** Alice is returned with her email and display name

#### Scenario: Lookup of a deleted user
- **WHEN** a client looks up the id of a user who has been deleted
- **THEN** the system reports that no user was found, without an error
