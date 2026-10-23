# user-workspace Specification

## Purpose

Gives each user a page in the web UI for working with their inbox, archives and documents: seeing what's unfiled, filing documents, and managing archives. Until authentication exists, the user in the page address stands in for the signed-in user.

## Requirements

### Requirement: Each user has a workspace page
The UI SHALL provide the signed-in user's workspace at `/workspace`, headed with that user's display name, and the same workspace with an archive selected at `/workspace/archives/{id}`. The workspace SHALL show only the signed-in user's inbox, archives and documents. A user's id SHALL NOT be part of a workspace address. A signed-out visitor SHALL be shown the landing page instead (see the `sign-in` capability). When the address names an archive that is not one of the signed-in user's live archives, the workspace SHALL show the inbox.

#### Scenario: Opening a user's workspace
- **WHEN** Alice is signed in and opens `/workspace`
- **THEN** the page shows Alice's display name, her inbox and her archives

#### Scenario: Unknown user
- **WHEN** a visitor opens the old address `/users/{id}` of any user
- **THEN** the not-found page is shown and no workspace

#### Scenario: An archive of another user in the address
- **WHEN** Bob is signed in and opens `/workspace/archives/{id}` with the id of Alice's archive
- **THEN** Bob's inbox is selected and none of Alice's documents are shown

### Requirement: The workspace shows the inbox and the user's archives
While a user's workspace is open, the app's navigation sidebar SHALL show a navigation list containing the inbox, with its number of documents, followed by each of the user's archives. The list SHALL sit with the sidebar's other entries, above the Settings entry. The workspace page SHALL NOT show a second navigation list of its own. On pages other than a workspace, and for an unknown or deleted user, the sidebar SHALL NOT show inbox or archive entries.

Exactly one entry SHALL be selected at a time and marked as the current page, and the inbox SHALL be selected when the page opens. The main area SHALL list the documents of the selected entry by title. When the selected entry has no documents, the main area SHALL show an empty-state message.

If the inbox or the archives cannot be loaded, the sidebar SHALL NOT present that as an empty inbox or as a user without archives.

#### Scenario: Inbox is selected by default
- **WHEN** a visitor opens Alice's workspace
- **THEN** the inbox is selected and the main area lists Alice's unfiled documents

#### Scenario: Selecting an archive
- **WHEN** a visitor selects archive `Taxes 2026`
- **THEN** the main area lists the documents in `Taxes 2026`

#### Scenario: Empty inbox
- **WHEN** Alice has no unfiled documents and the inbox is selected
- **THEN** the main area shows the empty-state message

#### Scenario: The entries are in the navigation sidebar
- **WHEN** a visitor opens Alice's workspace with the navigation sidebar expanded
- **THEN** the sidebar shows the inbox with its count and Alice's archives, above the Settings entry
- **AND** the page shows no other list of the inbox and archives

#### Scenario: Leaving the workspace
- **WHEN** a visitor goes from Alice's workspace to the landing page or the settings page
- **THEN** the sidebar no longer shows inbox or archive entries

#### Scenario: Unknown user
- **WHEN** a visitor opens `/users/{id}` with an id that belongs to no user
- **THEN** the sidebar shows no inbox or archive entries

### Requirement: Documents can be created from the workspace
The workspace SHALL offer a new-document action that asks for one or more files and does not ask for a title. Each chosen file SHALL be uploaded as its own document, titled from its file name. The action SHALL show each file with its state: waiting, uploading with its progress, done, or refused with a localized reason. A file that is refused SHALL NOT prevent the other files from being uploaded.

A created document SHALL go to the inbox, whichever entry is selected, and SHALL appear there without a manual page reload. The action SHALL flag a file larger than the size limit before sending it. The server's validation remains the authority.

#### Scenario: Creating a document
- **WHEN** a visitor chooses the file `Invoice March.pdf` in the new-document action
- **THEN** a document titled `Invoice March` appears in the inbox and the inbox count increases by one, without a page reload

