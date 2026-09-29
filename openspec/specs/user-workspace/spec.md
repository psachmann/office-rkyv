# user-workspace Specification

## Purpose

Gives each user a page in the web UI for working with their inbox, archives and documents: seeing what's unfiled, filing documents, and managing archives. Until authentication exists, the user in the page address stands in for the signed-in user.

## Requirements

### Requirement: Each user has a workspace page
The web UI SHALL provide a workspace page at `/users/{id}` for every non-deleted user, headed with that user's display name. No sign-in SHALL be required to view or use it. For an unknown or deleted user id, it SHALL show a not-found message instead of a workspace.

#### Scenario: Opening a user's workspace
- **WHEN** a visitor opens `/users/{id}` for Alice
- **THEN** the page shows Alice's display name, her inbox and her archives

#### Scenario: Unknown user
- **WHEN** a visitor opens `/users/{id}` with an id that belongs to no user
- **THEN** the page shows a not-found message and no workspace

### Requirement: The workspace shows the inbox and the user's archives
The workspace SHALL show a navigation list containing the inbox, with its number of documents, followed by each of the user's archives. Exactly one entry SHALL be selected at a time, and the inbox SHALL be selected when the page opens. The main area SHALL list the documents of the selected entry by title. When the selected entry has no documents, the main area SHALL show an empty-state message.

#### Scenario: Inbox is selected by default
- **WHEN** a visitor opens Alice's workspace
- **THEN** the inbox is selected and the main area lists Alice's unfiled documents

#### Scenario: Selecting an archive
- **WHEN** a visitor selects archive `Taxes 2026`
- **THEN** the main area lists the documents in `Taxes 2026`

#### Scenario: Empty inbox
- **WHEN** Alice has no unfiled documents and the inbox is selected
- **THEN** the main area shows the empty-state message

### Requirement: Documents can be created from the workspace
The workspace SHALL offer a new-document action that asks for a title. A created document SHALL go to the inbox, whichever entry is selected, and SHALL appear there without a manual page reload. The form SHALL flag a missing title before submitting. The server's validation remains the authority.

#### Scenario: Creating a document
- **WHEN** a visitor creates a document titled `Invoice March`
- **THEN** it appears in the inbox and the inbox count increases by one, without a page reload

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
The workspace SHALL offer actions to create an archive, and to rename and delete each archive. Create and rename SHALL ask for a name. Delete SHALL ask for confirmation and SHALL state that the archive's documents will return to the inbox. After an archive is deleted, the inbox SHALL be selected, and the returned documents SHALL appear in it without a manual page reload once the deletion has been processed.

#### Scenario: Creating an archive
- **WHEN** a visitor creates an archive named `Household`
- **THEN** `Household` appears in the navigation list

#### Scenario: Deleting an archive with documents
- **WHEN** a visitor deletes archive `Household`, which contains two documents, and confirms
- **THEN** `Household` disappears from the navigation list, the inbox is selected, and both documents appear in the inbox

### Requirement: Workspace refusals are explained in the UI's language
When the server refuses a workspace action, the workspace SHALL show a localized message chosen by the refusal's status code and reason code, never by server-supplied text. A stale-write refusal SHALL tell the user that the item changed elsewhere, and SHALL reload the affected lists.

#### Scenario: Stale write
- **WHEN** a visitor renames a document that another client changed in the meantime
- **THEN** the workspace shows the localized "changed elsewhere" message and shows the document's current state

#### Scenario: Filing into an archive deleted elsewhere
- **WHEN** a visitor files a document into an archive that another client deleted in the meantime
- **THEN** the workspace shows the localized message for a deleted archive and the archive disappears from the navigation list
