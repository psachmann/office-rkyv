# Spec Delta

## MODIFIED Requirements

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
