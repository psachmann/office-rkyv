## ADDED Requirements

### Requirement: The header offers search to a signed-in user
While a user is signed in, the app's header SHALL show a search control in its centre, on every page. The control SHALL look like a search field, SHALL name the keyboard shortcut that opens the search, and SHALL open the search dialog when activated. While nobody is signed in, the header SHALL NOT show it.

#### Scenario: On the workspace
- **WHEN** Alice is signed in and opens her workspace
- **THEN** the header shows the search control

#### Scenario: On another page
- **WHEN** Alice is signed in and opens the settings page
- **THEN** the header shows the search control

#### Scenario: Signed out
- **WHEN** nobody is signed in and a visitor opens the landing page
- **THEN** the header shows no search control

#### Scenario: Opening the dialog
- **WHEN** Alice activates the search control
- **THEN** the search dialog opens and its search field has the focus

### Requirement: The search dialog finds documents as the user types
The search dialog SHALL hold a search field and, under it, the results. While the field holds a query with a word of 3 or more characters, the dialog SHALL list the signed-in user's documents that match it, from the inbox and from every archive, best match first. Each result SHALL show the document's title, its file name and where it is: the inbox or the archive's name. The results SHALL update as the user types, after a short pause, without a separate submit action.

When no document matches, the dialog SHALL say so. When more documents matched than are shown, the dialog SHALL say so and suggest a more specific search. While the field holds no word of 3 or more characters, including when it is empty, the dialog SHALL show a hint to type at least 3 characters and no results. If the search cannot be run, the dialog SHALL show a failure message and SHALL NOT present that as no results.

Pressing Escape SHALL close the dialog. A dialog that is opened again SHALL start with an empty field.

#### Scenario: Finding documents across the inbox and an archive
- **WHEN** Alice has `Invoice March` in her inbox and `Invoice April` in her archive `Taxes 2026`, and she types `invoice` into the dialog's search field
- **THEN** the dialog lists both documents, `Invoice March` shown as in the inbox and `Invoice April` as in `Taxes 2026`

#### Scenario: Searching from a page that is not the workspace
- **WHEN** Alice is on the settings page and searches for a word in the title of one of her documents
- **THEN** the dialog lists that document

#### Scenario: No match
- **WHEN** Alice searches for a word that is in none of her documents
- **THEN** the dialog shows the no-results message

#### Scenario: A query that is too short
- **WHEN** Alice types `in` into the dialog's search field
- **THEN** the dialog shows the hint to type at least 3 characters and lists no documents

#### Scenario: An empty field
- **WHEN** Alice opens the dialog
- **THEN** the dialog shows the hint to type at least 3 characters and lists no documents

#### Scenario: More matches than are shown
- **WHEN** more documents match Alice's query than the server returns
- **THEN** the dialog lists the returned documents and says that more matched

#### Scenario: A search that fails
- **WHEN** the search is refused or cannot be run
- **THEN** the dialog shows a failure message and not the no-results message

#### Scenario: Closing and opening again
- **WHEN** Alice searches for `invoice`, presses Escape, and opens the dialog again
- **THEN** the search field is empty and no results are listed

#### Scenario: The query is not kept in the address
- **WHEN** Alice searches for `invoice` in the dialog
- **THEN** the page address does not contain the query

### Requirement: Choosing a result goes to where the document is
Choosing a result SHALL close the dialog and open the inbox, for a document in the inbox, or the archive that holds the document, with that entry selected. This holds on any page. The dialog SHALL NOT offer renaming, filing or deleting a document.

The user SHALL be able to choose a result with the keyboard. Enter in the search field SHALL choose the first result. Down in the search field SHALL move the focus to the first result and Down on a result to the next one; Up on a result SHALL move the focus to the previous one, and Up on the first result back to the search field. Enter on a focused result SHALL choose it.

#### Scenario: Choosing a document in an archive
- **WHEN** Alice searches for `invoice` and chooses the result `Invoice April`, which is in `Taxes 2026`
- **THEN** the dialog closes and her workspace shows `Taxes 2026` selected

#### Scenario: Choosing a document in the inbox from another page
- **WHEN** Alice is on the settings page, searches for `invoice` and chooses the result `Invoice March`, which is in her inbox
- **THEN** the dialog closes and her workspace shows the inbox selected

#### Scenario: Choosing with the keyboard
- **WHEN** Alice searches for `invoice`, presses Down twice and then Enter
- **THEN** the second result is chosen, and the dialog closes on the entry that holds it

#### Scenario: Back to the field
- **WHEN** Alice searches for `invoice`, presses Down and then Up
- **THEN** the search field has the focus again

#### Scenario: Enter chooses the first result
- **WHEN** Alice searches for `invoice` and presses Enter without moving the mark
- **THEN** the first result is chosen

### Requirement: A result can be downloaded from the dialog
Each result SHALL offer a download action that hands the document's file to the user under its original file name, with the content exactly as it was uploaded, as the workspace's download does. Downloading SHALL leave the dialog open. A refused download SHALL be explained in the dialog in the UI's language, and SHALL NOT be presented as a search failure.

#### Scenario: Downloading from a result
- **WHEN** Alice has uploaded `invoice.pdf`, searches for `invoice` and chooses the download action of that result
- **THEN** the browser receives a download named `invoice.pdf` whose content equals the uploaded file, and the dialog is still open

#### Scenario: A download that is refused
- **WHEN** Alice chooses the download action of a result whose document another client has deleted in the meantime
- **THEN** the dialog shows the localized message for a deleted document

### Requirement: A keyboard shortcut opens the search dialog
While a user is signed in, pressing Ctrl+K, or Cmd+K on a Mac, SHALL open the search dialog from any page, in the web app and in the desktop app, whichever element has the focus. In a browser, the shortcut SHALL replace the browser's own action for that key. While the dialog is open, pressing it SHALL keep the search field focused and SHALL NOT open a second dialog. While another dialog is open, and while nobody is signed in, pressing it SHALL do nothing.

#### Scenario: Opening with the shortcut
- **WHEN** Alice is signed in and presses Ctrl+K on her workspace
- **THEN** the search dialog opens and its search field has the focus

#### Scenario: Opening from the settings page
- **WHEN** Alice is signed in and presses Ctrl+K on the settings page
- **THEN** the search dialog opens

#### Scenario: Pressing it again
- **WHEN** the search dialog is open with a query typed, and Alice presses Ctrl+K
- **THEN** one search dialog is still open, the query is still in the field, and the field has the focus

#### Scenario: While another dialog is open
- **WHEN** the upload dialog is open and Alice presses Ctrl+K
- **THEN** no search dialog opens

#### Scenario: Signed out
- **WHEN** nobody is signed in and a visitor presses Ctrl+K on the landing page
- **THEN** no search dialog opens

#### Scenario: On the desktop
- **WHEN** Alice is signed in in the desktop app and presses Ctrl+K
- **THEN** the search dialog opens and its search field has the focus

### Requirement: The search follows the UI's language
The texts of the search control and the dialog, including the name of the shortcut, SHALL come from the UI's resources and be shown in the UI's resolved language, English and German.

#### Scenario: German
- **WHEN** Alice's UI language is German
- **THEN** the header's search control and the dialog are named in German, and the shortcut is shown as `Strg+K`
