# Design

## Context

See proposal.md (Why) for motivation and `specs/api-error-mapping/spec.md` for the wire contract.

Current state that shapes the approach:

- The API hosts proto-first gRPC services on plain `Grpc.AspNetCore` (`MapGrpcService<UsersServiceImpl>`). Each service method maps the proto request to a domain command (Mapperly) and calls `IMessageBus.InvokeAsync`, so handlers never see proto types.
- `ExceptionMappingInterceptor` is the one place exceptions become statuses today. It maps the app's own `ValidationException`, `DuplicateNaturalKeyException`, `InvariantViolationException`, `EventStreamUnexpectedMaxEventIdException` and `NotFoundException`, logs unmapped failures, and never forwards exception text.
- Validation lives in handler `Validate` methods that Wolverine weaves in by naming convention. The uncommitted WIP already contains a FluentValidation `CreateUserValidator` that emits `ViolationCodes` as error codes.
- `src/OfficeRkyv.Contracts` is a plain folder of `.proto` files. The API compiles it with `GrpcServices="Server"` and the web UI with `GrpcServices="Client"`, so each has its own copy of every message type. The unit-test project references both and needs the `web` extern alias as a result.
- The web UI's `CreateUserErrors` already maps codes to its own `.resx` keys and never shows server text. It currently reads the code from `FieldViolation.Description`.
- Only the in-repo web UI calls the API, so the wire change can ship in one commit.

## Goals / Non-Goals

**Goals:**
- Validation rules are declared once per command as FluentValidation validators and executed by middleware, not called by hand.
- Status mapping is configured through Wolverine's gRPC error pipeline instead of an app-maintained interceptor, while keeping every guarantee of the current `api-error-mapping` spec (no leaked internals, logging, uniform across services).
- Codes are defined once, in a library both processes compile against.

**Non-Goals:**
- Server-side localization or propagating the UI culture to the API: the server sends codes only.
- Letting Wolverine generate the gRPC service classes (`MapWolverineGrpcServices` proto-first stubs). `UsersServiceImpl` stays hand-written so handlers keep taking domain commands, not proto messages.
- An update-user screen in the web UI, or resource strings for invariant codes. The code travels on the wire; the UI gains text for it when a screen needs it.
- Codes as proto enums. `reason` fields are strings by definition; C# constants are enough for the one in-repo client.

## Decisions

### D1. Wolverine's gRPC rich error details replace `ExceptionMappingInterceptor`

`services.AddWolverineGrpc()` installs `WolverineGrpcExceptionInterceptor`; `opts.UseGrpcRichErrorDetails(cfg => ...)` registers the mappings:

| Exception | Status | Details |
|---|---|---|
| `FluentValidation.ValidationException` | `INVALID_ARGUMENT` | `BadRequest`, via our adapter (D2) |
| `InvariantViolationException` | `FAILED_PRECONDITION` | `ErrorInfo { reason = code, domain = ErrorDomain }` |
| `DuplicateNaturalKeyException` | `ALREADY_EXISTS` | none |
| `EventStreamUnexpectedMaxEventIdException` | `ABORTED` | none |
| `NotFoundException` | `NOT_FOUND` | none |
| anything else | `INTERNAL` | `EnableDefaultErrorInfo()`: an opaque `ErrorInfo` only |

Every status message is a fixed generic English sentence, never exception text.

*Alternatives:* keep our interceptor and only add a `FluentValidation.ValidationException` arm. That is smaller, but the user chose to adopt Wolverine's gRPC integration (typed client exceptions, header propagation) for its own sake. Running both interceptors was rejected: ours catches every `Exception`, so which one runs first would decide whether validation failures become `INTERNAL`.

*As built:* the non-validation rows are one app-owned `IGrpcStatusDetailsProvider` (`ErrorStatusProvider`), a single switch registered after Wolverine's validation provider, instead of several `MapException<T>` entries. Its message text is ours by construction, whereas what `MapException` puts in the status message could not be confirmed from the docs and might be `exception.Message`. All of it is wired in one `UseApiErrorMapping()` extension that both `Program.cs` and the tests call. Over-the-wire tests confirm that the validation provider's status message is generic.

*Guardrail outcome (task 1.2, verified against a Kestrel host):* (a) Wolverine's interceptor does run for `MapGrpcService<T>` services. (b) With `EnableDefaultErrorInfo()` the status message is the generic "Internal server error", but the attached `ErrorInfo` carries the exception's **type name** as `reason` (domain `wolverine.grpc`), which exposes an internal detail. (c) Every mapped exception is logged by `WolverineGrpcExceptionInterceptor` at **Warning**, not Error. (d) A client cancellation surfaces server-side as `TaskCanceledException`, is mapped to `INTERNAL` and logged as a warning. So `EnableDefaultErrorInfo()` is **not** used. Instead, an app-owned `IGrpcStatusDetailsProvider` is registered last. It maps an `OperationCanceledException` whose call token is cancelled to `CANCELLED` without logging. It maps anything else to `INTERNAL` with a generic message and no details, and logs it at Error with the full exception. Wolverine's interceptor log category is filtered to Error and above, so expected refusals (validation, not found, ...) don't produce warnings.

*Guardrail (as planned):* task 1 verifies, before anything is removed, that (a) Wolverine's interceptor runs for services mapped with plain `MapGrpcService<T>`, (b) unmapped exceptions produce no exception text in the status message or details, and (c) unmapped exceptions are logged. If (b) or (c) fails, we register our own last-in-chain `IGrpcStatusDetailsProvider` that logs and returns a generic `INTERNAL` status, instead of `EnableDefaultErrorInfo()`. If (a) fails, the change stops and the design is revisited.

