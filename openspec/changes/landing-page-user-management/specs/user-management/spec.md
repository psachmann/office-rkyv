# Spec Delta

## Purpose

Lets clients create and list users through the API, with the server as the single authority on input validity and on email uniqueness, so no client can put malformed or duplicate users into the system.

## ADDED Requirements

### Requirement: Users can be listed
The system SHALL return every user that has not been deleted, including each user's email as originally entered and their display name. When no such user exists, it SHALL return an empty list rather than an error.

#### Scenario: Listing when no users exist
- **WHEN** a client lists users and no user has ever been created
- **THEN** the system returns an empty list successfully

#### Scenario: Listing excludes deleted users
- **WHEN** a client lists users after one user has been created and another has been created and then deleted
- **THEN** only the non-deleted user is returned

### Requirement: Creating a user validates input on the server
The system SHALL reject a create-user request, without storing anything, when any of these rules is violated, and SHALL report every violated rule identified by the field it concerns:
- the email, after trimming surrounding whitespace, is empty
- the email is not plausibly well-formed: exactly one `@`, a non-empty part before it, a domain after it containing at least one `.` that is neither the domain's first nor last character, and no whitespace inside
- the email is longer than 254 characters
- the display name, after trimming surrounding whitespace, is empty
- the display name is longer than 100 characters

These rules SHALL hold regardless of any validation a client performs.

#### Scenario: Empty email and empty display name are both reported
- **WHEN** a client creates a user with an empty email and a whitespace-only display name
- **THEN** the request is rejected as invalid input, reporting a violation for the email field and a violation for the display-name field, and no user is created

#### Scenario: Malformed email is rejected
- **WHEN** a client creates a user with the email `alice.example.com`
- **THEN** the request is rejected as invalid input with a violation for the email field, and no user is created

#### Scenario: Valid input is accepted
- **WHEN** a client creates a user with the email `alice@example.com` and the display name `Alice`
- **THEN** the user is created and returned with that email and display name

### Requirement: Display names are stored trimmed
The system SHALL store a new user's display name with surrounding whitespace removed.

#### Scenario: Surrounding whitespace is removed from the display name
- **WHEN** a client creates a user with the display name `  Alice  `
- **THEN** the created user's display name is `Alice`

### Requirement: Email uniqueness ignores case and surrounding whitespace
The system SHALL treat two emails as the same when they are equal after trimming surrounding whitespace and converting to lowercase using culture-invariant rules. It SHALL refuse to create a user whose email is the same, in that sense, as a non-deleted existing user's, and SHALL report the refusal as a conflict with an existing user rather than as invalid input.

#### Scenario: Same email in different case is refused
- **WHEN** a user with the email `alice@example.com` exists and a client creates a user with the email `Alice@Example.COM`
- **THEN** the request is refused as already existing, and no second user is created

#### Scenario: Same email with surrounding whitespace is refused
- **WHEN** a user with the email `alice@example.com` exists and a client creates a user with the email `  alice@example.com `
- **THEN** the request is refused as already existing

#### Scenario: Different emails are both accepted
- **WHEN** a client creates users with the emails `alice@example.com` and `bob@example.com`
- **THEN** both users are created

### Requirement: The email is kept as entered for display
The system SHALL return a user's email as it was entered at creation, with only surrounding whitespace removed, and SHALL NOT return it lowercased.

#### Scenario: Mixed-case email is returned as entered
- **WHEN** a client creates a user with the email `Alice@Example.com`
- **THEN** the created user, and the same user in later list results, has the email `Alice@Example.com`

### Requirement: Finding a user by email ignores case and surrounding whitespace
The system SHALL find a non-deleted user by email using the same notion of sameness as uniqueness, so a lookup in any letter case or with surrounding whitespace finds that user.

#### Scenario: Lookup in different case finds the user
- **WHEN** a user with the email `Alice@Example.com` exists and a client looks up `alice@example.com`
- **THEN** that user is returned

#### Scenario: Lookup for an unknown email finds nothing
- **WHEN** a client looks up an email no non-deleted user has
- **THEN** the system reports that no user was found, without an error
