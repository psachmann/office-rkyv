# Spec Delta

## MODIFIED Requirements

### Requirement: Invalid input is reported as InvalidArgument with field details
When a request is rejected because its input violates validation rules, the API SHALL respond with the gRPC status `INVALID_ARGUMENT` and SHALL attach standard structured error details (`google.rpc.BadRequest`) listing one field violation per violated rule. Each field violation SHALL name the request field it concerns, using the field's name as it appears in the gRPC contract, and SHALL carry a stable, machine-readable code for the violated rule in its `reason`. The codes are:
- `required`: the field is empty after trimming surrounding whitespace
- `invalid_format`: the field's value does not have the required shape
- `too_long`: the field's value exceeds its maximum length

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

### Requirement: Business-rule violations are reported as FailedPrecondition
When a well-formed request is refused because the resource's current state does not allow the operation, the API SHALL respond with the gRPC status `FAILED_PRECONDITION` and SHALL attach a standard structured error detail (`google.rpc.ErrorInfo`) whose `reason` is a stable, machine-readable code naming the rule that refused the operation and whose `domain` identifies this application. The codes are:
- `user_deleted`: the operation is not allowed on a deleted user

#### Scenario: Renaming a deleted user
- **WHEN** a client updates the display name of a user that has been deleted, supplying that user's current version
- **THEN** the response status is `FAILED_PRECONDITION` and its details contain an `ErrorInfo` with reason `user_deleted`

## ADDED Requirements

### Requirement: Error responses carry no end-user text
The API SHALL NOT rely on any status message or error-detail text to convey user-facing wording. Every refusal a client may need to explain to an end user SHALL be identifiable by gRPC status code plus, where defined, a stable reason code, so that clients produce all user-facing text from their own localized resources. Status messages SHALL be generic and SHALL NOT vary with the client's language.

#### Scenario: A client explains a refusal without server text
- **WHEN** a client receives an `INVALID_ARGUMENT` or `FAILED_PRECONDITION` response
- **THEN** the status code and the reason codes in its details identify every refused rule, without reading any status message or `description`

#### Scenario: Status message does not follow the request language
- **WHEN** the same invalid request is sent twice with different `accept-language` metadata
- **THEN** both responses carry identical status messages and identical reason codes
