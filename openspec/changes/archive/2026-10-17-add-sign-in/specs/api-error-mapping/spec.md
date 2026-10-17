# Spec Delta

## ADDED Requirements

### Requirement: Calls without an accepted caller are reported as Unauthenticated
When a call needs a caller and has none the API accepts, the API SHALL respond with the gRPC status `UNAUTHENTICATED`, with no details and with the API's own fixed message. This SHALL hold for unary and streaming calls of every service.

#### Scenario: A call without a token
- **WHEN** a client renames a document without a caller token
- **THEN** the response status is `UNAUTHENTICATED` and the status carries no details

#### Scenario: A streaming call without a token
- **WHEN** a client downloads a document's file without a caller token
- **THEN** the call ends with the status `UNAUTHENTICATED` and no content

### Requirement: Actions on another user are reported as PermissionDenied
When an accepted caller asks to change a user other than themselves, the API SHALL respond with the gRPC status `PERMISSION_DENIED`, with no details and with the API's own fixed message.

#### Scenario: Deleting another user
- **WHEN** Bob's caller deletes Alice's user, supplying her current version
- **THEN** the response status is `PERMISSION_DENIED` and Alice still exists

## MODIFIED Requirements

### Requirement: Business-rule violations are reported as FailedPrecondition
When a well-formed request is refused because the resource's current state does not allow the operation, the API SHALL respond with the gRPC status `FAILED_PRECONDITION` and SHALL attach a standard structured error detail (`google.rpc.ErrorInfo`) whose `reason` is a stable, machine-readable code naming the rule that refused the operation and whose `domain` identifies this application. The codes are:
- `user_deleted`: the operation is not allowed on a deleted user
- `archive_deleted`: the operation is not allowed on a deleted archive, including filing a document into it
- `document_deleted`: the operation is not allowed on a deleted document
- `archive_owner_mismatch`: a document cannot be filed into an archive that belongs to a different user

A code SHALL keep its meaning and SHALL NOT be reused for another rule, also when the API no longer produces it. Through the API, a deleted caller is refused as unauthenticated rather than with `user_deleted`, and an archive of another user is not found rather than refused with `archive_owner_mismatch`. Clients SHALL still be able to explain both codes.

#### Scenario: Filing into a deleted archive
- **WHEN** a client files a document into an archive that has been deleted
- **THEN** the response status is `FAILED_PRECONDITION` and its details contain an `ErrorInfo` with reason `archive_deleted`

#### Scenario: Renaming a deleted user
- **WHEN** a client updates the display name of a user that has been deleted, with a caller token naming that user
- **THEN** the response status is `UNAUTHENTICATED`, not `FAILED_PRECONDITION` with reason `user_deleted`

#### Scenario: Filing into another user's archive
- **WHEN** a client files the caller's document into an archive owned by a different user
- **THEN** the response status is `NOT_FOUND`, not `FAILED_PRECONDITION` with reason `archive_owner_mismatch`

#### Scenario: Renaming a deleted document
- **WHEN** a client renames a document of the caller that has been deleted, supplying its current version
- **THEN** the response status is `FAILED_PRECONDITION` and its details contain an `ErrorInfo` with reason `document_deleted`

### Requirement: Writes to a missing resource are reported as NotFound
When a write targets an archive or a document that does not exist, or one that belongs to another user, the API SHALL respond with the gRPC status `NOT_FOUND`. A write to a user other than the caller is refused as `PERMISSION_DENIED` whether or not that user exists, because a caller may change only themselves.

#### Scenario: Update of an unknown user
- **WHEN** a client updates a user id that was never created, with a valid caller token for another user
- **THEN** the response status is `PERMISSION_DENIED`

#### Scenario: Rename of an unknown document
- **WHEN** a client renames a document id that was never created
- **THEN** the response status is `NOT_FOUND`

#### Scenario: Rename of another user's archive
- **WHEN** Bob's caller renames Alice's archive, supplying its current version
- **THEN** the response status is `NOT_FOUND`
