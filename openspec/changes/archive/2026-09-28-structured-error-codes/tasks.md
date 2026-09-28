# Tasks

## 1. Verify Wolverine's gRPC error pipeline (design D1 guardrail)

- [x] 1.1 Bump every `WolverineFx.*` package to 6.41.0, add `WolverineFx.Grpc` and `WolverineFx.FluentValidation` to the API, and remove `WolverineFx.FluentValidation.Grpc` from `Directory.Packages.props` and the API csproj; verify `dotnet build` restores with no NU1605/NU1608 warnings
- [x] 1.2 In a throwaway test, add `AddWolverineGrpc()` + `UseGrpcRichErrorDetails(cfg => cfg.EnableDefaultErrorInfo())` next to the existing setup and send an unmapped exception through `UsersServiceImpl` (still registered with `MapGrpcService<T>`). Record in design.md whether (a) Wolverine's interceptor ran, (b) the status message or details contain any exception text, (c) the failure was logged, and (d) a cancelled call is passed through without being logged as an error
- [x] 1.3 If 1.2 (b), (c) or (d) failed, plan a custom last-in-chain `IGrpcStatusDetailsProvider` that logs and returns a generic `INTERNAL` status (used in 3.4). If (a) failed, stop and revisit the design with the user. Verify by updating design.md D1 with the outcome

## 2. Shared Contracts library (design D5)

- [x] 2.1 Create `src/OfficeRkyv.Contracts/OfficeRkyv.Contracts.csproj` compiling `*.proto` with `GrpcServices="Both"`, pin `Google.Protobuf`, `Grpc.Core.Api` and `Grpc.Tools` (`PrivateAssets="all"`) in `Directory.Packages.props`, and add the project to `OfficeRkyv.slnx`; verify `dotnet build src/OfficeRkyv.Contracts` succeeds
- [x] 2.2 Add `ViolationCodes` (`required`, `invalid_format`, `too_long`), `InvariantCodes` (`user_deleted`) and `ErrorDomain` to Contracts, and delete `ViolationCodes` from the API; verify the API compiles against the moved constants
- [x] 2.3 Replace the `<Protobuf>` items in the API and Web csproj files with a `ProjectReference` to Contracts; verify the whole solution builds and the unit tests pass unchanged. Drop the `web` alias on the test project only if the build proves it is no longer needed

## 3. API: validation and error mapping (design D1-D4)

- [x] 3.1 Rename the WIP validator class and file to agree, make it `public`, and have it emit the shared `ViolationCodes`; verify with a unit test that validates a command directly and checks property names and error codes for empty, too-long and malformed inputs
- [x] 3.2 Enable `opts.UseFluentValidation()`, delete `CreateUserHandler.Validate`, the static validator field, `ValidatorExtensions`, `AbstractValidatorExtensions`, and the app's `ValidationException`/`FieldViolation`; verify the `UserPipelineTests` create-validation tests, rewritten to expect `FluentValidation.ValidationException` with the same `(PropertyName, ErrorCode)` pairs, pass through the bus
- [x] 3.3 Implement the app's `IValidationFailureAdapter` (snake_case `field`, `reason` = error code, `description` = English message) and register it; verify with unit tests that a `FluentValidation.ValidationException` becomes the expected `BadRequest` field violations, including the snake_case conversion formerly covered by `ExceptionMappingInterceptorTests`
- [x] 3.4 Change `InvariantViolationException` to take a code, update `UpdateUserHandler` to `InvariantCodes.UserDeleted` and the event-sourcing test fixtures to a test code; verify `DecideTests`, `EventSourcingPipelineTests`, `MaterializedEntityStoreTests` and the deleted-user rename test pass
- [x] 3.5 In `Program.cs`, register `AddWolverineGrpc()` and `UseGrpcRichErrorDetails` with the D1 mappings (`ErrorInfo` for invariants, `ALREADY_EXISTS`, `ABORTED`, `NOT_FOUND`, generic `INTERNAL` per the task 1 outcome) and generic status messages, then delete `ExceptionMappingInterceptor` and its registration; verify the API starts and `dotnet build` has no references left to the removed types
- [x] 3.6 Replace `ExceptionMappingInterceptorTests` and the `MappedFailureOf` helper in `UserPipelineTests` with tests that run each exception through the real Wolverine gRPC pipeline (resolved from the test host). Verify: invalid input → `INVALID_ARGUMENT` with `reason` codes, invariant → `FAILED_PRECONDITION` with `ErrorInfo` reason `user_deleted`, duplicate → `ALREADY_EXISTS`, stale version → `ABORTED`, missing → `NOT_FOUND`, unmapped → `INTERNAL` with no exception text

## 4. Web UI (design D6)

- [x] 4.1 Add `WolverineFx.Grpc` to Web and switch both clients to `AddWolverineGrpcClient<T>`; verify the Web app starts and the landing page lists users. If the client needs a Wolverine runtime the Web host lacks, fall back to `AddGrpcClient<T>`, note it in design.md D6, and continue
- [x] 4.2 Update `CreateUserErrors` to find the `RpcException` on the exception or its `InnerException`, read `violation.Reason`, and match on the shared `ViolationCodes`; verify with updated `CreateUserErrorsTests`, including a wrapped-exception case, that per-field, already-in-use and general messages are unchanged
- [x] 4.3 Update `UsersFacadeTests` if the failure type they observe changes; verify they pass

## 5. Final verification

- [x] 5.1 Run the full unit test suite and `dotnet build` for the solution with zero warnings introduced by this change
- [x] 5.2 With the app running, submit the create-user form with an empty email and a too-long display name, then with an existing email; verify that the localized per-field and already-in-use messages appear exactly as before
- [x] 5.3 Run `openspec validate structured-error-codes --strict` and verify it passes
