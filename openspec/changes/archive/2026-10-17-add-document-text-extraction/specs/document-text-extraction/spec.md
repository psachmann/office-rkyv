# Spec Delta

## Purpose

Extracts the text of a document's file and keeps it, so that later features such as search can work on the words inside a file. It covers when extraction happens, which files and pages are read and how, what is recorded about the outcome, and what happens when text cannot be read.

## ADDED Requirements

### Requirement: Text is extracted after a document is created
The system SHALL extract the text of a document's file after the document has been created, without the client asking for it. The upload SHALL be answered without waiting for the extraction, which MAY complete shortly afterwards. Once it has completed, the system SHALL hold an extraction outcome for that file content, which is one of `extracted`, `unsupported` or `unreadable`.

#### Scenario: An upload is followed by an extraction
- **WHEN** a client uploads a PDF whose single page has a text layer reading `Invoice 4711 for March 2026`
- **THEN** the upload returns the created document
- **AND** once the extraction has been processed, the outcome for that content is `extracted` and the kept text contains `Invoice 4711 for March 2026`

### Requirement: A PDF page with a text layer uses it, and a page without one is read by OCR
For a PDF the system SHALL decide page by page. A page whose text layer holds at least 20 letters or digits SHALL contribute that text layer and SHALL NOT be read by OCR. Any other page SHALL be read by OCR, and the OCR result SHALL be that page's text. The outcome SHALL record the number of pages and the number of pages read by OCR.

#### Scenario: A born-digital PDF needs no OCR
- **WHEN** a three-page PDF in which every page has a text layer of more than 20 letters is extracted
- **THEN** the outcome is `extracted` with 3 pages, of which 0 were read by OCR, and OCR was not run

#### Scenario: A scanned PDF is read by OCR
- **WHEN** a two-page PDF in which no page has a text layer is extracted
- **THEN** the outcome is `extracted` with 2 pages, of which 2 were read by OCR, and the kept text is what OCR returned for those pages

#### Scenario: A mixed PDF is decided per page
- **WHEN** a PDF is extracted whose first page has a text layer of more than 20 letters and whose second page has none
- **THEN** the outcome is `extracted` with 2 pages, of which 1 was read by OCR
- **AND** the kept text has the text layer of the first page followed by the OCR result of the second

#### Scenario: A page with only a few characters is read by OCR
- **WHEN** a one-page PDF whose text layer is only `Page 1` is extracted
- **THEN** that page is read by OCR and its text is the OCR result

### Requirement: An image is read by OCR
The system SHALL read a PNG, JPEG or TIFF file by OCR and keep the result as the file's text. An image SHALL count as one page.

#### Scenario: A photographed receipt
- **WHEN** a PNG file is extracted
- **THEN** the outcome is `extracted` with 1 page, of which 1 was read by OCR, and the kept text is what OCR returned

### Requirement: The kind of file is decided from its content
The system SHALL decide whether a file is a PDF, a supported image or something else from the content itself and not from the content type recorded with the document. Extraction SHALL NOT change the recorded content type.

#### Scenario: A PDF uploaded without a content type
- **WHEN** a client uploads a PDF without declaring a content type, so that `application/octet-stream` is recorded
- **THEN** the outcome for that content is `extracted`, and the document's content type is still `application/octet-stream`

#### Scenario: A declared type that does not match the content
- **WHEN** a client uploads a ZIP archive declared as `application/pdf`
- **THEN** the outcome for that content is `unsupported`

### Requirement: Files that are not read are recorded and leave the document untouched
The system SHALL record the outcome `unsupported` for content that is neither a PDF nor a supported image, and `unreadable` for a PDF that cannot be opened, including one that is password protected. In both cases it SHALL keep no text. The document SHALL stay listed and its file downloadable.

#### Scenario: An unsupported file
- **WHEN** a client uploads a ZIP archive
- **THEN** the outcome for that content is `unsupported`, no text is kept, and the document's file can still be downloaded

#### Scenario: A damaged PDF
- **WHEN** a client uploads a file that starts like a PDF but is cut off in the middle
- **THEN** the outcome for that content is `unreadable`, no text is kept, and the document is still listed in the owner's inbox

### Requirement: A page that OCR cannot read is counted, not fatal
When OCR cannot read a page, because the OCR programs are not installed, because one of them fails, or because it takes longer than the time allowed for a page, the system SHALL continue with the remaining pages, SHALL keep the text of the pages it could read, and SHALL record the number of pages that were not read. The outcome SHALL still be `extracted`. The absence of the OCR programs SHALL NOT prevent the application from starting, or any upload or download from working.

#### Scenario: OCR is not installed
- **WHEN** OCR is unavailable and a PDF is extracted whose first page has a text layer of more than 20 letters and whose second page has none
- **THEN** the outcome is `extracted` with 2 pages, of which 1 was not read, and the kept text is the text layer of the first page

#### Scenario: OCR fails on one page
- **WHEN** a three-page scanned PDF is extracted and OCR fails on the second page only
- **THEN** the outcome is `extracted` with 3 pages, of which 2 were read by OCR and 1 was not read, and the kept text holds the first and the third page

#### Scenario: An image without OCR
- **WHEN** OCR is unavailable and a JPEG file is extracted
- **THEN** the outcome is `extracted` with 1 page, of which 1 was not read, and the kept text is empty

### Requirement: The text is kept page by page
The system SHALL keep the extracted text of a file so that it can be read back later, with the pages in their order in the file and distinguishable from each other. A page that was not read SHALL be present as an empty page, so that a page's position in the kept text is its position in the file.

#### Scenario: Reading the text back
- **WHEN** a three-page PDF has been extracted whose pages read `first`, nothing that could be read, and `third`
- **THEN** the kept text has three pages, of which the first contains `first`, the second is empty and the third contains `third`

### Requirement: The same content is extracted once
The system SHALL keep one extraction outcome and one text per distinct file content. When a file whose content already has an outcome is uploaded again, for the same or another user, the system SHALL NOT extract it again.

#### Scenario: The same file uploaded twice
- **WHEN** a client uploads the same PDF twice for Alice and both extractions have been processed
- **THEN** there is one outcome for that content, and the file was read once

### Requirement: Extraction does not change the document
Extraction SHALL NOT alter a document: its title, archive, file facts and version SHALL be the same after the extraction as before it.

#### Scenario: A rename right after the upload is not stale
- **WHEN** a client uploads a file, the extraction of that file completes, and the client then renames the document supplying the version the upload returned
- **THEN** the rename succeeds

### Requirement: Waiting extractions survive a restart
An extraction that has not completed when the application stops SHALL be carried out after the application starts again on the same data, without the file being uploaded again.

#### Scenario: Stopped before the extraction ran
- **WHEN** a document is created, the application stops before that file's extraction has completed, and the application is started again on the same data
- **THEN** once the extraction has been processed, the outcome for that content is present
