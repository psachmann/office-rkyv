# Design

## Context

See proposal.md - Why. Current state that shapes the approach:

- **API**: the `UserService` gRPC service (`Features/Users/UserGrpcService.cs`) sends Wolverine messages. It has no exception handling of its own, so every exception reaches the client as `StatusCode.Unknown`. Users are event-sourced with Fisher on **SQLite**, with an inline snapshot of `User`. `User.Email` carries `[NaturalKey]`, and `User.Create` is `[NaturalKeySource]`, so the key value is **extracted from the `UserCreated` event**, not from the snapshot.
- **Fisher's key behaviour**: on append, Fisher refuses a key that another live stream already holds, throwing `Fisher.Exceptions.DuplicateNaturalKeyException`, a subclass of `JasperFx.Events.DuplicateNaturalKeyException`. On replay or rebuild the lookup is **last-writer-wins and refuses nothing**.
- **Existing failure types**: `InvariantViolationException` (a decider refusing an operation), `JasperFx.Events.EventStreamUnexpectedMaxEventIdException` (stale expected version, already asserted in `UserPipelineTests`), No exception identifies a missing stream: an unknown id with expected version >= 1 throws the same `EventStreamUnexpectedMaxEventIdException` as a stale version (it carries no expected/actual versions, only ids and types), and at version 0 the decider receives a null aggregate.
- **Web**: Blazor Server with global `InteractiveServer` rendering and Radzen. `DialogService` is available through `AddRadzenComponents` and `<RadzenComponents />` in `MainLayout`. `UserService.UserServiceClient` is registered but unused. DotNetQuery 2.0 is registered (30 s refetch, stale immediately) but no page uses it yet. `Home.razor` is the scaffold.
- **Dev data**: the AppHost's `AddSqlite("officerkyv-db")` has no path, so every AppHost start gets a fresh temp database. No data migration is needed.

## Goals / Non-Goals

**Goals:**
- One uniqueness key that is decided once, at write time, and never recomputed.
- Error translation lives in one place for all gRPC services. Handlers keep throwing domain and store exceptions and know nothing about gRPC.
- The UI can tell "fix this field" apart from "email taken" and "something broke" using only status codes and structured details.
- The first real DotNetQuery usage stays small and conventional, so later pages can copy it.

**Non-Goals:**
- A general validation framework. This change validates exactly one command.
- Upcasting old `UserCreated` events. Deferred until real data exists; that's a good learning exercise for its own change.
- Frontend handling for `ABORTED`, `FAILED_PRECONDITION` or `NOT_FOUND`. The page only creates users, so those surface only as the general failure message.

## Decisions

### D1. `NormalizedEmail` is recorded in `UserCreated`, not derived during the fold
`UserCreated` becomes `(Email, NormalizedEmail, DisplayName)`. `User` gains `NormalizedEmail`, which takes over `[NaturalKey]`. `Email` stays a plain property holding the trimmed address as entered. `CreateUserHandler` normalizes once (`Trim().ToLowerInvariant()`) through a small static `EmailAddress.Normalize` in the Users feature. `FindUserByEmailHandler` uses the same function.

*Why:* the natural key is extracted from the event, and replay never refuses a collision. If the key were derived during the fold, or with `NaturalKeyFor().SetBy(e => Normalize(e.Email))`, a later change to the normalization rule would silently re-key existing streams on rebuild. Two users could then merge into one key with no error. Recording the key in the event makes the value that decided uniqueness part of the history.

*Alternatives:* deriving in `Create()` or with `SetBy` (rejected for the reason above); a case-insensitive column collation (Fisher owns the lookup table, and it would tie the rule to SQLite).

*Naming:* the key property is named `NormalizedEmail` on both the event and the snapshot, so `[NaturalKeySource]` maps it by convention.

### D2. Validation is a Wolverine `Validate` step on the create handler that throws `ValidationException`
`CreateUserHandler` gets a static `Validate(CreateUserCommand)` method. Wolverine runs it before `Handle` by naming convention. It collects every violation (see the user-management spec) and throws a new `Shared/ValidationException(IReadOnlyList<FieldViolation>)`. `FieldViolation` is `(string Field, string Code)`. `Field` is the command's property name (`Email`, `DisplayName`). `Code` is a stable machine-readable reason: `required`, `invalid_format` or `too_long`.

*Why:* it matches the existing style, where deciders throw `InvariantViolationException`. It keeps `Handle` free of validation code, and it goes through the real Wolverine pipeline that `UserPipelineTests` already exercises. Stable codes let the UI pick localized text without parsing English.

*Lightweight validation (`Validate` returning `ValidationOutcome` or `string[]`) was tried and rejected:* over `IMessageBus.InvokeAsync<T>` a failing outcome is only logged and the handler is stopped, so the caller receives `null` and no exception. Field errors would never reach the gRPC interceptor. `Validate` therefore returns void and throws.

*Alternatives:* FluentValidation plus Wolverine middleware (a new dependency for two fields, rejected for now); validating in the gRPC mapper (then non-gRPC callers of the bus skip validation, rejected).

The email shape check is a small hand-written check that follows the spec's rule. It does not use `MailAddress`, which accepts display-name forms like `"A" <a@b.c>`, or a large regex.

### D3. One gRPC server interceptor maps exceptions to status codes
`Shared/ExceptionMappingInterceptor : Interceptor` overrides `UnaryServerHandler` and is registered with `AddGrpc(o => o.Interceptors.Add<ExceptionMappingInterceptor>())`. It handles unary calls only, because no streaming RPCs exist.

