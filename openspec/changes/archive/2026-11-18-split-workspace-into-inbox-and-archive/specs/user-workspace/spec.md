# Spec Delta

## REMOVED Requirements

### Requirement: Each user has a workspace page
**Reason**: The workspace page is split into the inbox page and the archive page, and the workspace address is gone.
**Migration**: See `inbox-page` (the inbox address and what it shows, and the former addresses) and `archive-page` (an archive's address and an address that does not resolve).

### Requirement: The workspace shows the inbox and the user's archives
**Reason**: The entries belong to the sidebar, which the two pages share, and the main area belongs to each page.
**Migration**: See `sidebar-navigation` for the entries, `inbox-page` and `archive-page` for the lists and their empty states.

### Requirement: Documents can be created from the workspace
**Reason**: Uploading is offered by the inbox page only, since a new document always goes to the inbox.
**Migration**: See `inbox-page`, "Documents can be created from the inbox page".

### Requirement: Documents can be filed from the workspace
**Reason**: Filing is offered on the inbox page and on archive pages alike, so it is specified once for both.
**Migration**: See `document-actions`, "Documents can be filed from a list".

### Requirement: Documents can be renamed and deleted from the workspace
**Reason**: Specified once for both pages.
**Migration**: See `document-actions`, "Documents can be renamed and deleted from a list".

### Requirement: Archives can be managed from the workspace
**Reason**: Creating an archive is a sidebar action; renaming and deleting belong to the archive's own page.
**Migration**: See `sidebar-navigation`, "Archives can be created from the sidebar", and `archive-page`, "An archive can be renamed and deleted from its page".

### Requirement: Workspace refusals are explained in the UI's language
**Reason**: Specified once for both pages.
**Migration**: See `document-actions`, "Refused actions are explained in the UI's language".

### Requirement: Documents can be downloaded from the workspace
**Reason**: Specified once for both pages.
**Migration**: See `document-actions`, "Documents can be downloaded from a list".
