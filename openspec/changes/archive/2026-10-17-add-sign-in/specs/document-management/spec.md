# Spec Delta

## MODIFIED Requirements

### Requirement: A user can create a document
The system SHALL create a document for the caller when the caller uploads a file (see the `api-authorization` capability for who the caller is and when a caller is refused). Every document SHALL have exactly one file. There SHALL be no way to create a document without a file. The new document SHALL belong to the caller, SHALL NOT be in any archive, and SHALL be returned with its id, title, owner, archive (none), version, and its file's name, content type, size in bytes and SHA-256 hash. A title supplied with the upload SHALL be stored with surrounding whitespace removed.

When the upload is refused for any reason, no document SHALL be created.

#### Scenario: Creating a document puts it in the inbox
- **WHEN** Alice's caller uploads the file `scan.pdf` with the title `Invoice March`
- **THEN** the document is returned with the title `Invoice March`, owned by Alice and in no archive, and it appears in Alice's inbox

#### Scenario: The file's facts are returned
- **WHEN** a client uploads a 2,000-byte file named `scan.pdf` with the content type `application/pdf`
- **THEN** the created document reports the file name `scan.pdf`, the content type `application/pdf`, the size 2000 and the SHA-256 hash of those bytes

#### Scenario: Title is stored trimmed
- **WHEN** a client uploads a file with the title `  Lease contract  `
- **THEN** the created document's title is `Lease contract`

#### Scenario: Unknown owner
- **WHEN** a client uploads a file with a caller token naming a user id that was never created
- **THEN** the request is refused as unauthenticated and no document is created

#### Scenario: Deleted owner
- **WHEN** a client uploads a file with a caller token naming a user who has been deleted
- **THEN** the request is refused as unauthenticated and no document is created

### Requirement: A document can be filed into an archive
The system SHALL file a non-deleted document of the caller into an archive when the client supplies the document's current version and the archive exists, isn't deleted, and belongs to the caller. Filing SHALL work from the inbox and from another archive. Filing a document into the archive it's already in SHALL succeed without changing anything. Otherwise the system SHALL refuse it:
- the document or the archive doesn't exist, or belongs to another user: not found
- the archive has been deleted: business-rule violation with reason `archive_deleted`
- the document has been deleted: business-rule violation with reason `document_deleted`
- the supplied document version isn't current: stale write

#### Scenario: Filing from the inbox
- **WHEN** Alice's caller files a document from Alice's inbox into Alice's archive `Taxes 2026`, supplying the document's current version
- **THEN** the document appears in the contents of `Taxes 2026` and no longer in Alice's inbox

#### Scenario: Filing into another user's archive
- **WHEN** Alice's caller files her document into an archive that belongs to Bob
- **THEN** the request is refused as not found and the document stays where it was

#### Scenario: Filing into a deleted archive
- **WHEN** a client files a document into an archive that has been deleted
- **THEN** the request is refused with reason `archive_deleted` and the document stays where it was

### Requirement: A document's file can be downloaded
The system SHALL return the content of a non-deleted document's file exactly as it was uploaded, when the document belongs to the caller. It SHALL refuse a download for an unknown document, or another user's document, as not found, and for a deleted document as a business-rule violation with reason `document_deleted`.

#### Scenario: The downloaded bytes equal the uploaded ones
- **WHEN** a client uploads a 300,000-byte file and then downloads that document's file
- **THEN** the downloaded content has 300,000 bytes and the same SHA-256 hash as the uploaded file

#### Scenario: Unknown document
- **WHEN** a client downloads the file of a document id that was never created
- **THEN** the request is refused as not found

#### Scenario: Another user's document
- **WHEN** Bob's caller downloads the file of Alice's document
- **THEN** the request is refused as not found

#### Scenario: Deleted document
- **WHEN** a client downloads the file of a document that has been deleted
- **THEN** the request is refused with reason `document_deleted`
