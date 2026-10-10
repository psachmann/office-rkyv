# Spec Delta

## MODIFIED Requirements

### Requirement: The desktop app offers the same workspace as the web app
The desktop app SHALL offer the same pages and features as the web UI: the user list and user creation, each user's workspace (inbox, archives, documents, uploading files, filing, renaming and deleting), and the settings page, with the same behaviour, validation and localized messages as specified for the web UI. Handing a downloaded file to the user is the one part that differs, and has its own requirement.

#### Scenario: Working with documents on the desktop
- **WHEN** a user creates an archive, uploads a file as a document on the desktop, files the document into the archive, and then deletes the archive
- **THEN** the document returns to the inbox, as it does in the web app

### Requirement: The desktop app keeps the user's data
The desktop app SHALL store its data, including the content of every uploaded file, in the user's data directory (`$XDG_DATA_HOME/office-rkyv`, defaulting to `~/.local/share/office-rkyv`), and SHALL keep it across restarts.

#### Scenario: Data survives a restart
- **WHEN** a user creates a user and uploads a file as a document, closes the app, and starts it again
- **THEN** the user and the document are still there, and the document's file can be saved with its original content

#### Scenario: File content is inside the data directory
- **WHEN** a user uploads a file as a document
- **THEN** the content is stored below the user's data directory and nowhere else

## ADDED Requirements

### Requirement: The desktop app saves a downloaded file to the user's filesystem
When a user chooses a document's download action, the desktop app SHALL write the document's file, with the content exactly as it was uploaded, to a location on the user's filesystem: either one the user chooses in a save dialog that proposes the original file name, or the user's Downloads directory under the original file name. When it saves into the Downloads directory, it SHALL tell the user where the file was saved, and SHALL choose a different name instead of overwriting an existing file. It SHALL NOT require a browser.

#### Scenario: Saving a document's file
- **WHEN** a user uploads `invoice.pdf` on the desktop, chooses the download action of that document and completes the save
- **THEN** a file with the same content as the uploaded one exists at the saved location

#### Scenario: An existing file is not overwritten without asking
- **WHEN** the desktop app saves into the Downloads directory and a file named `invoice.pdf` is already there
- **THEN** the download is saved under a different name and the existing file is unchanged

#### Scenario: Cancelling the save
- **WHEN** a user chooses the download action and cancels the save dialog
- **THEN** no file is written and no error is shown
