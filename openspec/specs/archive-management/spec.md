# archive-management Specification

## Purpose

Lets a user organize their documents into any number of named archives, and see which documents each archive contains. The user can create, rename and delete archives.

## Requirements

### Requirement: A user can create an archive
The system SHALL create an archive for an existing, non-deleted user, given a name. It SHALL return the archive with its id, name, owner and version, and SHALL store the name with surrounding whitespace removed. A user SHALL be able to have any number of archives, including none. No archive SHALL be created automatically.

#### Scenario: Creating an archive
- **WHEN** a client creates an archive named `Taxes 2026` for user Alice
- **THEN** the archive is returned with that name and owned by Alice, and it appears in Alice's archive list

#### Scenario: A new user has no archives
- **WHEN** a user has just been created
- **THEN** that user's archive list is empty

#### Scenario: Unknown owner
- **WHEN** a client creates an archive for a user id that was never created
- **THEN** the request is refused as not found and no archive is created

#### Scenario: Deleted owner
- **WHEN** a client creates an archive for a user who has been deleted
- **THEN** the request is refused as a business-rule violation with reason `user_deleted`

### Requirement: Archive names are free text
The system SHALL reject creating or renaming an archive, without storing anything, when the name is empty after trimming surrounding whitespace, or longer than 100 characters after trimming. It SHALL report each violation against the `name` field. It SHALL NOT require archive names to be unique. A user MAY have several archives with the same name.

#### Scenario: Empty name
- **WHEN** a client creates an archive with a whitespace-only name
- **THEN** the request is rejected as invalid input with a `required` violation for `name`

#### Scenario: Duplicate names are allowed
- **WHEN** Alice already has an archive named `Taxes` and a client creates another archive named `Taxes` for Alice
- **THEN** the second archive is created, and Alice's archive list contains both

### Requirement: An archive can be renamed
The system SHALL change a non-deleted archive's name when the client supplies the archive's current version. It SHALL refuse the change as a stale write when the supplied version isn't current, and as a business-rule violation with reason `archive_deleted` when the archive has been deleted.

#### Scenario: Rename with the current version
- **WHEN** a client renames archive `Taxes` to `Taxes 2026`, supplying its current version
- **THEN** the archive's name is `Taxes 2026`

#### Scenario: Rename a deleted archive
- **WHEN** a client renames an archive that has been deleted, supplying its current version
- **THEN** the request is refused with reason `archive_deleted`

### Requirement: An archive can be deleted
The system SHALL delete an archive when the client supplies its current version, whether or not it contains documents. A deleted archive SHALL no longer appear in its owner's archive list. Deleting an already-deleted archive SHALL succeed without changing anything. The documents it contained are returned to the inbox as the `document-management` capability specifies.

#### Scenario: Deleting a non-empty archive is allowed
- **WHEN** a client deletes an archive that contains documents, supplying its current version
- **THEN** the deletion succeeds and the archive no longer appears in the owner's archive list

#### Scenario: Delete with a stale version
- **WHEN** a client deletes an archive supplying a version older than its current one
- **THEN** the request is refused as a stale write and the archive still exists

### Requirement: A user's archives can be listed
The system SHALL list, for a given user, every non-deleted archive owned by that user, with each archive's id, name and version. It SHALL NOT include other users' archives. When there are none, including for an unknown user id, it SHALL return an empty list rather than an error.

#### Scenario: Only the user's own archives
- **WHEN** Alice has archives `Taxes` and `Household`, and Bob has archive `Work`
- **THEN** Alice's archive list contains exactly `Taxes` and `Household`

### Requirement: An archive's documents can be listed
The system SHALL list every non-deleted document currently in a given archive, with each document's id, title and version. When the archive is empty, deleted or unknown, it SHALL return an empty list rather than an error.

#### Scenario: Listing an archive's contents
- **WHEN** archive `Taxes` contains two documents and one other document was filed there and later deleted
- **THEN** listing `Taxes` returns exactly the two non-deleted documents
