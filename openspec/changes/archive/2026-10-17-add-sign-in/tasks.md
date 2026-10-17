# Tasks

## 1. Check the risky assumption first

- [x] 1.1 Verify design D9: register a gRPC client with `AddCallCredentials((context, metadata, services) => ...)` through `AddWolverineGrpcClient(...).HttpClientBuilder`, resolve the client inside a DI scope holding a scoped marker service, and confirm the delegate's `services` resolves that scope's instance and not the root's. Write it as a UI-logic test (`CallerTokenCredentialsTests`) that stays in the suite. If it fails, switch to D9's fallback and note it in design.md before going on.

## 2. Contracts and packages

- [x] 2.1 Add `Microsoft.AspNetCore.Authentication.JwtBearer` (Api) and `Microsoft.IdentityModel.JsonWebTokens` (UI) to `Directory.Packages.props` and the two project files; verify `just build` succeeds.
- [x] 2.2 Add `CallerTokens` to `OfficeRkyv.Contracts` (issuer `office-rkyv-ui`, audience `office-rkyv-api`, minimum key length 32, lifetime 5 minutes, setting name `CallerTokens:SigningKey`); verify the build.
- [x] 2.3 Remove `owner_id` from `ListArchivesRequest`, `CreateArchiveRequest`, `ListInboxRequest`, `SearchDocumentsRequest` and `UploadDocumentHeader`, marking the field numbers `reserved`, and update the comments that mention an owner; verify the build shows the expected compile errors in the mappers and facades, which the next groups fix.

## 3. API: identifying the caller

- [x] 3.1 Add `UnauthenticatedException` and `PermissionDeniedException` under `Shared/Errors`, and map them in `UseApiErrorMapping`/`ErrorStatusProvider` to `UNAUTHENTICATED` and `PERMISSION_DENIED` with fixed messages and no details; verify with pipeline tests in `Shared/Errors` that a unary and a streaming call each carry the status with no details.
- [x] 3.2 Add `AddOfficeRkyvApi`'s `callerTokenKey` parameter: register JwtBearer authentication validating signature, issuer, audience and lifetime from `CallerTokens`, and refuse a key shorter than the minimum. `MapOfficeRkyvApi` calls `UseAuthentication`. Verify at the pipeline level once `PipelineHost` is updated (3.4).
- [x] 3.3 Add the scoped `CallerAccessor` in `Shared/Auth` with `RequireAsync(ServerCallContext)`: `sub` from the authenticated principal, then `FindUserByIdQuery`; a missing principal, an unparsable id, or no live user throws `UnauthenticatedException`. Verify with the pipeline tests of 3.5.
- [x] 3.4 Give `PipelineHost` a fixed test key passed to `AddOfficeRkyvApi`, `TokenFor(userId, key?, expires?)`, `CallOptionsFor(userId)` and a channel with `UnsafeUseInsecureChannelCallCredentials`; make `TestDocuments.UploadAsync`/`DownloadAsync` take the caller; verify the existing pipeline tests compile against it.
- [x] 3.5 Pipeline tests for the `api-authorization` requirement "Archive and document calls identify the caller": no token, wrong key, expired, unknown user, deleted user, and an upload without a token storing no blob (check `PipelineHost.BlobDirectory` has no content file); verify they pass.

## 4. API: ownership

