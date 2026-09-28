# Spec Delta

## Purpose

Defines how the gRPC API reports failures, so every client can tell invalid input, conflicts, concurrency clashes, and missing resources apart by status code alone, and internal details never reach a client.

## ADDED Requirements

### Requirement: Invalid input is reported as InvalidArgument with field details
When a request is rejected because its input violates validation rules, the API SHALL respond with the gRPC status `INVALID_ARGUMENT` and SHALL attach standard structured error details (`google.rpc.BadRequest`) listing one field violation per violated rule, each naming the request field it concerns.

#### Scenario: Field violations travel with the status
- **WHEN** a client creates a user with an empty email and an empty display name
- **THEN** the response status is `INVALID_ARGUMENT` and its details contain a field violation for `email` and a field violation for `display_name`

### Requirement: Conflicts with existing data are reported as AlreadyExists
When a request is refused because it would claim a unique value already held by another resource, the API SHALL respond with the gRPC status `ALREADY_EXISTS`.

#### Scenario: Duplicate email
- **WHEN** a client creates a user whose email is already used by a non-deleted user
- **THEN** the response status is `ALREADY_EXISTS`

### Requirement: Business-rule violations are reported as FailedPrecondition
When a well-formed request is refused because the resource's current state does not allow the operation, the API SHALL respond with the gRPC status `FAILED_PRECONDITION`.

#### Scenario: Renaming a deleted user
- **WHEN** a client updates the display name of a user that has been deleted, supplying that user's current version
- **THEN** the response status is `FAILED_PRECONDITION`

### Requirement: Stale-version writes are reported as Aborted
When a write is rejected because the version the client supplied no longer matches the resource's current version, the API SHALL respond with the gRPC status `ABORTED`, signalling that the client should reload and retry.

#### Scenario: Update with a stale version
- **WHEN** a client updates a user supplying a version older than the user's current version
- **THEN** the response status is `ABORTED`

### Requirement: Writes to a missing resource are reported as NotFound
When a write targets a resource that does not exist, the API SHALL respond with the gRPC status `NOT_FOUND`.

#### Scenario: Update of an unknown user
- **WHEN** a client updates a user id that was never created
- **THEN** the response status is `NOT_FOUND`

### Requirement: Unexpected failures do not leak internals
Any failure not covered by another mapping SHALL be reported with the gRPC status `INTERNAL` and a generic message that contains no exception text, stack trace, or storage detail. The failure SHALL still be logged on the server with its full detail.

#### Scenario: Unhandled server error
- **WHEN** a request fails with an error the API does not specifically map
- **THEN** the response status is `INTERNAL`, its message contains none of the original error's text, and the original error is logged server-side

### Requirement: Mapping applies to every gRPC service
The failure mapping SHALL apply uniformly to all gRPC services the API hosts, without each service implementing it.

#### Scenario: Consistent mapping across services
- **WHEN** a failure of a mapped kind occurs in any hosted gRPC service
- **THEN** the response uses the same status code the mapping defines