### D2. Our own `IValidationFailureAdapter`; drop `WolverineFx.FluentValidation.Grpc`

The packaged `FluentValidationFailureAdapter` copies `PropertyName` into `field` unchanged (PascalCase) and `ErrorMessage` into `description`, and leaves `reason` empty. That does not meet the spec. Our adapter, registered in DI where `ValidationExceptionStatusDetailsProvider` finds it:

- `field` = the property name converted to snake_case (the existing `ToSnakeCase` logic moves here)
- `reason` = `ErrorCode` (a `ViolationCodes` constant)
- `description` = FluentValidation's English `ErrorMessage`, for developers only

`UseGrpcRichErrorDetails()` must be on; without it the adapter is never consulted.

### D3. Wolverine's FluentValidation middleware runs validators

`opts.UseFluentValidation()` discovers validators in the application assembly and runs them before the handler. Its default failure action throws `FluentValidation.ValidationException` with every failure, and the adapter maps that. `CreateUserHandler.Validate` is deleted, along with the app's `ValidationException`, `FieldViolation`, `ValidatorExtensions` and `AbstractValidatorExtensions`.

- The validator class must be `public`, or `IncludeInternalTypes` must be set, for discovery to find it.
- Validators take no constructor dependencies. The pasted Wolverine docs warn that injected services can force service location in the generated code, and validators that only emit codes never need one.
- `UpdateUserHandler.ValidateAsync` (the existence check) is not input validation and stays as it is.
- A command is always wholly valid or wholly rejected: the validator reports all violated rules, not just the first. This preserves today's behavior.

*Name clash:* inside the API, `ValidationException` would otherwise resolve to both FluentValidation's type and ours. Deleting ours removes the clash.

### D4. `InvariantViolationException` carries a code

The constructor becomes `InvariantViolationException(string code)`. The code is also passed to the base `Exception` message, so server logs still say what happened. Codes live in `InvariantCodes` (D5). The only production use today is `UpdateUserHandler`, which changes to `InvariantCodes.UserDeleted`. Test fixtures pass a test-local code.

### D5. `OfficeRkyv.Contracts` becomes a class library

A new `OfficeRkyv.Contracts.csproj` compiles `*.proto` with `GrpcServices="Both"` and holds:

- `ViolationCodes` (`required`, `invalid_format`, `too_long`), moved from the API
- `InvariantCodes` (`user_deleted`)
- `ErrorDomain`, the `ErrorInfo.domain` value

The API and the web UI replace their `<Protobuf>` items with a `ProjectReference`. The proto namespaces (`OfficeRkyv.Contracts`, `OfficeRkyv.Contracts.Users`) are unchanged, so no `using` changes. The web UI also gets the server base classes it doesn't use; that is harmless. Packages: `Google.Protobuf`, `Grpc.Core.Api`, `Grpc.Tools` (`PrivateAssets="all"`), pinned in `Directory.Packages.props`.

*Alternative:* link a shared `.cs` file into both projects (A2). That would compile each type twice and keep the duplicate proto types, and the user chose A1.

### D6. Web UI uses Wolverine's typed gRPC client

`AddGrpcClient<T>` becomes `AddWolverineGrpcClient<T>` for both clients. Its client interceptor rethrows a failed call as a typed .NET exception with the original `RpcException` as `InnerException`. `CreateUserErrors.From` therefore looks for the `RpcException` on the exception itself or its inner exception, and reads `violation.Reason` instead of `violation.Description`. The code strings in `Key()` are replaced with the shared `ViolationCodes` constants.

### D7. Package alignment

Every `WolverineFx.*` package goes to 6.41.0: `WolverineFx`, `WolverineFx.Fisher`, `WolverineFx.RuntimeCompilation`, plus the new `WolverineFx.FluentValidation` and `WolverineFx.Grpc`. `WolverineFx.FluentValidation.Grpc` is removed.

## Risks / Trade-offs

- [Wolverine's default mapping differs from ours in ways the unit tests didn't cover: status messages, logging, the `OperationCanceledException` pass-through when the client disconnects] → task 1 verifies this first; we keep behavior-level tests of the mapping (status code, details, no leaked text) rather than tests of an interceptor class.
- [`AddWolverineGrpcClient` in the web UI may expect Wolverine services that a host without `UseWolverine` lacks] → verify while wiring the client. If it needs the Wolverine runtime, fall back to plain `AddGrpcClient` and keep the unwrap in `CreateUserErrors`; nothing else changes.
- [The `reason` move is a breaking wire change] → API and web UI change in the same commit; there are no other clients.
- [Validator discovery silently finds nothing (for example an internal class), so invalid input reaches the handler] → pipeline tests send invalid commands through the bus and assert that a `FluentValidation.ValidationException` is thrown with the expected codes.
- [Test project conflicts after Contracts becomes shared] → the `web` alias may still be needed for other duplicate top-level types (for example `Program`); keep it unless the build proves otherwise.
- [The DI-driven error pipeline is less visible than one switch expression] → all mappings sit in one `UseGrpcRichErrorDetails` block in `Program.cs`, with a comment pointing to the spec.

## Migration Plan

Ship as a single commit: API, Contracts and web UI together. No data or event-schema changes. To roll back, revert the commit.
