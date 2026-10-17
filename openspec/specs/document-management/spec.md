# document-management Specification

## Purpose

Lets a user keep documents, each one a single file that never changes together with its metadata, and file each one into at most one of their archives. Documents that aren't filed make up the user's inbox, so every document always has exactly one place: the inbox or a single archive.

## Requirements

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

### Requirement: Document titles are validated on the server
The system SHALL reject an upload that supplies a title, and a rename, without storing anything, when the title is empty after trimming surrounding whitespace, or longer than 200 characters after trimming. It SHALL report each violation against the `title` field. An upload that supplies no title is not subject to this rule; its title is derived from the file name.

#### Scenario: Empty title
- **WHEN** a client uploads a file with a whitespace-only title
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

### Requirement: A document can be returned to the inbox
The system SHALL remove a non-deleted document from its archive, returning it to its owner's inbox, when the client supplies the document's current version. Unfiling a document that's already in the inbox SHALL succeed without changing anything.

#### Scenario: Unfiling a filed document
- **WHEN** a client unfiles a document from archive `Household`, supplying its current version
- **THEN** the document appears in the inbox and no longer in `Household`

### Requirement: The inbox lists a user's unfiled documents
The system SHALL list, for a given user, every non-deleted document owned by that user that is in no archive, with each document's id, title and version, and its file's name, content type and size. It SHALL NOT include other users' documents. When there are none, including for an unknown user id, it SHALL return an empty list rather than an error.

#### Scenario: Inbox holds only the user's own unfiled documents
- **WHEN** Alice has one unfiled document and one filed document, and Bob has one unfiled document
- **THEN** Alice's inbox contains exactly her unfiled document

#### Scenario: Empty inbox
- **WHEN** a user's documents are all filed or deleted
- **THEN** the user's inbox is an empty list

#### Scenario: The list carries the file's name and size
- **WHEN** Alice uploads a 2,000-byte file named `scan.pdf`
- **THEN** her inbox lists that document with the file name `scan.pdf` and the size 2000

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

### Requirement: A document's title defaults to its file name
When an upload supplies no title, the system SHALL use the file's name without its last extension as the title, with surrounding whitespace removed and cut to 200 characters. When that leaves nothing, the system SHALL use the whole file name.

#### Scenario: Title from the file name
- **WHEN** a client uploads `Invoice March.pdf` without a title
- **THEN** the created document's title is `Invoice March`

#### Scenario: Only the last extension is dropped
- **WHEN** a client uploads `backup.2026.tar.gz` without a title
- **THEN** the created document's title is `backup.2026.tar`

#### Scenario: A long file name is cut
- **WHEN** a client uploads a file whose name without its extension has 230 characters, without a title
- **THEN** the created document's title is the first 200 of those characters

#### Scenario: A name that is only an extension
- **WHEN** a client uploads a file named `.pdf` without a title
- **THEN** the created document's title is `.pdf`

### Requirement: Uploaded files are limited in size and must not be empty
The system SHALL accept a file of any content type. It SHALL refuse a file larger than the size limit, which is 100 MB unless configured otherwise, as invalid input with a `too_large` violation for `content`. It SHALL refuse a file with no content as invalid input with a `required` violation for `content`. A refused upload SHALL create no document.

#### Scenario: A file at the limit is accepted
- **WHEN** a client uploads a file whose size equals the limit
- **THEN** the document is created

#### Scenario: A file over the limit is refused
- **WHEN** a client uploads a file one byte larger than the limit
- **THEN** the request is rejected as invalid input with a `too_large` violation for `content` and no document is created

#### Scenario: An empty file is refused
- **WHEN** a client uploads a file with no content
- **THEN** the request is rejected as invalid input with a `required` violation for `content` and no document is created

#### Scenario: Any content type is accepted
- **WHEN** a client uploads a file with the content type `image/jpeg`
- **THEN** the document is created with the content type `image/jpeg`

### Requirement: File names and content types are recorded as given, within limits
The system SHALL record the last path segment of the uploaded file's name as the document's file name. It SHALL reject an upload whose file name is empty after trimming, with a `required` violation for `file_name`, or longer than 255 characters, with a `too_long` violation for `file_name`. It SHALL record the content type the client declared without inspecting the content, SHALL record `application/octet-stream` when none was declared, and SHALL reject a content type longer than 255 characters with a `too_long` violation for `content_type`.

#### Scenario: A path is reduced to its file name
- **WHEN** a client uploads a file named `scans/2026/invoice.pdf`
- **THEN** the created document's file name is `invoice.pdf`

#### Scenario: Missing file name
- **WHEN** a client uploads a file with an empty file name
- **THEN** the request is rejected as invalid input with a `required` violation for `file_name` and no document is created

#### Scenario: Missing content type
- **WHEN** a client uploads a file without declaring a content type
- **THEN** the created document's content type is `application/octet-stream`

### Requirement: A document's file never changes
The system SHALL offer no operation that replaces or alters a document's file. Renaming, filing, unfiling and returning a document to the inbox SHALL leave its file name, content type, size and hash unchanged.

#### Scenario: Renaming keeps the file
- **WHEN** a client renames a document uploaded as `scan.pdf` to the title `Invoice April`
- **THEN** the document's title is `Invoice April` and its file name, content type, size and hash are the same as before

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

### Requirement: The same file may be uploaded more than once
The system SHALL accept an upload whose content equals that of an existing document, for the same or another user, and SHALL create a separate document for it. Deleting one of them SHALL NOT affect the others.

#### Scenario: Two documents from the same file
- **WHEN** a client uploads the same file twice for Alice
- **THEN** Alice's inbox lists two documents with different ids and the same hash

#### Scenario: Deleting one keeps the other downloadable
- **WHEN** a client uploads the same file twice for Alice and deletes the first document
- **THEN** the second document's file can still be downloaded with its original content
