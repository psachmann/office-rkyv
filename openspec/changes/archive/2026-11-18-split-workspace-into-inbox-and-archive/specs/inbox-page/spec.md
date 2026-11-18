# Spec Delta

## Purpose

Gives each signed-in user a page for their inbox: the documents that are not filed in any archive, with the action that brings new documents in. It replaces the first half of the former workspace page.

## ADDED Requirements

### Requirement: The inbox page lists the signed-in user's unfiled documents
The UI SHALL provide the inbox page at `/inbox`, headed `Inbox`, listing the signed-in user's documents that are in no archive, by title. It SHALL show only the signed-in user's documents. A user's id SHALL NOT be part of the address. A signed-out visitor who opens `/inbox` SHALL be shown the landing page (see the `sign-in` capability).

#### Scenario: Opening the inbox
- **WHEN** Alice is signed in and opens `/inbox`
- **THEN** the page is headed `Inbox` and lists Alice's unfiled documents, and her display name is shown in the sidebar

#### Scenario: Another user's documents
- **WHEN** Alice and Bob each have an unfiled document and Bob is signed in and opens `/inbox`
- **THEN** the page lists Bob's document and not Alice's

### Requirement: The inbox page says when it is empty or could not be loaded
When the inbox has no documents, the page SHALL show an empty-state message. If the inbox cannot be loaded, the page SHALL say so and SHALL NOT show the empty-state message.

#### Scenario: Empty inbox
- **WHEN** Alice has no unfiled documents and opens `/inbox`
- **THEN** the page shows the empty-state message

#### Scenario: The inbox cannot be loaded
- **WHEN** Alice opens `/inbox` and her inbox cannot be retrieved
- **THEN** the page shows the localized load-error message and not the empty-state message

### Requirement: Documents can be created from the inbox page
The inbox page SHALL offer a new-document action that asks for one or more files and does not ask for a title. Each chosen file SHALL be uploaded as its own document, titled from its file name. The action SHALL show each file with its state: waiting, uploading with its progress, done, or refused with a localized reason. A file that is refused SHALL NOT prevent the other files from being uploaded.

#### Scenario: Uploading several files
- **WHEN** a visitor chooses the files `invoice.pdf` and `lease.pdf` in the new-document action
- **THEN** both are shown as done, and the inbox lists the documents `invoice` and `lease`

#### Scenario: A refused file does not stop the others
- **WHEN** a visitor chooses an empty file and the file `lease.pdf` in the new-document action
- **THEN** the empty file is shown as refused with the localized message for an empty file, and `lease` appears in the inbox

### Requirement: A created document appears in the inbox
A document created by the new-document action SHALL go to the inbox and SHALL appear in the page's list, and in the inbox count in the sidebar, without a manual page reload. The action SHALL flag a file larger than the size limit before sending it. The server's validation remains the authority.

#### Scenario: Creating a document
- **WHEN** a visitor chooses the file `Invoice March.pdf` in the new-document action
- **THEN** a document titled `Invoice March` appears in the inbox list and the inbox count increases by one, without a page reload

#### Scenario: A file over the size limit
- **WHEN** a visitor chooses a file larger than the size limit
- **THEN** the action flags the file before sending it and uploads nothing for it

### Requirement: The former workspace addresses are gone
The addresses `/workspace`, `/workspace/archives/{id}` and `/users/{id}` SHALL NOT lead to a page of documents, and SHALL NOT redirect. Each SHALL show the not-found page, for a signed-in and a signed-out visitor alike, and the sidebar SHALL show no inbox or archive entries on it.

#### Scenario: The old workspace address
- **WHEN** Alice is signed in and opens `/workspace`
- **THEN** the not-found page is shown, the address is unchanged, and the sidebar shows no inbox or archive entries

#### Scenario: The old archive address
- **WHEN** Alice is signed in and opens `/workspace/archives/{id}` with the id of one of her archives
- **THEN** the not-found page is shown

#### Scenario: The old user address
- **WHEN** a visitor opens `/users/{id}` with the id of any user
- **THEN** the not-found page is shown and no documents
