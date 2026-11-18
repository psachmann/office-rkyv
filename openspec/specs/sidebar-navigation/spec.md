# sidebar-navigation Specification

## Purpose
Defines the inbox and archive entries in the navigation sidebar: what they show, which one is current, where they appear, and the action that creates an archive.

## Requirements

### Requirement: The sidebar shows the inbox and the user's archives
While the inbox page or an archive page is open, the navigation sidebar SHALL show a navigation list containing the inbox, with its number of documents, followed by each of the signed-in user's archives. The list SHALL sit with the sidebar's other entries, above the Settings entry. Choosing an entry SHALL open the inbox page or that archive's page. The pages SHALL NOT show a second navigation list of their own.

#### Scenario: The entries are in the navigation sidebar
- **WHEN** a visitor opens the inbox page with the navigation sidebar expanded
- **THEN** the sidebar shows the inbox with its count and Alice's archives, above the Settings entry
- **AND** the page shows no other list of the inbox and archives

#### Scenario: Choosing an archive
- **WHEN** a visitor chooses the entry `Taxes 2026` in the sidebar
- **THEN** the archive page of `Taxes 2026` opens

#### Scenario: Moving between pages keeps the entries
- **WHEN** a visitor on the inbox page chooses an archive entry, and then the inbox entry
- **THEN** the sidebar's entries stay on the page and are not replaced by a loading state in between

### Requirement: The entries appear only on the inbox and archive pages
On every other page, including the not-found page, and while nobody is signed in or the signed-in user is unknown to the API, the sidebar SHALL NOT show inbox or archive entries.

#### Scenario: Leaving the inbox and archives
- **WHEN** a visitor goes from the inbox page to the settings page, or signs out
- **THEN** the sidebar no longer shows inbox or archive entries

### Requirement: The sidebar marks the current entry and says when entries cannot be loaded
Exactly one entry SHALL be marked as the current page: the inbox on the inbox page, the archive on its page. If the inbox or the archives cannot be loaded, the sidebar SHALL NOT present that as an empty inbox or as a user without archives.

#### Scenario: The current entry
- **WHEN** a visitor opens the page of the archive `Taxes 2026`
- **THEN** the `Taxes 2026` entry is marked as the current page and the inbox entry is not

#### Scenario: The entries cannot be loaded
- **WHEN** the archives cannot be retrieved
- **THEN** the sidebar shows a load-error message in place of the archive entries and not the no-archives message

### Requirement: Archives can be created from the sidebar
The sidebar SHALL offer an action to create an archive, placed with the archive entries. It SHALL ask for a name. A created archive SHALL appear in the navigation list without a manual page reload, and the current page SHALL stay open.

#### Scenario: Creating an archive
- **WHEN** a visitor creates an archive named `Household`
- **THEN** `Household` appears in the navigation list, and the page that was open is still open

#### Scenario: A name is required
- **WHEN** a visitor submits the create-archive form with no name
- **THEN** the name field is flagged and no archive is created
