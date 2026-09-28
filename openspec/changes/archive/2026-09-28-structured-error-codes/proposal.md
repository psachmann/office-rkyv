# Proposal

## Why

Input validation is hand-written, rule by rule, in each handler's `Validate` method, and the API's error contract is only half machine-readable. Field violations smuggle their code through the human-readable `description` slot, and a business-rule refusal (e.g. renaming a deleted user) carries only English text, which the web UI can neither localize nor tell apart from other refusals. The web UI also duplicates the code strings the API emits, so the two can drift silently. Moving to declarative FluentValidation rules, Wolverine's gRPC error pipeline, and codes shared by both sides makes validation more structured and makes every user-facing refusal translatable in the frontend.

## What Changes

- Validation rules are declared as FluentValidation validators and run by Wolverine's FluentValidation middleware before the handler; the per-handler `Validate` methods and the app's own `ValidationException`/`FieldViolation` types are removed.
- Exception-to-status mapping moves from the hand-written `ExceptionMappingInterceptor` to Wolverine's gRPC integration (`WolverineFx.Grpc` rich error details), with an app-owned `IValidationFailureAdapter` producing the field violations.
- **BREAKING (wire contract)**: each `google.rpc.BadRequest` field violation carries its stable code in `reason` instead of `description`; `description` becomes a developer-facing English text that clients must not display.
- Business-rule refusals (`FAILED_PRECONDITION`) carry a stable code in a `google.rpc.ErrorInfo` detail instead of free English text; `InvariantViolationException` takes a code instead of a message.
- The server never sends end-user text; all user-facing wording stays in the web UI's localized resources, keyed by these codes.
- `OfficeRkyv.Contracts` becomes a class library that compiles the `.proto` files once and holds the shared code constants (`ViolationCodes`, `InvariantCodes`); the API and web UI reference it instead of each compiling the protos themselves.
- The web UI's gRPC clients are registered through Wolverine's typed client (`AddWolverineGrpcClient`), and its create-user error handling reads `reason` from the (now wrapped) `RpcException`.
- Housekeeping folded in: align all `WolverineFx.*` packages on one version, drop the unused `WolverineFx.FluentValidation.Grpc` package and `AbstractValidatorExtensions`, fix the uncommitted validator's class-name mismatch.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `api-error-mapping`: invalid-input field violations must carry a stable machine-readable reason code; business-rule refusals must carry a stable reason code in structured error details; neither may rely on server text for anything a client shows.

## Impact

- **API** (`src/OfficeRkyv.Api`): `Program.cs` bootstrapping; `Features/Users/Handlers/*` (validator, create/update handlers); `Shared/Exceptions/*` and `Shared/Interceptors/ExceptionMappingInterceptor.cs` removed or reshaped; new validation-failure adapter.
- **Contracts** (`src/OfficeRkyv.Contracts`): new `.csproj`, shared code constants; added to `OfficeRkyv.slnx`.
- **Web** (`src/OfficeRkyv.Web`): gRPC client registration, `CreateUserErrors`, csproj references. No new resource strings for invariant codes, since the web UI has no update-user screen yet.
- **Tests** (`tests/OfficeRkyv.UnitTests`): interceptor tests replaced; `UserPipelineTests`, `CreateUserErrorsTests`, `UsersFacadeTests` and event-sourcing fixtures that construct `InvariantViolationException` updated; the `web` extern alias on the Web reference may no longer be needed.
- **Dependencies**: add `WolverineFx.FluentValidation`, `WolverineFx.Grpc` (API and Web), `Google.Protobuf`, `Grpc.Core.Api`, `Grpc.Tools` (Contracts); remove `WolverineFx.FluentValidation.Grpc`; bump `WolverineFx.*` to one aligned version.
- **Clients**: only the in-repo web UI consumes the API, so the wire change ships in lockstep.
