# api-error-mapping Specification

## Purpose

Defines how the gRPC API reports failures, so every client can tell invalid input, conflicts, concurrency clashes, and missing resources apart by status code alone, and internal details never reach a client.

## Requirements

### Requirement: Invalid input is reported as InvalidArgument with field details
When a request is rejected because its input violates validation rules, the API SHALL respond with the gRPC status `INVALID_ARGUMENT` and SHALL attach standard structured error details (`google.rpc.BadRequest`) listing one field violation per violated rule. Each field violation SHALL name the request field it concerns, using the field's name as it appears in the gRPC contract, and SHALL carry a stable, machine-readable code for the violated rule in its `reason`. The codes are:
- `required`: the field is empty after trimming surrounding whitespace, or, for file content, has no bytes
- `invalid_format`: the field's value does not have the required shape
- `too_long`: the field's value exceeds its maximum length
- `too_large`: the field's content exceeds its maximum size in bytes

A field violation's `description` SHALL be developer-facing text only; clients SHALL NOT depend on its content.

#### Scenario: Field violations travel with the status
- **WHEN** a client creates a user with an empty email and an empty display name
- **THEN** the response status is `INVALID_ARGUMENT` and its details contain a field violation for `email` with reason `required` and a field violation for `display_name` with reason `required`

#### Scenario: Every violated rule is reported with its own code
- **WHEN** a client creates a user with a 255-character email that contains no `@` and a 101-character display name
- **THEN** the details contain field violations for `email` with reasons `too_long` and `invalid_format`, and a field violation for `display_name` with reason `too_long`

#### Scenario: An empty field is reported only as required
- **WHEN** a client creates a user with an empty email
- **THEN** the only field violation for `email` has reason `required`

#### Scenario: An oversized file is reported as too large
- **WHEN** a client uploads a file larger than the size limit
- **THEN** the response status is `INVALID_ARGUMENT` and its details contain a field violation for `content` with reason `too_large`

### Requirement: Conflicts with existing data are reported as AlreadyExists
When a request is refused because it would claim a unique value already held by another resource, the API SHALL respond with the gRPC status `ALREADY_EXISTS`.

#### Scenario: Duplicate email
- **WHEN** a client creates a user whose email is already used by a non-deleted user
- **THEN** the response status is `ALREADY_EXISTS`

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

### Requirement: Stale-version writes are reported as Aborted
When a write is rejected because the version the client supplied no longer matches the resource's current version, the API SHALL respond with the gRPC status `ABORTED`, signalling that the client should reload and retry.

#### Scenario: Update with a stale version
- **WHEN** a client updates a user supplying a version older than the user's current version
- **THEN** the response status is `ABORTED`

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

### Requirement: Unexpected failures do not leak internals
Any failure not covered by another mapping SHALL be reported with the gRPC status `INTERNAL` and a generic message that contains no exception text, stack trace, or storage detail. The failure SHALL still be logged on the server with its full detail.

#### Scenario: Unhandled server error
- **WHEN** a request fails with an error the API does not specifically map
- **THEN** the response status is `INTERNAL`, its message contains none of the original error's text, and the original error is logged server-side

### Requirement: Mapping applies to every gRPC service
The failure mapping SHALL apply uniformly to all gRPC services the API hosts, without each service implementing it. It SHALL apply to calls that stream a request or a response in the same way as to calls that do not.

#### Scenario: Consistent mapping across services
- **WHEN** a failure of a mapped kind occurs in any hosted gRPC service
- **THEN** the response uses the same status code the mapping defines

#### Scenario: A refused streaming upload carries its details
- **WHEN** a client streams an upload that is refused as invalid input
- **THEN** the call ends with the status `INVALID_ARGUMENT` and the same structured field violations a non-streaming call would carry

#### Scenario: A refused streaming download carries its status
- **WHEN** a client requests the download of a document id that was never created
- **THEN** the call ends with the status `NOT_FOUND` and delivers no content

### Requirement: Error responses carry no end-user text
The API SHALL NOT rely on any status message or error-detail text to convey user-facing wording. Every refusal a client may need to explain to an end user SHALL be identifiable by gRPC status code plus, where defined, a stable reason code, so that clients produce all user-facing text from their own localized resources. Status messages SHALL be generic and SHALL NOT vary with the client's language.

#### Scenario: A client explains a refusal without server text
- **WHEN** a client receives an `INVALID_ARGUMENT` or `FAILED_PRECONDITION` response
- **THEN** the status code and the reason codes in its details identify every refused rule, without reading any status message or `description`

#### Scenario: Status message does not follow the request language
- **WHEN** the same invalid request is sent twice with different `accept-language` metadata
- **THEN** both responses carry identical status messages and identical reason codes

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
