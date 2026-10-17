# api-authorization Specification

## Purpose
Makes the API the authority on who is calling and what they may touch, so that no client can read or change another user's archives and documents by naming that user's ids.

## Requirements

### Requirement: Archive and document calls identify the caller
Every call to the archive and document services SHALL carry a caller token that names one user, is signed with the key the API trusts, is meant for this API, and has not expired. The API SHALL refuse a call without such a token, without doing anything, with the status `UNAUTHENTICATED`. It SHALL also refuse with `UNAUTHENTICATED` a token whose user does not exist or has been deleted.

#### Scenario: No token
- **WHEN** a client lists the inbox without a caller token
- **THEN** the response status is `UNAUTHENTICATED`

#### Scenario: A token signed with another key
- **WHEN** a client creates an archive with a caller token for Alice that was signed with a key the API does not trust
- **THEN** the response status is `UNAUTHENTICATED` and no archive is created

#### Scenario: An expired token
- **WHEN** a client lists archives with a caller token for Alice whose expiry has passed
- **THEN** the response status is `UNAUTHENTICATED`

#### Scenario: A token for a deleted user
- **WHEN** Alice has been deleted and a client uploads a file with a valid caller token for Alice
- **THEN** the response status is `UNAUTHENTICATED` and no document is created

#### Scenario: An upload without a token stores nothing
- **WHEN** a client uploads a file without a caller token
- **THEN** the response status is `UNAUTHENTICATED`, no document is created and no file content is stored

### Requirement: The caller owns what they create and list
The API SHALL create archives and documents for the caller, and SHALL list archives, the inbox and search results of the caller. A request SHALL NOT be able to name another owner.

#### Scenario: Creating for the caller
- **WHEN** Alice's caller creates an archive named `Taxes`
- **THEN** the archive is owned by Alice and appears in Alice's archive list, and not in anybody else's

#### Scenario: Listing is the caller's
- **WHEN** Alice and Bob each have an archive, and Bob's caller lists archives
- **THEN** only Bob's archive is listed

### Requirement: Another user's archives and documents are not found
When the caller acts on an archive or a document that belongs to another user, the API SHALL answer exactly as it answers for an id that does not exist, and SHALL change nothing. Renaming, deleting, filing, unfiling and downloading a document, renaming and deleting an archive, and filing into an archive SHALL be refused as `NOT_FOUND`. Listing the documents of another user's archive SHALL return an empty list.

#### Scenario: Renaming another user's document
- **WHEN** Bob's caller renames Alice's document, supplying its current version
- **THEN** the response status is `NOT_FOUND` and the document keeps its title

#### Scenario: Downloading another user's document
- **WHEN** Bob's caller downloads the file of Alice's document
- **THEN** the response status is `NOT_FOUND` and no content is sent

#### Scenario: Deleting another user's archive
- **WHEN** Bob's caller deletes Alice's archive, supplying its current version
- **THEN** the response status is `NOT_FOUND` and the archive still exists

#### Scenario: Filing into another user's archive
- **WHEN** Alice's caller files her own document into an archive that belongs to Bob
- **THEN** the response status is `NOT_FOUND` and the document stays where it was

#### Scenario: Listing another user's archive
- **WHEN** Bob's caller lists the documents of Alice's archive, which holds two documents
- **THEN** the list is empty and the call is not refused

### Requirement: Users can be listed, found and created without a caller
Listing users, finding a user by id or by email, and creating a user SHALL be answered without a caller token, so that a signed-out visitor can choose or create a user. Updating or deleting a user SHALL require a caller token as above, and SHALL be allowed only when the caller is that user. Otherwise it SHALL be refused, without changing anything, with the status `PERMISSION_DENIED`.

#### Scenario: Listing users while signed out
- **WHEN** a client without a caller token lists users
- **THEN** the users are returned

#### Scenario: Renaming yourself
- **WHEN** Alice's caller updates Alice's display name, supplying her current version
- **THEN** the display name is changed

#### Scenario: Renaming someone else
- **WHEN** Bob's caller updates Alice's display name
- **THEN** the response status is `PERMISSION_DENIED` and Alice's display name is unchanged

### Requirement: Caller tokens stay out of telemetry
A caller token SHALL NOT appear in any span, metric or log. Where telemetry identifies the caller at all, it SHALL do so by their user id only, never by email or display name.

#### Scenario: No span carries the token
- **WHEN** a client lists archives with a caller token
- **THEN** no exported span carries the token or any part of it