#### Scenario: Uploading several files
- **WHEN** a visitor chooses the files `invoice.pdf` and `lease.pdf` in the new-document action
- **THEN** both are shown as done, and the inbox lists the documents `invoice` and `lease`

#### Scenario: A refused file does not stop the others
- **WHEN** a visitor chooses an empty file and the file `lease.pdf` in the new-document action
- **THEN** the empty file is shown as refused with the localized message for an empty file, and `lease` appears in the inbox

### Requirement: Documents can be filed from the workspace
Each listed document SHALL offer a "File into..." choice listing the user's archives other than the one it's in. When the document is in an archive, the choice SHALL also offer the inbox. Choosing an entry SHALL file or unfile the document, and the lists SHALL update without a manual page reload.

#### Scenario: Filing a document from the inbox
- **WHEN** a visitor chooses `Taxes 2026` from the "File into..." choice of a document in the inbox
- **THEN** the document leaves the inbox list, the inbox count decreases by one, and the document is listed when `Taxes 2026` is selected

#### Scenario: Returning a document to the inbox
- **WHEN** a visitor chooses the inbox from the "File into..." choice of a document in an archive
- **THEN** the document leaves that archive's list and appears in the inbox

### Requirement: Documents can be renamed and deleted from the workspace
Each listed document SHALL offer rename and delete actions. Delete SHALL ask for confirmation before deleting. Both SHALL update the lists without a manual page reload.

#### Scenario: Deleting a document
- **WHEN** a visitor deletes a document and confirms
- **THEN** the document is no longer listed anywhere in the workspace

### Requirement: Archives can be managed from the workspace
The workspace SHALL offer an action to create an archive, placed with the archive entries in the navigation sidebar. While an archive is selected, the main area SHALL offer actions to rename and to delete that archive. While the inbox is selected, no rename or delete action for an archive SHALL be offered.

Create and rename SHALL ask for a name. Delete SHALL ask for confirmation and SHALL state that the archive's documents will return to the inbox. After an archive is deleted, the inbox SHALL be selected, and the returned documents SHALL appear in it without a manual page reload once the deletion has been processed.

#### Scenario: Creating an archive
- **WHEN** a visitor creates an archive named `Household`
- **THEN** `Household` appears in the navigation list

#### Scenario: Renaming the selected archive
- **WHEN** a visitor selects archive `Taxes` and renames it to `Taxes 2026`
- **THEN** the navigation list and the main area show `Taxes 2026`, and the archive stays selected

#### Scenario: No archive actions for the inbox
- **WHEN** the inbox is selected
- **THEN** the workspace offers no action to rename or delete an archive

#### Scenario: Deleting an archive with documents
- **WHEN** a visitor selects archive `Household`, which contains two documents, deletes it and confirms
- **THEN** `Household` disappears from the navigation list, the inbox is selected, and both documents appear in the inbox

### Requirement: Workspace refusals are explained in the UI's language
When the server refuses a workspace action, the workspace SHALL show a localized message chosen by the refusal's status code and reason code, never by server-supplied text. A stale-write refusal SHALL tell the user that the item changed elsewhere, and SHALL reload the affected lists.

#### Scenario: Stale write
- **WHEN** a visitor renames a document that another client changed in the meantime
- **THEN** the workspace shows the localized "changed elsewhere" message and shows the document's current state

#### Scenario: Filing into an archive deleted elsewhere
- **WHEN** a visitor files a document into an archive that another client deleted in the meantime
- **THEN** the workspace shows the localized message for a deleted archive and the archive disappears from the navigation list

### Requirement: Documents can be downloaded from the workspace
Each listed document SHALL offer a download action that hands the document's file to the visitor under its original file name, with the content exactly as it was uploaded. In the web UI the file SHALL arrive as a browser download.

#### Scenario: Downloading a document
- **WHEN** a visitor uploads `invoice.pdf` and then chooses the download action of that document
- **THEN** the browser receives a download named `invoice.pdf` whose content equals the uploaded file

