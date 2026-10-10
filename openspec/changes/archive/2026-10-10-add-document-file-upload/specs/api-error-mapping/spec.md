# Spec Delta

## MODIFIED Requirements

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
