# Spec Delta

## MODIFIED Requirements

### Requirement: The desktop app offers the same workspace as the web app
The desktop app SHALL offer the same pages and features as the web UI: the landing page with its user cards and user creation, signing in and out, the signed-in user's workspace (inbox, archives, documents, uploading files, filing, renaming and deleting), and the settings page, with the same behaviour, validation and localized messages as specified for the web UI. Handing a downloaded file to the user, and how long a sign-in lasts (see the `sign-in` capability), are the parts that differ.

#### Scenario: Working with documents on the desktop
- **WHEN** a user signs in on the desktop, creates an archive, uploads a file as a document, files the document into the archive, and then deletes the archive
- **THEN** the document returns to the inbox, as it does in the web app

#### Scenario: Signing in and out on the desktop
- **WHEN** a user chooses Alice on the desktop's landing page, then signs out from the sidebar and chooses Bob
- **THEN** the workspace shows Alice's documents first, then the landing page, then Bob's documents and none of Alice's
