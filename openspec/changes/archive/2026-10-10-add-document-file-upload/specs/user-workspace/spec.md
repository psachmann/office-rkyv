# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Documents can be downloaded from the workspace
Each listed document SHALL offer a download action that hands the document's file to the visitor under its original file name, with the content exactly as it was uploaded. In the web UI the file SHALL arrive as a browser download.

#### Scenario: Downloading a document
- **WHEN** a visitor uploads `invoice.pdf` and then chooses the download action of that document
- **THEN** the browser receives a download named `invoice.pdf` whose content equals the uploaded file