- [x] 4.1 Archives: the service resolves the caller in every method; `CreateArchiveCommand`/`FindArchivesByOwnerQuery` get the caller as owner; `RenameArchiveCommand`/`DeleteArchiveCommand` gain `CallerId`; `ArchiveExistence.RequireAsync` takes the caller and treats another owner's archive as not found; mapper updated. Verify with pipeline tests in `ArchivePipelineTests` for creating, listing, and renaming or deleting another user's archive (`NOT_FOUND`, unchanged).
- [x] 4.2 Documents: the same for `FindInboxQuery`, `SearchDocumentsQuery`, the upload header's owner, and `CallerId` on rename, delete, file and unfile; `DocumentExistence.RequireAsync` takes the caller. Verify with pipeline tests in `DocumentPipelineTests` and `DocumentSearchTests` for each "another user's document is not found" scenario and the caller-only lists and search.
- [x] 4.3 `FindArchiveDocumentsQuery` gains `OwnerId` and returns an empty list for another user's archive. Verify with the pipeline test "Listing another user's archive".
- [x] 4.4 `FileDocumentHandler` throws `NotFoundException` instead of `archive_owner_mismatch` for another user's archive, keeping `InvariantCodes.ArchiveOwnerMismatch` in Contracts. Verify with the pipeline test "Filing into another user's archive" (`NOT_FOUND`, document unmoved), replacing the old mismatch test.
- [x] 4.5 `DownloadDocument` resolves the caller and answers another user's document as not found before streaming. Verify with the pipeline test "Downloading another user's document".
- [x] 4.6 Users: `UpdateUser`/`DeleteUser` require the caller and throw `PermissionDeniedException` unless it is the target; the other user RPCs stay open. Verify with pipeline tests in `UserPipelineTests` (list signed out, rename yourself, rename someone else, update an unknown user).
- [x] 4.7 Move every remaining archive, document, deletion-race and search pipeline test from `owner_id` to calling as a user; verify `dotnet test --project tests/OfficeRkyv.UnitTests` is green.
- [x] 4.8 Keep the bus caller-free: the seed and the cascade still pass the user explicitly. Verify `DevelopmentSeedTests` and the cascade pipeline tests (`DeleteArchiveTests`, `ArchiveDeletionRaceTests`) pass unchanged in intent.
- [x] 4.9 Extend `ApiTelemetryTests` (pipeline level) with "no span carries the token"; verify it passes.

## 5. API server and AppHost

- [x] 5.1 The API server reads `CallerTokens:SigningKey` and passes its UTF-8 bytes to `AddOfficeRkyvApi`; a missing key fails start-up with an exception naming the setting. Verify by starting `api-svc` without it (the exception message) and through `just dev` (starts).
- [x] 5.2 The AppHost declares one generated secret parameter and passes it to `api-svc` and `web-svc` as `CallerTokens__SigningKey`, confirming `GenerateParameterDefault`'s API against Aspire 13.4 docs; verify `just dev` starts both resources and the Aspire dashboard shows the parameter as secret.

## 6. UI library: the sign-in seam

- [x] 6.1 Add `OfficeRkyv.UI.Auth`: `ISignInService`, `SignedInUser.IdOf(ClaimsPrincipal)`, `RedirectToLanding`, and the caller-token issuer with `CallerTokenCredentials.AddTo(IHttpClientBuilder, byte[] key)`; `AddOfficeRkyvUi` adds `AddAuthorizationCore` and `AddCascadingAuthenticationState`. Verify with UI-logic tests: a fake client sees a valid bearer token for the signed-in user and no header when signed out (extending 1.1).
- [x] 6.2 Facades: `ArchivesFacade`, `DocumentsFacade` and `UploadAsync` stop sending an owner; `WorkspaceKeys` keep the owner id. Verify with `WorkspaceFacadeTests` (UI logic) updated and green.
- [x] 6.3 `WorkspaceErrors` maps `UNAUTHENTICATED` to a signed-out result, and the workspace's helper calls `ISignInService.SignOutAsync()` for it. Verify with UI-logic tests in `WorkspaceErrorsTests`.
- [x] 6.4 Landing page: cards per D10, ordered by display name ignoring case, a card is a button named by the display name, the create card, and the empty state; choosing a card or creating a user calls `SignInAsync`; a signed-in visitor is sent to `/workspace`. Localized texts in `Home.resx` and `Home.de.resx`, with unused grid keys removed. Verify `TranslationCompletenessTests` (unit) passes.
- [x] 6.5 Workspace: routes `/workspace` and `/workspace/archives/{ArchiveId:guid}`, `[Authorize]`, user id from the authentication state, the not-found card replaced by signing out (D11), and an archive id that is not the user's falls back to the inbox. Verify the build; behaviour is covered in group 9.
- [x] 6.6 Sidebar: `MainLayout` shows the signed-in user's display name and a sign-out button above Settings through `AuthorizeView`, with texts in `MainLayout.resx`/`.de.resx`. Verify `TranslationCompletenessTests` passes.

## 7. Web host

