# Tasks

## 1. Normalized email (backend)

- [ ] 1.1 Add `EmailAddress.Normalize` (trim + `ToLowerInvariant`) in `Features/Users`; verify with unit tests covering mixed case, surrounding whitespace, and an already-normalized input
- [ ] 1.2 Change `UserCreated` to `(Email, NormalizedEmail, DisplayName)`, add `User.NormalizedEmail` carrying `[NaturalKey]` (remove it from `Email`), and populate both in `User.Create`; verify `dotnet build` succeeds and the existing `DuplicateEmail_IsRefusedByTheStore_NotByAHandlerPreCheck` test still passes
- [ ] 1.3 Make `CreateUserHandler` store the trimmed email as `Email`, the normalized form as `NormalizedEmail`, and the trimmed display name; verify with unit tests: `Alice@Example.COM` then `alice@example.com` is refused with `DuplicateNaturalKeyException`, the returned `Email` keeps its original case, and `"  Alice  "` is stored as `Alice`
- [ ] 1.4 Make `FindUserByEmailHandler` normalize its input and match on `NormalizedEmail`; verify with unit tests that a lookup in different case or with surrounding whitespace finds the user, and an unknown email returns null

## 2. Server-side validation

- [ ] 2.1 Add `Shared/ValidationException` carrying `IReadOnlyList<FieldViolation(Field, Code)>`, with codes `required`, `invalid_format`, `too_long`; verify `dotnet build` succeeds
- [ ] 2.2 Add a static `Validate(CreateUserCommand)` to `CreateUserHandler` implementing every rule in the user-management spec (required, email shape, 254/100 length limits), collecting all violations before throwing; verify with unit tests through the Wolverine bus: empty email + blank display name gives two violations, `alice.example.com` gives `Email`/`invalid_format`, over-long values give `too_long`, and a rejected create leaves no user in `FindAllUsersQuery`

## 3. gRPC error mapping

- [ ] 3.1 Add `Grpc.StatusProto` to `Directory.Packages.props` and reference it from `OfficeRkyv.Api` and `OfficeRkyv.Web`; verify `dotnet build` succeeds for the solution
- [ ] 3.2 Write a unit test that updates a never-created user id through the bus and records which exception type is thrown; confirm it is one of `RequiredDataMissingException` / `NonExistentStreamException` (adjust the mapping in 3.3 if it is something else)
- [ ] 3.3 Implement `Shared/Grpc/ExceptionMappingInterceptor` per the design D3 table (including `BadRequest` details with snake_case field names, `RpcException` pass-through, and `Internal` with a generic message plus server-side logging) and register it in `AddGrpc`; verify with unit tests invoking the interceptor with a stub continuation for each exception kind, asserting the status code, that `DisplayName` maps to `display_name` in the `BadRequest` details, and that the `Internal` message contains none of the original exception text

## 4. Landing page (web)

- [ ] 4.1 Create `Components/Pages/Users/CreateUserDialog.razor` + `Resources/Components/Pages/Users/CreateUserDialog.resx`: `RadzenTemplateForm` with email and display-name fields, `RadzenRequiredValidator`/`RadzenEmailValidator` hints, a DotNetQuery create mutation invalidating the `users` key, closing via `DialogService.Close` on success; verify by running the app and confirming empty-field submit is blocked client-side with both fields flagged
- [ ] 4.2 Add server-error handling to the dialog per design D4: `InvalidArgument` field violations to per-field localized text by `(field, code)`, `AlreadyExists` to `EmailTaken` under email, anything else to a `CreateFailed` alert; the model is kept so entries survive; verify by running the app and submitting a duplicate email in different case, observing the localized "already in use" text under the email field
- [ ] 4.3 Replace `Home.razor` with the landing page: users query (`QueryKey.From("users")`, `ListUsersAsync`) rendered via `<Transition>`, empty-state card with a create button, a `RadzenDataGrid` of email + display name with a "New user" button, and a localized load-error failure template; update `Home.resx` (remove the scaffold strings); verify by running the app: no users shows the empty state, creating a user replaces it with the grid without a reload, and stopping the API shows the load error rather than the empty state
- [ ] 4.4 Confirm no hardcoded user-facing strings remain in the new/changed components (all text via `IStringLocalizer`); verify by reviewing the `.razor` files against the `.resx` keys

## 5. Tests & verification

- [ ] 5.1 Replace `HelloWorldTest` in the functional tests with an ordered landing-page test class per design D5 (empty state, create via dialog then grid row appears, same email in different case shows the localized "already in use" text); verify the functional test suite passes (only when no developer AppHost is running)
- [ ] 5.2 Run the full unit test suite and `dotnet build` with warnings as configured; verify both succeed with no new warnings