| Exception | Status | Detail |
|---|---|---|
| `ValidationException` | `InvalidArgument` | `google.rpc.BadRequest`, one `FieldViolation` per violation: `field` = snake_case of the property name (`display_name`, matching the proto), `description` = the code |
| `JasperFx.Events.DuplicateNaturalKeyException` | `AlreadyExists` | none |
| `InvariantViolationException` | `FailedPrecondition` | domain message (app-authored, safe) |
| `EventStreamUnexpectedMaxEventIdException` | `Aborted` | none |
| `NotFoundException` | `NotFound` | none |
| `RpcException` | passed through unchanged | none |
| anything else | `Internal` | generic message only; the exception is logged with `ILogger` |

The duplicate-key row catches the JasperFx base type so that both the Fisher subclass and a future store's exception match. The PascalCase-to-snake_case conversion keeps field names in the API layer and out of the domain: the domain reports its own property names, and the edge translates them into the contract's names.

Rich status uses `Grpc.StatusProto` (`Google.Rpc.Status` plus `ToRpcException()`) on the server and `RpcException.GetRpcStatus()` / `GetDetail<BadRequest>()` on the client.

*Alternatives:* try/catch in every service method (repetitive, and easy to forget in the next service); a plain status with the message as the only detail (the UI would have to parse text, rejected).

**Unknown ids are reported as not found by the handlers, not inferred by the interceptor.** `UpdateUserHandler` and `DeleteUserHandler` each have a `ValidateAsync` step (via a shared `UserExistence.RequireAsync`) that loads the user and throws `NotFoundException` when absent. Wolverine runs `Validate` before the decider loads the stream and checks the expected version, so the check precedes the conflict it would otherwise be mistaken for. A soft-deleted user still exists, so it passes and the decider applies its own rules (`FailedPrecondition` for a rename, a no-op for a repeat delete). The cost is one extra read per update or delete; a user removed between the read and the write still surfaces as `Aborted`.

### D4. Web: DotNetQuery query and mutation, rendered through `Transition`
- `Home.razor` creates an `IQuery<Unit, IReadOnlyList<UserDto>>` through `IQueryClient.CreateQuery`. The key is `QueryKey.From("users")` and the fetcher calls `ListUsersAsync`. It's rendered with `<Transition>`, not `<Suspense>`, so the 30 s background refetch doesn't blank the grid every time. The success content branches on count: zero shows the empty-state card, more than zero shows a `RadzenDataGrid<UserDto>` with Email and Display name columns. The failure template shows the localized load error, so a failure is never shown as "no users".
- `CreateUserDialog.razor` (in `Components/Pages/Users/`) holds a `RadzenTemplateForm`. The hints are `RadzenRequiredValidator` on both fields and `RadzenEmailValidator` on email. Its mutation (`IQueryClient.CreateMutation`) calls `CreateUserAsync` and sets `InvalidateKeys = [users]`, so the list refetches on success. The dialog then closes through `DialogService.Close`.
- Both the page's "New user" button and the empty-state button call `DialogService.OpenAsync<CreateUserDialog>`, so one form serves both states.
- On `RpcException` the dialog keeps its model, which preserves what was entered, and maps:
  - `InvalidArgument`: each `BadRequest` field violation goes to that field's error text by `(field, code)`, for example `email`/`invalid_format` becomes `Errors.EmailInvalid`. Unknown pairs fall back to a generic "invalid" text for that field.
  - `AlreadyExists`: `Errors.EmailTaken` under the email field.
  - anything else, including `Unavailable`: `Errors.CreateFailed` shown as a `RadzenAlert`.
- Server-side field errors are shown as text under each field. They're cleared on the next submit, and they don't feed Radzen's validator pipeline, which only knows client rules.
- All text lives in `Resources/Components/Pages/Home.resx` and `Resources/Components/Pages/Users/CreateUserDialog.resx`, per CLAUDE.md.

*Alternatives:* an inline form above the grid (it clutters the everyday view, and the dialog was the agreed choice); calling the gRPC client directly without DotNetQuery (it misses the cache and invalidation pattern the app has already committed to).

### D5. Functional-test shape
The Aspire functional tests start a fresh AppHost, which means an empty temp SQLite database, shared per test session. Because of that, the landing-page tests run in order inside one class. First they assert the empty state, then create a user through the dialog and assert the grid. Then they submit the same email in a different case and assert the localized "already in use" text. The old `HelloWorldTest` is removed.

## Risks / Trade-offs

- **[Risk] Changing the event shape breaks deserialization of existing dev events.** → The dev database is a temp file recreated on each AppHost start. Tell the developer to restart the AppHost after pulling. The unit tests already use a fresh temp database for each test.
- **[Risk] Snake-casing is a guess at the proto field name.** → It holds for the current proto conventions. A unit test asserts `DisplayName` becomes `display_name`, so a mismatch fails loudly.
- **[Trade-off] A deleted user keeps their email claimed**, because a soft delete doesn't release the natural key. → Out of scope (see proposal). Creating a user with the email of a deleted user returns `ALREADY_EXISTS`.
- **[Risk] Functional tests depend on order and share one AppHost.** → Keep them in one sequential class, and don't start them while the developer's own AppHost is running (they would compete for the same resources).
- **[Trade-off] `Validate` must keep up with `Handle`.** Normalization happens in `Handle`, so `Validate` checks the trimmed raw input. The two share a single trim helper, so they can't disagree about whitespace.

## Migration Plan

No production data exists. After merging, restart the AppHost to get a fresh SQLite database. Rolling back means reverting the change and restarting again.

## Open Questions

- Should `InvariantViolationException` messages be sent to clients at all once other clients exist? They're safe today because the app writes them, but they're English. The UI in this change doesn't display them either way.
