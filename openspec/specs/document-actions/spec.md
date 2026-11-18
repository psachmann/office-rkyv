# document-actions Specification

## Purpose
Defines what a signed-in user can do with a document in the inbox or archive list (download, rename, file into an archive or back to the inbox, delete) and how a refused action is explained, the same on both pages.

## Requirements

### Requirement: Documents can be filed from a list
Each document listed on the inbox page or an archive page SHALL offer a "File into..." choice listing the user's archives other than the one it is in. When the document is in an archive, the choice SHALL also offer the inbox. Choosing an entry SHALL file or unfile the document, and the lists and the sidebar count SHALL update without a manual page reload.

#### Scenario: Filing a document from the inbox
- **WHEN** a visitor chooses `Taxes 2026` from the "File into..." choice of a document on the inbox page
- **THEN** the document leaves the inbox list, the inbox count decreases by one, and the document is listed on the page of `Taxes 2026`

#### Scenario: Returning a document to the inbox
- **WHEN** a visitor chooses the inbox from the "File into..." choice of a document on an archive page
- **THEN** the document leaves that archive's list and appears on the inbox page

### Requirement: Documents can be renamed and deleted from a list
Each document listed on the inbox page or an archive page SHALL offer rename and delete actions. Delete SHALL ask for confirmation before deleting. Both SHALL update the lists without a manual page reload.

#### Scenario: Renaming a document
- **WHEN** a visitor renames the document `invoice` to `Invoice March` on the inbox page
- **THEN** the inbox list shows `Invoice March` and no longer `invoice`

#### Scenario: Deleting a document
- **WHEN** a visitor deletes a document and confirms
- **THEN** the document is no longer listed on the inbox page or on any archive page

### Requirement: Documents can be downloaded from a list
Each document listed on the inbox page or an archive page SHALL offer a download action that hands the document's file to the visitor under its original file name, with the content exactly as it was uploaded. In the web UI the file SHALL arrive as a browser download.

#### Scenario: Downloading a document
- **WHEN** a visitor uploads `invoice.pdf` and then chooses the download action of that document
- **THEN** the browser receives a download named `invoice.pdf` whose content equals the uploaded file

### Requirement: Refused actions are explained in the UI's language
When the server refuses an action on a document or an archive, the page SHALL show a localized message chosen by the refusal's status code and reason code, never by server-supplied text. The message SHALL be shown in one place on the page, whichever action was refused. A stale-write refusal SHALL tell the user that the item changed elsewhere, and SHALL reload the affected lists.

#### Scenario: Stale write
- **WHEN** a visitor renames a document that another client changed in the meantime
- **THEN** the page shows the localized "changed elsewhere" message and shows the document's current state

#### Scenario: Filing into an archive deleted elsewhere
- **WHEN** a visitor files a document into an archive that another client deleted in the meantime
- **THEN** the page shows the localized message for a deleted archive and the archive disappears from the sidebar

#### Scenario: The same message on an archive page
- **WHEN** a visitor on an archive page renames a document that another client changed in the meantime
- **THEN** the page shows the localized "changed elsewhere" message
