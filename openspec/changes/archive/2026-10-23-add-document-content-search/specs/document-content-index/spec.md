# Spec Delta

## Purpose

Keeps the text extracted from files searchable. It covers when a file's text is added to the search, that it is added once for all documents that share the file, how texts extracted before the search existed are added, and that the text stays out of telemetry.

## ADDED Requirements

### Requirement: Extracted text is searchable once its extraction has completed
When the extraction of a file content has completed with the outcome `extracted`, the system SHALL make the extracted text searchable by every document that has that content, without a further request. The text SHALL be searchable as soon as the outcome is present, and an outcome of `unsupported` or `unreadable` SHALL make nothing searchable. A page that was not read SHALL contribute nothing. The words of a text SHALL be found in the way the words of a title are: anywhere inside a word, ignoring case.

#### Scenario: Searchable as soon as the outcome is present
- **WHEN** the extraction of Alice's uploaded PDF, whose text layer reads `Heizkostenabrechnung 2025`, has completed
- **THEN** a search for `heizkosten` contains that document, with no wait beyond the outcome being present

#### Scenario: A word inside a longer word of the text
- **WHEN** Alice has a document whose text reads `Stromrechnung für April` and she searches for `rechnung`
- **THEN** the result contains that document

#### Scenario: A page that was not read
- **WHEN** Alice has a three-page document of which OCR could read the first and the third page, and she searches for a word of the third page
- **THEN** the result contains that document

#### Scenario: Unsupported content makes nothing searchable
- **WHEN** Alice uploads a ZIP archive that contains the text `Kaufvertrag` in a stored entry, the extraction has been processed with the outcome `unsupported`, and she searches for `kaufvertrag`
- **THEN** the result is empty

### Requirement: A content is indexed once for all documents that share it
The system SHALL hold the searchable text of a file content once, whatever number of documents refer to the file. A document SHALL be found by the text of its file whoever owns the other documents with the same file. A search SHALL only ever return the documents of the caller.

#### Scenario: Two users upload the same file
- **WHEN** Alice and Bob each upload the same PDF, whose text reads `Mietvertrag Hauptstraße`, and the extraction has been processed
- **THEN** Alice's search for `mietvertrag` contains Alice's document and not Bob's, and Bob's search contains Bob's document and not Alice's

#### Scenario: A second upload of the same file
- **WHEN** Alice uploads a PDF, its extraction has been processed, and she uploads the same file again
- **THEN** the text is held once, and a search for a word in it contains both documents

### Requirement: What the document is does not change the text
A document SHALL be found by the text of its file whatever its title, archive and status at the time. Renaming a document, filing it in an archive or returning it to the inbox SHALL NOT remove it from the results for a word in its text. A deleted document SHALL NOT be returned. Indexing the text SHALL NOT alter any document: its title, archive, file facts and version SHALL be the same after the text became searchable as before.

#### Scenario: Renamed
- **WHEN** Alice has a document whose text reads `Kündigung` and renames it from `Scan` to `Letter`, and she searches for `kündigung`
- **THEN** the result contains that document, titled `Letter`

#### Scenario: Filed
- **WHEN** Alice files a document whose text reads `Kündigung` in her archive `Housing`, and she searches for `kündigung`
- **THEN** the result contains that document with the id of `Housing` as its archive

#### Scenario: Deleted
- **WHEN** Alice deletes a document whose text reads `Kündigung`, and she searches for `kündigung`
- **THEN** the result is empty

#### Scenario: A rename right after the text became searchable is not stale
- **WHEN** a client uploads a file, its text becomes searchable, and the client then renames the document supplying the version the upload returned
- **THEN** the rename succeeds

### Requirement: Texts extracted before the search existed are made searchable at start
When the application starts, the system SHALL make searchable the text of every file content that has an extraction outcome of `extracted` and a kept text but no searchable text, from the kept text and without reading the file again. Starting again with nothing left to do SHALL change nothing. The kept text SHALL remain the source: a searchable text that is lost or stale SHALL be restored by the next start.

#### Scenario: A database from before the search
- **WHEN** a store holds a document whose extraction outcome is `extracted` and whose text is kept, written by a version that did not make texts searchable, and the application is started on it
- **THEN** a search for a word in that text contains that document, and OCR has not run

#### Scenario: Starting again
- **WHEN** the application is started on the same data a second time
- **THEN** the same search gives the same result, and the document is listed once

#### Scenario: An extraction whose text is not there
- **WHEN** the application is started on data that holds an outcome `extracted` for a content whose kept text is missing
- **THEN** the application starts, and a search for the document's title finds it

### Requirement: The extracted text stays out of telemetry
The extracted text of a file SHALL NOT appear in any span or log that the API emits when the text is made searchable or when it is searched.

#### Scenario: No span carries the text
- **WHEN** a PDF whose text layer reads `Zahnarztrechnung` is uploaded, its extraction has been processed, and Alice searches for `Zahnarztrechnung`
- **THEN** no span emitted while handling the extraction or the search has an attribute or event containing `Zahnarztrechnung`
