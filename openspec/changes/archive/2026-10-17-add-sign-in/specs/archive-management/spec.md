# Spec Delta

## MODIFIED Requirements

### Requirement: A user can create an archive
The system SHALL create an archive for the caller, given a name (see the `api-authorization` capability for who the caller is and when a caller is refused). It SHALL return the archive with its id, name, owner and version, and SHALL store the name with surrounding whitespace removed. A user SHALL be able to have any number of archives, including none. No archive SHALL be created automatically.

#### Scenario: Creating an archive
- **WHEN** Alice's caller creates an archive named `Taxes 2026`
- **THEN** the archive is returned with that name and owned by Alice, and it appears in Alice's archive list

#### Scenario: A new user has no archives
- **WHEN** a user has just been created
- **THEN** that user's archive list is empty

#### Scenario: Unknown owner
- **WHEN** a client creates an archive with a caller token naming a user id that was never created
- **THEN** the request is refused as unauthenticated and no archive is created

#### Scenario: Deleted owner
- **WHEN** a client creates an archive with a caller token naming a user who has been deleted
- **THEN** the request is refused as unauthenticated and no archive is created

### Requirement: A user's archives can be listed
The system SHALL list every non-deleted archive owned by the caller, with each archive's id, name and version. It SHALL NOT include other users' archives. When there are none, it SHALL return an empty list rather than an error.

#### Scenario: Only the user's own archives
- **WHEN** Alice has archives `Taxes` and `Household`, Bob has archive `Work`, and Alice's caller lists archives
- **THEN** the list contains exactly `Taxes` and `Household`

### Requirement: An archive's documents can be listed
The system SHALL list every non-deleted document currently in a given archive of the caller, with each document's id, title and version. When the archive is empty, deleted, unknown or another user's, it SHALL return an empty list rather than an error.

#### Scenario: Listing an archive's contents
- **WHEN** Alice's archive `Taxes` contains two documents and one other document was filed there and later deleted, and Alice's caller lists `Taxes`
- **THEN** the list contains exactly the two non-deleted documents

#### Scenario: Listing another user's archive
- **WHEN** Bob's caller lists Alice's archive `Taxes`
- **THEN** the list is empty
