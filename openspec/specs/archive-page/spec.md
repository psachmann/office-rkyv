# archive-page Specification

## Purpose
Gives each signed-in user a page for one of their archives: the documents filed in it, and the actions that rename or delete the archive. It replaces the second half of the former workspace page.

## Requirements

### Requirement: The archive page lists one archive's documents
The UI SHALL provide the archive page at `/archives/{id}`, headed with the archive's name, listing the documents filed in that archive by title. It SHALL show only the signed-in user's archives and documents. A signed-out visitor who opens an archive address SHALL be shown the landing page (see the `sign-in` capability).

#### Scenario: Opening an archive
- **WHEN** Alice is signed in and opens the address of her archive `Taxes 2026`
- **THEN** the page is headed `Taxes 2026` and lists the documents filed in it

### Requirement: The archive page says when it is empty or could not be loaded
When the archive has no documents, the page SHALL show an empty-state message. If the archive's documents cannot be loaded, the page SHALL say so and SHALL NOT show the empty-state message.

#### Scenario: Empty archive
- **WHEN** Alice opens the address of an archive that holds no documents
- **THEN** the page shows the empty-state message

#### Scenario: The archive's documents cannot be loaded
- **WHEN** Alice opens the address of her archive and its documents cannot be retrieved
- **THEN** the page shows the localized load-error message and not the empty-state message

### Requirement: The archive page offers no new-document action
The archive page SHALL NOT offer the new-document action. A new document always goes to the inbox, so the action belongs to the inbox page.

#### Scenario: No new-document action
- **WHEN** Alice opens the address of her archive `Taxes 2026`
- **THEN** the page offers no new-document action

### Requirement: An archive address that does not resolve shows not-found
When the address of an archive page names an archive that is not one of the signed-in user's live archives, the UI SHALL show the not-found page, and SHALL NOT show the inbox or any other user's documents. This holds for an unknown id, a deleted archive, and an archive of another user.

#### Scenario: An archive of another user in the address
- **WHEN** Bob is signed in and opens `/archives/{id}` with the id of Alice's archive
- **THEN** the not-found page is shown and none of Alice's documents

#### Scenario: An unknown id
- **WHEN** Alice is signed in and opens `/archives/{id}` with an id that belongs to no archive
- **THEN** the not-found page is shown

### Requirement: An archive can be renamed and deleted from its page
The archive page SHALL offer actions to rename and to delete the archive it shows. The inbox page SHALL NOT offer any. Rename SHALL ask for a name. Delete SHALL ask for confirmation and SHALL state that the archive's documents will return to the inbox.

After an archive is deleted, the inbox page SHALL be shown, and the returned documents SHALL appear in its list and count without a manual page reload once the deletion has been processed.

#### Scenario: Renaming the archive
- **WHEN** a visitor opens the archive `Taxes` and renames it to `Taxes 2026`
- **THEN** the page heading and the sidebar show `Taxes 2026`, and the archive page stays open

#### Scenario: No archive actions on the inbox page
- **WHEN** a visitor opens the inbox page
- **THEN** it offers no action to rename or delete an archive

#### Scenario: Deleting an archive with documents
- **WHEN** a visitor opens the archive `Household`, which contains two documents, deletes it and confirms
- **THEN** `Household` disappears from the sidebar, the inbox page is shown, and both documents appear in the inbox list

### Requirement: An archive that goes away while its page is open leads to the inbox
When the archive shown by an open archive page is deleted by another client, the UI SHALL show the inbox page once it learns of it, and SHALL NOT keep showing the archive's documents.

#### Scenario: Deleted elsewhere
- **WHEN** Alice has the archive `Household` open, and another client deletes it, and her archive list is then refreshed
- **THEN** the inbox page is shown and `Household` is gone from the sidebar
