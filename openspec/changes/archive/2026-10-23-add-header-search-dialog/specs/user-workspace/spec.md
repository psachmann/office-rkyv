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

## REMOVED Requirements

### Requirement: Documents can be searched from the workspace
**Reason**: Search is no longer a field of the workspace page. It is reached from the header on every page and answers in a dialog, so it no longer replaces the document list. The new behaviour is the `header-search` capability.
**Migration**: A user searches through the header's search control or its keyboard shortcut. A result takes the user to the inbox or archive that holds the document, where the row actions are.

### Requirement: The search is kept in the page address
**Reason**: The search is a transient dialog and no longer a state of the workspace page, so there is nothing for a reload, the back button or a pasted address to restore.
**Migration**: None. The workspace address no longer has a `q` part, and an address that still has one opens the workspace as if it had none.
