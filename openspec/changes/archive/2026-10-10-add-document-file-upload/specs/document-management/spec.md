# Spec Delta

## MODIFIED Requirements

### Requirement: A user can create a document
The system SHALL create a document for an existing, non-deleted user when a client uploads a file for that user. Every document SHALL have exactly one file. There SHALL be no way to create a document without a file. The new document SHALL belong to that user, SHALL NOT be in any archive, and SHALL be returned with its id, title, owner, archive (none), version, and its file's name, content type, size in bytes and SHA-256 hash. A title supplied with the upload SHALL be stored with surrounding whitespace removed.

When the upload is refused for any reason, no document SHALL be created.

#### Scenario: Creating a document puts it in the inbox
- **WHEN** a client uploads the file `scan.pdf` with the title `Invoice March` for user Alice
- **THEN** the document is returned with the title `Invoice March`, owned by Alice and in no archive, and it appears in Alice's inbox

#### Scenario: The file's facts are returned
- **WHEN** a client uploads a 2,000-byte file named `scan.pdf` with the content type `application/pdf`
- **THEN** the created document reports the file name `scan.pdf`, the content type `application/pdf`, the size 2000 and the SHA-256 hash of those bytes

#### Scenario: Title is stored trimmed
- **WHEN** a client uploads a file with the title `  Lease contract  `
- **THEN** the created document's title is `Lease contract`

#### Scenario: Unknown owner
- **WHEN** a client uploads a file for a user id that was never created
- **THEN** the request is refused as not found and no document is created

#### Scenario: Deleted owner
- **WHEN** a client uploads a file for a user who has been deleted
- **THEN** the request is refused as a business-rule violation with reason `user_deleted` and no document is created

### Requirement: Document titles are validated on the server
The system SHALL reject an upload that supplies a title, and a rename, without storing anything, when the title is empty after trimming surrounding whitespace, or longer than 200 characters after trimming. It SHALL report each violation against the `title` field. An upload that supplies no title is not subject to this rule; its title is derived from the file name.

#### Scenario: Empty title
- **WHEN** a client uploads a file with a whitespace-only title
- **THEN** the request is rejected as invalid input with a `required` violation for `title` and no document is created

#### Scenario: Title too long
- **WHEN** a client renames a document to a 201-character title
- **THEN** the request is rejected as invalid input with a `too_long` violation for `title` and the document keeps its previous title

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

## ADDED Requirements

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
The system SHALL return the content of a non-deleted document's file exactly as it was uploaded. It SHALL refuse a download for an unknown document as not found, and for a deleted document as a business-rule violation with reason `document_deleted`.

#### Scenario: The downloaded bytes equal the uploaded ones
- **WHEN** a client uploads a 300,000-byte file and then downloads that document's file
- **THEN** the downloaded content has 300,000 bytes and the same SHA-256 hash as the uploaded file

#### Scenario: Unknown document
- **WHEN** a client downloads the file of a document id that was never created
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