- [x] 7.1 Add `SignInTickets` (single use, 60 s, action-bound, random 32 bytes) and unit tests for redeeming once, expiry, and a sign-out ticket not signing in; verify they pass.
- [x] 7.2 Cookie authentication, `UseAuthentication`/`UseAuthorization`, and the endpoints `/account/sign-in` and `/account/sign-out`, which redeem a ticket, set or clear the cookie and redirect (`/workspace` or `/`; `/` for a bad ticket). Verify with functional tests in 9.2.
- [x] 7.3 `WebSignInService` implements `ISignInService` (ticket, then navigation with `forceLoad`); `Routes.razor` uses `AuthorizeRouteView` with `RedirectToLanding`; the three gRPC clients get `CallerTokenCredentials` with the configured key. Verify `just dev`: choosing the demo user opens its workspace with the seeded documents.

## 8. Desktop host

- [x] 8.1 `DesktopHost` makes a random 32-byte key at start and passes it to `DesktopApi.StartAsync` and `AddSocketClients`, which adds `CallerTokenCredentials` and `UnsafeUseInsecureChannelCallCredentials`. Verify with `DesktopApiTests` (unit): a socket call without a token gets `UNAUTHENTICATED`, one with a token signed by the key succeeds.
- [x] 8.2 `DesktopSignIn` is one singleton registered as `AuthenticationStateProvider` and `ISignInService`; it logs `Signed in as user {UserId}` and `Signed out` through `[LoggerMessage]` at Information; `Main.razor` uses `AuthorizeRouteView`. Verify with a unit test of the state changes and the raised `AuthenticationStateChanged`.
- [x] 8.3 By hand, `just desktop`: the window opens on the landing page with cards. Choose a user and observe `Signed in as user <id>` in stdout and that user's workspace. Sign out from the sidebar and observe `Signed out` and the landing page. Create a user and observe a `Signed in as user` line with the new id and an empty workspace. Upload, file and delete an archive as in the existing desktop scenario. Close and restart, and observe the landing page with nobody signed in.

## 9. Functional tests

- [x] 9.1 `WorkspacePage.OpenNewUsersWorkspaceAsync` creates the user through the dialog and expects `/workspace` headed with the new name; update the page objects for cards; verify `WorkspaceTests` and `SettingsTests` pass (functional), rerunning a known flaky settings test once if it is the only failure.
- [x] 9.2 New functional tests (`SignInTests`): signing in from a card, reload stays signed in, sign-out returns to the landing page and `/workspace` then shows it, a signed-out `/workspace` shows the landing page, a signed-in `/` shows the workspace, settings while signed out, a forged ticket signs nobody in, two browser contexts as two users see only their own documents, and the old `/users/{id}` address shows not-found. Verify they pass.
- [x] 9.3 Rewrite `LandingPageTests` for cards (order by display name, empty state, create signs in, hints, duplicate email, server-only validation, users listed after a reload) with TUnit assertions, and remove its `[Skip]`; verify they pass (functional).
- [x] 9.4 Run `just test` and `just test Release`; verify both are green with warnings as errors.

## 10. Docs and TODOS

- [x] 10.1 Update `docs/architecture.md` (request path with the token, host table rows for sign-in and key, `ISignInService` among the host interfaces) and the matching `context` in `openspec/config.yaml`; verify both describe the same path.
- [x] 10.2 Update `docs/api.md` (the caller, `CallerId`/`OwnerId` on commands, ownership in the existence check, the two refusals in the failures table, "resolve the caller" in "Adding an RPC", the service rule's exception); verify against the code.
- [x] 10.3 Update `docs/ui.md` (the sign-in seam, web tickets and endpoints, routes, the signed-out case in failures, German terms for "Sign in"/"Sign out"); verify against the code.
- [x] 10.4 Update `docs/desktop.md` (in-memory app-wide sign-in, per-process key, call credentials over the socket, the two log lines); verify against the code.
- [x] 10.5 Update `docs/testing.md` (calling as a user in pipeline tests, signing in in functional tests, `SignInTests`); verify against the tests.
- [x] 10.6 Update `docs/observability.md` (tokens and cookies never in spans or logs, IdentityModel's PII switches stay off, the desktop's two messages); verify against the code.
- [x] 10.7 Update `TODOS.md` as design.md's "Guideline docs to update" lists: remove the authentication item and the skipped-`LandingPageTests` item, add web passkeys, desktop passkeys, remembering the desktop's last user and asymmetric caller keys, and update the folder-watcher and search-box items; verify each new item says why it was postponed and links this change's design.
