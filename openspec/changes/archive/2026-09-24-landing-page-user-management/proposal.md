# Proposal

## Why

The API can already create and list users over gRPC, but nothing in the web UI uses it — `/` is still the "Hello, world!" scaffold, and there is no way to get a first user into the system without a gRPC client. At the same time the backend is not yet safe to put a form in front of: it accepts empty or malformed emails, treats `Alice@Example.com` and `alice@example.com` as two different users, and lets every failure (including a duplicate email) surface as an opaque `Unknown` gRPC error that a UI cannot act on.

## What Changes

- Replace the scaffold home page (`/`) with a user landing page: a list of existing users, an empty state when there are none, and a "create user" dialog reachable from both states. No authentication — anyone who can reach the page can list and create users.
- The web UI talks to the existing `UserService` gRPC client through DotNetQuery (a users-list query and a create-user mutation that refreshes the list), making this the first real DotNetQuery usage in the app.
- The form gives client-side hints (required fields, email shape) for fast feedback, but the backend is the authority: every rule the UI hints at is also enforced server-side.
- **Backend validation** of `CreateUser`: email and display name are required, and the email must be plausibly well-formed. Violations are reported per field.
- **Normalized email**: users gain a `NormalizedEmail` (trimmed, lowercased) recorded in the `UserCreated` event and used as the uniqueness key, while `Email` keeps the address as entered for display. Email lookups normalize their input. **BREAKING** (storage/events only): `UserCreated` gains a field and the natural key moves from `Email` to `NormalizedEmail`; existing dev data is not migrated (the dev SQLite database is recreated on every AppHost start).
- **gRPC error mapping**: a server interceptor translates domain and store exceptions into meaningful gRPC status codes (`InvalidArgument` with field details, `AlreadyExists`, `FailedPrecondition`, `Aborted`, `NotFound`), and anything unexpected into `Internal` without leaking internal messages.
- The UI maps those status codes to its own localized messages rather than displaying server text.

## Capabilities

### New Capabilities

- `user-management`: creating and listing users through the API — input validation, email normalization, and email uniqueness (case- and whitespace-insensitive).
- `api-error-mapping`: how the gRPC API reports failures — the mapping from failure kinds to gRPC status codes, structured field-level validation details, and never leaking internal error text.
- `user-landing-page`: the web UI landing page — listing users, the empty state, the create-user dialog with client-side hints, and localized presentation of server-side errors.

### Modified Capabilities

_None._ `ui-localization` already requires home-page text to come from resource files; this change follows that requirement without changing it.

## Impact

- `src/OfficeRkyv.Api/Features/Users/*`: `UserCreated`, `User` (new `NormalizedEmail`, natural key moves), `CreateUserHandler` (validation + normalization), `FindUserByEmailHandler` (normalized lookup).
- `src/OfficeRkyv.Api/Shared/*` and `Program.cs`: new validation exception type and a gRPC error-mapping interceptor registered on `AddGrpc`.
- `src/OfficeRkyv.Web/Components/Pages/Home.razor` + `Resources/Components/Pages/Home.resx`: landing page replaces the scaffold; new create-user dialog component with its own `.resx`.
- `src/OfficeRkyv.Contracts/users.proto`: unchanged — `NormalizedEmail` stays internal; error details travel in standard gRPC rich status.
- Dependencies: `Grpc.StatusProto` (rich status / `google.rpc.BadRequest`) added to Api and Web.
- Tests: user pipeline unit tests updated for normalization/validation; new interceptor mapping tests; the functional test asserting the "Hello, world!" heading is replaced by landing-page tests.
- Out of scope: authentication/authorization, editing or deleting users from the UI, migrating/upcasting existing `UserCreated` events, releasing a deleted user's email for reuse.
