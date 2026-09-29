# Spec Delta

## MODIFIED Requirements

### Requirement: Business-rule violations are reported as FailedPrecondition
When a well-formed request is refused because the resource's current state does not allow the operation, the API SHALL respond with the gRPC status `FAILED_PRECONDITION` and SHALL attach a standard structured error detail (`google.rpc.ErrorInfo`) whose `reason` is a stable, machine-readable code naming the rule that refused the operation and whose `domain` identifies this application. The codes are:
- `user_deleted`: the operation is not allowed on a deleted user, or on behalf of a deleted user
- `archive_deleted`: the operation is not allowed on a deleted archive, including filing a document into it
- `document_deleted`: the operation is not allowed on a deleted document
- `archive_owner_mismatch`: a document cannot be filed into an archive that belongs to a different user

#### Scenario: Renaming a deleted user
- **WHEN** a client updates the display name of a user that has been deleted, supplying that user's current version
- **THEN** the response status is `FAILED_PRECONDITION` and its details contain an `ErrorInfo` with reason `user_deleted`

#### Scenario: Filing into a deleted archive
- **WHEN** a client files a document into an archive that has been deleted
- **THEN** the response status is `FAILED_PRECONDITION` and its details contain an `ErrorInfo` with reason `archive_deleted`

#### Scenario: Filing into another user's archive
- **WHEN** a client files a document into an archive owned by a different user
- **THEN** the response status is `FAILED_PRECONDITION` and its details contain an `ErrorInfo` with reason `archive_owner_mismatch`
