# Spec Delta

## MODIFIED Requirements

### Requirement: The workspace shows the inbox and the user's archives
While a user's workspace is open, the app's navigation sidebar SHALL show a navigation list containing the inbox, with its number of documents, followed by each of the user's archives. The list SHALL sit with the sidebar's other entries, above the Settings entry. The workspace page SHALL NOT show a second navigation list of its own. On pages other than a workspace, and for an unknown or deleted user, the sidebar SHALL NOT show inbox or archive entries.

Exactly one entry SHALL be selected at a time and marked as the current page, and the inbox SHALL be selected when the page opens. The main area SHALL list the documents of the selected entry by title, except while a search is entered, when it SHALL show the search instead (see "Documents can be searched from the workspace"). When the selected entry has no documents, the main area SHALL show an empty-state message.

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

## ADDED Requirements

### Requirement: Documents can be searched from the workspace
The workspace SHALL offer a search field above the document list, whichever entry is selected. While the field holds a query of 3 or more characters, the main area SHALL list the user's documents that match it, from the inbox and from every archive, each showing where it is: the inbox or the archive's name. The results SHALL update as the visitor types, after a short pause, without a separate submit action. When no document matches, the main area SHALL show a no-results message. When more documents matched than are shown, the main area SHALL say so and suggest a more specific search.

While the field holds a query of 1 or 2 characters, the main area SHALL show a hint to type at least 3 characters, and no results. Clearing the field SHALL show the selected entry's documents again. Selecting an entry in the navigation list SHALL clear the search.

A listed result SHALL offer the same actions as any listed document, and the results SHALL update after such an action without a manual page reload. If the search cannot be run, the main area SHALL show a failure message and SHALL NOT present that as no results.

#### Scenario: Finding documents across the inbox and an archive
- **WHEN** Alice has `Invoice March` in her inbox and `Invoice April` in her archive `Taxes 2026`, and a visitor types `invoice` into the search field of her workspace
- **THEN** the main area lists both documents, `Invoice March` shown as in the inbox and `Invoice April` as in `Taxes 2026`

#### Scenario: Search while an archive is selected
- **WHEN** archive `Taxes 2026` is selected and a visitor searches for a word in the title of a document in the inbox
- **THEN** the main area lists that document

#### Scenario: No match
- **WHEN** a visitor searches for a word that is in none of Alice's documents
- **THEN** the main area shows the no-results message and not the empty-inbox message

#### Scenario: A query that is too short
- **WHEN** a visitor types `in` into the search field
- **THEN** the main area shows the hint to type at least 3 characters and lists no documents

#### Scenario: Clearing the search
- **WHEN** a visitor has searched for `invoice` while the inbox was selected, and clears the search field
- **THEN** the main area lists the inbox again

#### Scenario: Selecting an entry clears the search
- **WHEN** a visitor has searched for `invoice` and selects archive `Taxes 2026`
- **THEN** the search field is empty and the main area lists the documents in `Taxes 2026`

#### Scenario: Acting on a result
- **WHEN** a visitor has searched for `invoice` and renames the result `Invoice March` to `Electricity bill`
- **THEN** `Electricity bill` is no longer listed among the results, without a page reload

### Requirement: The search is kept in the page address
The workspace SHALL keep the search query in its page address while a search is entered, and SHALL open with that search when the address holds one. Typing SHALL NOT add a browser history entry per change, so that going back leaves the search and not one character of it.

#### Scenario: Reloading keeps the search
- **WHEN** a visitor has searched for `invoice` and reloads the page
- **THEN** the search field holds `invoice` and the main area lists the matching documents

#### Scenario: Opening an address with a search
- **WHEN** a visitor opens Alice's workspace with the search `invoice` in its address
- **THEN** the search field holds `invoice` and the main area lists the matching documents
