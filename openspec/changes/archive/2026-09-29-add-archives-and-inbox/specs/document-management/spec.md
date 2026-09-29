# Spec Delta

## Purpose

Lets a user keep documents, which for now are metadata only, and file each one into at most one of their archives. Documents that aren't filed make up the user's inbox, so every document always has exactly one place: the inbox or a single archive.

## ADDED Requirements

### Requirement: A user can create a document
The system SHALL create a document for an existing, non-deleted user, given a title. The new document SHALL belong to that user, SHALL NOT be in any archive, and SHALL be returned with its id, title, owner, archive (none) and version. The title SHALL be stored with surrounding whitespace removed.

#### Scenario: Creating a document puts it in the inbox
- **WHEN** a client creates a document titled `Invoice March` for user Alice
- **THEN** the document is returned with the title `Invoice March`, owned by Alice and in no archive, and it appears in Alice's inbox

#### Scenario: Title is stored trimmed
- **WHEN** a client creates a document with the title `  Lease contract  `
- **THEN** the created document's title is `Lease contract`

#### Scenario: Unknown owner
- **WHEN** a client creates a document for a user id that was never created
- **THEN** the request is refused as not found and no document is created

#### Scenario: Deleted owner
- **WHEN** a client creates a document for a user who has been deleted
- **THEN** the request is refused as a business-rule violation with reason `user_deleted` and no document is created

### Requirement: Document titles are validated on the server
The system SHALL reject creating or renaming a document, without storing anything, when the title is empty after trimming surrounding whitespace, or longer than 200 characters after trimming. It SHALL report each violation against the `title` field.

#### Scenario: Empty title
- **WHEN** a client creates a document with a whitespace-only title
- **THEN** the request is rejected as invalid input with a `required` violation for `title` and no document is created

#### Scenario: Title too long
- **WHEN** a client renames a document to a 201-character title
- **THEN** the request is rejected as invalid input with a `too_long` violation for `title` and the document keeps its previous title

### Requirement: A document can be renamed
The system SHALL change a non-deleted document's title when the client supplies the document's current version. It SHALL refuse the change as a stale write when the supplied version isn't current, and as a business-rule violation with reason `document_deleted` when the document has been deleted.

#### Scenario: Rename with the current version
- **WHEN** a client renames a document to `Invoice April`, supplying its current version
- **THEN** the document's title is `Invoice April` and its version has increased

#### Scenario: Rename with a stale version
- **WHEN** a client renames a document supplying a version older than its current one
- **THEN** the request is refused as a stale write and the title is unchanged

### Requirement: A document can be deleted
The system SHALL delete a document when the client supplies its current version. A deleted document SHALL no longer appear in the inbox, in any archive's contents, or in any list. Deleting an already-deleted document SHALL succeed without changing anything.

#### Scenario: Deleted document disappears from its archive
- **WHEN** a document filed in archive `Taxes 2026` is deleted
- **THEN** it no longer appears in the contents of `Taxes 2026`, nor in the inbox

### Requirement: A document is in at most one archive
The system SHALL record at most one archive per document. A document that is in no archive is in its owner's inbox. There SHALL be no state in which a document is in more than one archive, or in an archive and the inbox at the same time.

#### Scenario: Moving between archives leaves the first
- **WHEN** a document in archive `Household` is filed into archive `Taxes 2026`
- **THEN** it appears in the contents of `Taxes 2026` only, and not in `Household` or the inbox

### Requirement: A document can be filed into an archive
The system SHALL file a non-deleted document into an archive when the client supplies the document's current version and the archive exists, isn't deleted, and belongs to the document's owner. Filing SHALL work from the inbox and from another archive. Filing a document into the archive it's already in SHALL succeed without changing anything. Otherwise the system SHALL refuse it:
- the archive doesn't exist: not found
- the archive has been deleted: business-rule violation with reason `archive_deleted`
- the archive belongs to a different user: business-rule violation with reason `archive_owner_mismatch`
- the document has been deleted: business-rule violation with reason `document_deleted`
- the supplied document version isn't current: stale write

#### Scenario: Filing from the inbox
- **WHEN** a client files a document from Alice's inbox into Alice's archive `Taxes 2026`, supplying the document's current version
- **THEN** the document appears in the contents of `Taxes 2026` and no longer in Alice's inbox

#### Scenario: Filing into another user's archive
- **WHEN** a client files Alice's document into an archive that belongs to Bob
- **THEN** the request is refused with reason `archive_owner_mismatch` and the document stays where it was

#### Scenario: Filing into a deleted archive
- **WHEN** a client files a document into an archive that has been deleted
- **THEN** the request is refused with reason `archive_deleted` and the document stays where it was

### Requirement: A document can be returned to the inbox
The system SHALL remove a non-deleted document from its archive, returning it to its owner's inbox, when the client supplies the document's current version. Unfiling a document that's already in the inbox SHALL succeed without changing anything.

#### Scenario: Unfiling a filed document
- **WHEN** a client unfiles a document from archive `Household`, supplying its current version
- **THEN** the document appears in the inbox and no longer in `Household`

### Requirement: The inbox lists a user's unfiled documents
The system SHALL list, for a given user, every non-deleted document owned by that user that is in no archive, with each document's id, title and version. It SHALL NOT include other users' documents. When there are none, including for an unknown user id, it SHALL return an empty list rather than an error.

#### Scenario: Inbox holds only the user's own unfiled documents
- **WHEN** Alice has one unfiled document and one filed document, and Bob has one unfiled document
- **THEN** Alice's inbox contains exactly her unfiled document

#### Scenario: Empty inbox
- **WHEN** a user's documents are all filed or deleted
- **THEN** the user's inbox is an empty list

### Requirement: Deleting an archive returns its documents to the inbox
When an archive is deleted, every non-deleted document in it at that moment SHALL be returned to its owner's inbox. This MAY complete shortly after the deletion is acknowledged rather than as part of it. A document moved out of the archive, or deleted, before its return is processed SHALL keep that newer state and SHALL NOT be moved to the inbox.

#### Scenario: Documents come back to the inbox
- **WHEN** archive `Household` holding two documents is deleted
- **THEN** once the deletion has been processed, both documents are in the owner's inbox and in no archive

#### Scenario: A newer move wins over the return
- **WHEN** archive `Household` is deleted, and before its documents are returned one of them is filed into `Taxes 2026`
- **THEN** that document stays in `Taxes 2026`

### Requirement: Filing never leaves a document in a deleted archive
The system SHALL guarantee that no document ends up in a deleted archive, even when filing into an archive and deleting that same archive happen concurrently. Either the filing takes effect before the deletion, and the document is then returned to the inbox by the deletion, or the filing is refused.

#### Scenario: Filing concurrently with deletion
- **WHEN** one client files a document into archive `Household` while another client deletes `Household`
- **THEN** after both requests and the deletion have been processed, the document is either in the inbox or where it was before the filing, and never in `Household`
