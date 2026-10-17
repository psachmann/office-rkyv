# Design

## Context

See proposal.md for why. The state this design starts from:

- **No identity anywhere.** `Workspace.razor` takes `UserId` from `/users/{id}` and passes it as `owner_id` to `ListArchives`, `CreateArchive`, `ListInbox`, `SearchDocuments` and the upload header. The API trusts it. `RenameDocument`, `DeleteDocument`, `FileDocument`, `UnfileDocument`, `DownloadDocument`, `RenameArchive`, `DeleteArchive` and `ListArchiveDocuments` take only a resource id and check no owner.
- **Two hosts, one UI.** The web host (Blazor Server) calls the API over `https://api-svc`. The desktop host runs the API in its own process and calls it over a unix socket with plain HTTP/2. The UI library knows no host; what differs reaches it through an interface the host implements (`IUserSettingsService`, `IFileSaver`).
- **Bus-level callers exist that are not users at a keyboard.** The development seed (`DemoDataSeeder`) creates a user, an archive and documents through the bus and `DocumentIngest`. The archive-deletion cascade (`ReturnDocumentToInbox`) runs from the outbox. The folder watcher in `TODOS.md` would be a third.
- **One place maps failures** (`UseApiErrorMapping`, `ErrorStatusProvider`), and the UI explains a refusal from its status and code (`WorkspaceErrors`, `CreateUserErrors`).

## Goals / Non-Goals

**Goals:**

- The API decides who is calling, and every archive and document operation is limited to the caller's own data.
- The credential is replaceable: passkeys (web) or a native authenticator (desktop) later change how a host signs a user in, not the API, the token or the pages.
- Both hosts use one mechanism for the API call. No host gets a bypass.

**Non-Goals:**

- Any secret: passwords, passkeys, OIDC. The sign-in is choosing a user.
- Hiding the user list or restricting user creation. The landing page stays public.
- Roles, sharing, or an admin who can act for others.
- Encryption at rest, or protecting the desktop's files from the OS account that owns them.
- Remembering the desktop's last user across restarts.

## Decisions

### D1. The caller is a short-lived signed token, issued by the UI host and validated by the API

On every gRPC call, the UI host sends `Authorization: Bearer <token>`. The token is a JWT signed with HMAC-SHA256. Its `sub` is the user id, its issuer is `office-rkyv-ui`, its audience is `office-rkyv-api`, and it lives for 5 minutes. The API validates it with `Microsoft.AspNetCore.Authentication.JwtBearer` (signature, issuer, audience and lifetime, with the default clock skew). The names (issuer, audience, the key's minimum length) are constants in `OfficeRkyv.Contracts` (`CallerTokens`), since both sides of the wire need them. The token is issued in the UI library with `Microsoft.IdentityModel.JsonWebTokens`, so both hosts share the issuing code.

Alternatives:

- **A trusted header with the user id.** Simplest, but anything that can reach `api-svc` can claim any user. It also gives passkeys nothing to build on.
- **Forwarding the web cookie to the API.** Ties the API to the web host's cookie format and data-protection keys, and the desktop has no cookie.
- **An OpenID Connect server issuing tokens.** The right shape once there are external clients. Far too much for a placeholder credential.

Verified at the pipeline level: no token, a wrong key, wrong audience and an expired token are all refused (`api-authorization`).

### D2. The API checks the caller in the services, and refuses through the error mapping

`AddOfficeRkyvApi` registers JwtBearer authentication, and `MapOfficeRkyvApi` adds `UseAuthentication`. **No `[Authorize]`, no `RequireAuthorization`.** Instead, a scoped `CallerAccessor` in `Shared/Auth` gives the services `RequireAsync(ServerCallContext)`. It reads `sub` from the authenticated principal, then loads the user with `FindUserByIdQuery`, which already answers "not found" for a deleted user. A missing, invalid or expired token, or a user that is unknown or deleted, throws `UnauthenticatedException`. `UseApiErrorMapping` maps that to `UNAUTHENTICATED` with a fixed message and no details. `PermissionDeniedException` maps to `PERMISSION_DENIED` the same way.

Every archive and document service method calls `RequireAsync` first. The upload calls it before it reads the header, so a refused upload stores no content.

Why not `[Authorize]`: the authorization middleware answers with HTTP 401, which the gRPC client turns into `UNAUTHENTICATED` with a message it makes up itself. That bypasses the one place that maps failures, and the `api-error-mapping` spec says statuses carry the API's own fixed text. It also could not check that the user still exists without a custom handler that loads the user anyway.

This departs slightly from [api.md](../../../docs/api.md) ("a service maps, dispatches and maps back"): a service now also resolves its caller. api.md is updated to say so. The rule stays that services own no business rule.

Cost: one snapshot read per call. That is acceptable on SQLite, and it is the same read the handlers' `UserExistence` checks already do for creates.

Verified at the pipeline level: unknown and deleted callers, and the status with no details for unary and streaming calls.

### D3. Commands carry the acting user, and the existence check becomes an ownership check

The services fill the acting user into the command or query from the caller:

- **Creates and owner-scoped queries keep `OwnerId`** (`CreateArchiveCommand`, `FindArchivesByOwnerQuery`, `FindInboxQuery`, `SearchDocumentsQuery`, `UploadHeader`). The service sets it to the caller, never to a request field.
- **Commands on an existing aggregate gain `Guid CallerId`** (`RenameArchiveCommand`, `DeleteArchiveCommand`, `RenameDocumentCommand`, `DeleteDocumentCommand`, `FileDocumentCommand`, `UnfileDocumentCommand`). The feature's existence check (`ArchiveExistence.RequireAsync`, `DocumentExistence.RequireAsync`) takes the caller, and throws `NotFoundException` when the aggregate is missing or its `OwnerId` is not the caller. It already runs before the stream is loaded at the expected version, so the version check does not reveal anything about someone else's aggregate either.
- **`FindArchiveDocumentsQuery` gains `OwnerId`.** The handler returns an empty list unless the archive is the owner's.
- **Download** loads the document as before and treats another owner's document as not found.
- **Filing:** once the document is known to be the caller's, "the archive belongs to another owner" is the same as "the archive is not the caller's", so `FileDocumentHandler` throws `NotFoundException` where it threw `archive_owner_mismatch`. `InvariantCodes.ArchiveOwnerMismatch` and its UI text stay, because codes are never removed or reused.

The bus stays caller-free. Handlers never read an `HttpContext`, and the seed, the cascade and a later folder watcher go on naming the user explicitly. Authorization is the service's job of filling these fields from the caller, plus the ownership comparison in the handlers.

Alternative: a Wolverine middleware that reads the caller from an ambient context. Rejected, because it would make the seed and the cascade need a fake caller, and the identity would be invisible in the command records.

Verified at the pipeline level: each "another user's ... is not found" scenario, and the search and lists returning only the caller's data. Existing pipeline tests that create data for two users keep covering isolation.

### D4. The user service stays open, except changing a user

`ListUsers`, `FindUserById`, `FindUserByEmail` and `CreateUser` take no caller, because the landing page calls them signed out. `UpdateUser` and `DeleteUser` call `RequireAsync` and throw `PermissionDeniedException` unless the caller is the target user, whether or not the target exists. The UI calls neither today, so this is purely the API's protection.

Verified at the pipeline level.

### D5. The UI's seam: `AuthenticationStateProvider` to read, `ISignInService` to act

- **Reading.** The UI reads the signed-in user only through Blazor's `AuthenticationStateProvider` and the cascaded `Task<AuthenticationState>`. The user id is the `NameIdentifier` claim, read through one helper (`SignedInUser.IdOf(ClaimsPrincipal)`).
- **Acting.** Signing in and out goes through `ISignInService` (`SignInAsync(Guid userId)`, `SignOutAsync()`), in `OfficeRkyv.UI.Auth`, which each host implements. This is the same pattern as `IUserSettingsService`, and for the same reason: the web needs a real navigation to change a cookie, the desktop does it in place.
- **Routing.** `AddOfficeRkyvUi` adds `AddAuthorizationCore` and `AddCascadingAuthenticationState`. Both routers (`Routes.razor` on the web, `Main.razor` on the desktop) switch `RouteView` to `AuthorizeRouteView`, whose `NotAuthorized` renders `RedirectToLanding` (in the UI library), which navigates to `/`. The workspace is `[Authorize]`. The landing page navigates a signed-in visitor to `/workspace`, replacing the history entry. Settings and not-found stay anonymous.
- **Sidebar.** `MainLayout` shows an `AuthorizeView` at the bottom, above Settings: the display name from `UsersFacade.FindUserById` (shared with the workspace heading) and a sign-out button.

Alternative: a custom `ICurrentUser` service of our own. Rejected, because `AuthorizeRouteView`, `AuthorizeView` and `[Authorize]` already work with the built-in provider in both Blazor Server and `BlazorWebView`, and a passkey sign-in on the web produces exactly this principal.

Verified at the UI-logic level (the helper, the desktop provider) and the functional level (redirects, sidebar).

### D6. Web: a cookie, set by an endpoint that redeems a one-time ticket

A Blazor Server circuit cannot set a cookie, so a sign-in has to be a real request. That's the same reason the language switch goes through `/culture/set`.

1. `WebSignInService.SignInAsync(userId)` runs in the circuit. It asks the singleton `SignInTickets` for a ticket: 32 random bytes, base64url, valid for 60 seconds, usable once, held in `IMemoryCache`, and naming the action (sign in as this user, or sign out). It then navigates with `forceLoad: true` to `/account/sign-in?ticket=…` (or `/account/sign-out?ticket=…`).
2. The endpoint redeems the ticket. For a valid sign-in ticket, it calls `HttpContext.SignInAsync` with the cookie scheme (claim `NameIdentifier`) and redirects to `/workspace`. For sign-out, it signs out and redirects to `/`. For a missing, expired or used ticket, it redirects to `/` and changes nothing.
3. The cookie is ASP.NET Core cookie authentication: `HttpOnly`, `Secure`, `SameSite=Lax`, sliding, with the default 14 days. `UseAuthentication` and `UseAuthorization` are added to the web pipeline before `UseAntiforgery`.

The ticket is what stops another site from signing a visitor in (login CSRF): only the app's own circuit can mint one, and a guessed or replayed one does nothing. It also fits both card clicks and "created, now sign in", which happen inside the circuit after a mutation.

Alternatives:

- **A plain HTML form POST with an antiforgery token.** It works for a card, but not for signing in after the create dialog's mutation succeeds, and it can't be shared with the desktop's in-place sign-in.
- **`GET /account/sign-in?userId=…`.** Login CSRF: any page could sign a visitor in as someone else.

Verified at the unit level (tickets: single use, expiry, action) and the functional level (sign-in, reload, forged ticket, sign-out).

### D7. Desktop: an app-wide in-memory sign-in, and a key made at start

- `DesktopSignIn` is one singleton that is both the `AuthenticationStateProvider` and the `ISignInService`. Signing in sets the principal and raises `AuthenticationStateChanged`, then navigates to `/workspace`. Signing out clears it and navigates to `/`. Nothing is written to disk, so every start is signed out.
- **As built, two types, not one.** `DesktopSignIn` is the singleton `AuthenticationStateProvider`, and a per-window `DesktopSignInService` is the `ISignInService`, because a singleton cannot hold a window's scoped `NavigationManager`. A sign-in changes the state only (the landing page sends a signed-in visitor on, in `OnParametersSetAsync`); a sign-out also navigates to `/`, since it can be made from settings.
- It is **app-wide, not per window**: a second launch opens a second window on the running instance (see [desktop.md](../../../docs/desktop.md)), and both show the same user. One person runs the desktop app.
- **The key:** `DesktopHost` makes 32 random bytes at start and hands them to `DesktopApi.StartAsync` (the API's JwtBearer) and to the clients' token issuer. Nothing outside the process ever knows the key. The socket's directory is already owner-only. The token is still required, so the API has one code path and the desktop could later sign in through something else (`TODOS.md`).
- **Plain HTTP over the socket:** gRPC only attaches call credentials over TLS, so the desktop's clients set `UnsafeUseInsecureChannelCallCredentials = true`. That is safe here, because the socket lives in the owner-only runtime directory, and the web host keeps TLS.

Alternative: the desktop API trusting the socket with no token. Rejected: it is a bypass, so the API would have two paths to test, and a later desktop authenticator would have nothing to plug into.

Verified at the unit level (`DesktopApiTests`: a socket call without a token is refused, with one is accepted; the provider's state changes) and by hand (sign-in and sign-out in the window, with the log lines below).

### D8. The signing key comes from configuration; the AppHost generates one per run

The API server and the web host read `CallerTokens:SigningKey`, a string of at least 32 characters whose UTF-8 bytes are the HMAC key. Both refuse to start without it, with an exception that names the setting. The AppHost declares one secret parameter with a generated default (`AddParameter` with `GenerateParameterDefault`, to be confirmed against Aspire 13.4 at implementation) and passes it to `api-svc` and `web-svc` as `CallerTokens__SigningKey`. The functional tests start the AppHost, so they get a key the same way. The pipeline tests and the desktop make their own (D7, D11).

Alternative: asymmetric keys, where the web signs with a private key and the API holds only the public key. That would be better once there is more than one issuer or the API is exposed. It is not worth managing a key pair while one AppHost starts both processes.

### D9. The token is attached by the gRPC client factory, from the caller's scope

Each host registers its clients with `AddCallCredentials((context, metadata, services) => …)` on the client's HTTP builder (Wolverine's `AddWolverineGrpcClient` exposes `HttpClientBuilder`, as `DesktopApi.AddSocketClients` already uses). The delegate resolves `AuthenticationStateProvider` from `services`, the scope the client was resolved in: the circuit on the web, the window's scope on the desktop. If a user is signed in, it adds a freshly issued token. Otherwise it adds nothing, and a call that needs a caller is refused by the API. A token is issued per call, because HMAC signing is cheap and it avoids caching and refresh logic.

One registration helper in the UI library (`CallerTokenCredentials.AddTo(IHttpClientBuilder, byte[] key)`) is used by both hosts.

Risk: that `services` is the consumer's scope, not the root, is an assumption about the gRPC client factory. Task 1 checks it before anything is built on it. If it does not hold, the fallback is a scoped `SignedInCaller` that the facades set, read through a scoped handler.

Verified at the UI-logic level (a fake service sees the header for the signed-in user and none when signed out) and by every functional test.

### D10. The landing page: one card per user, a card is a button

`Home.razor` replaces the `RadzenDataGrid` with a wrapping `RadzenRow` of `RadzenColumn`s (full width on narrow screens, three to four per row on wide ones), each holding a `RadzenCard`. A user card is a `<button>` whose accessible name is the display name, showing initials, display name and email. Choosing it calls `ISignInService.SignInAsync`. The last card is the create-user action. The empty state is the existing card. Cards are ordered by display name with `StringComparer.CurrentCultureIgnoreCase` in the page, because the API's list order is insertion order and a grid used to sort it. `CreateUserDialog` closes with the created user, and the page signs in as that user. The dialog's errors and hints are unchanged.

No new CSS beyond what Radzen cannot express: the card-as-button's reset, if needed, goes into `app.css` ([ui.md](../../../docs/ui.md#styling)).

Verified at the functional level (`LandingPageTests`).

### D11. A sign-in the API stops accepting ends in the UI

The workspace already turns refusals into messages through `WorkspaceErrors`. `UNAUTHENTICATED` becomes a new case, `WorkspaceErrors.SignedOut`. When a query's failure or a mutation's refusal carries it, the workspace calls `ISignInService.SignOutAsync()` instead of showing a message, and the landing page follows. `FindUserById` answering "no user" for the signed-in id is treated the same way. This replaces the workspace's not-found card.

Verified at the UI-logic level (`WorkspaceErrors` maps `UNAUTHENTICATED` to signed-out; the workspace's reaction is covered by a test of the helper it calls with a fake `ISignInService`) and the pipeline level (a deleted user's token is refused). Not at the functional level: deleting a user there would need the AppHost's generated key inside the test process, and the UI has no delete-user action.

### D12. Addresses and facades lose the user id, query keys keep it

- **Addresses.** `Workspace.razor` routes become `/workspace` and `/workspace/archives/{ArchiveId:guid}`, and take the user id from the authentication state. `/users/{id}` is not kept as a redirect: the routes are recent and nothing outside the app links to them. The old address gets the ordinary not-found page.
- **Facades.** `ArchivesFacade`, `DocumentsFacade` and `UploadAsync` stop putting an owner in requests.
- **Query keys.** `WorkspaceKeys` keep the owner id, so a desktop sign-out and sign-in as another user never serves the first user's cached lists. On the web, a sign-in is a reload and a new circuit anyway.

### D13. Tests sign in the way the hosts do

- **Pipeline.** `PipelineHost` gets a fixed test key and `TokenFor(Guid userId, …)`, and exposes `CallOptionsFor(userId)` (and a channel variant) so tests call as a user. Overloads build a wrong-key or expired token for the refusal scenarios. The channel sets `UnsafeUseInsecureChannelCallCredentials`, as the desktop does. Every existing archive and document pipeline test is moved from `owner_id` to a caller. `TestDocuments.UploadAsync` takes the caller.
- **Functional.** Every test signs in through the landing page. `WorkspacePage.OpenNewUsersWorkspaceAsync` creates a user through the dialog, which now lands in that user's workspace. Each test's browser context has its own cookie, so tests stay parallel. `LandingPageTests` is rewritten for cards (it is currently `[Skip]`ped for an unrelated reason in `TODOS.md`; the rewrite also moves it to TUnit assertions and removes the skip).
- **By hand.** Signing in and out in the desktop window (see tasks).

## Telemetry and logs

- **Traces and metrics:** no new source or meter. JwtBearer and cookie authentication run inside the existing ASP.NET Core request spans. ASP.NET Core's instrumentation does not record request headers by default, so the `Authorization` header and the cookie stay out of spans. `ApiTelemetryTests` gains "no span carries the token", in line with "no span carries the email". The caller's user id is not added to spans in this change.
- **Logs:** JwtBearer logs a rejected token at Information. IdentityModel hides token contents in its messages unless `IdentityModelEventSource.ShowPII`/`LogCompleteSecurityArtifact` is turned on, and **neither may be turned on**. That rule goes into observability.md. Expected refusals (`UNAUTHENTICATED`, `PERMISSION_DENIED`) are not logged by our mapping, like every other refusal.
- **New log messages:** only on the desktop, as evidence for the by-hand check, both `[LoggerMessage]` at Information with the user id only: `Signed in as user {UserId}` and `Signed out`. The web host logs nothing new: a redeemed or refused ticket is an answer, not a fault.

## Guideline docs to update

- `docs/architecture.md`: the request path gains the token (host issues, API validates). The two-hosts table gains rows for sign-in and the key. `UI` gets `ISignInService` next to `IUserSettingsService`. `openspec/config.yaml`'s `context` repeats the request path, so it is updated with it.
- `docs/api.md`: the caller (`CallerAccessor`, D2), commands carrying the acting user (D3), the ownership check in the existence check, the two new refusals in the failures table, and "Adding an RPC" gains "resolve the caller first".
- `docs/ui.md`: the sign-in seam (D5), the web sign-in endpoints and tickets (D6), the routes, and the `SignedOut` case in "Failures become localized messages".
- `docs/desktop.md`: the desktop's sign-in and per-process key, and the socket's call credentials (D7).
- `docs/testing.md`: calling as a user in pipeline tests, signing in in functional tests (D13).
- `docs/observability.md`: tokens and cookies never in spans or logs, and IdentityModel's PII switches stay off.
- `TODOS.md`: remove "Authentication and authorization". Add web passkeys, desktop passkeys (the three routes and "what it protects"), remembering the desktop's last user, and asymmetric caller keys. Update the folder watcher ("which user owns a dropped file" is now a caller given explicitly on the bus) and "a search box on every page" (there is now a signed-in user outside the workspace). Remove "`LandingPageTests` is skipped", since the rewrite in D13 resolves it.

## Risks / Trade-offs

- [Anyone who reaches the web host can sign in as anyone] → This is the chosen placeholder, and the web host is the development harness. The landing page says nothing about security, and `TODOS.md` names passkeys as the next step. The API's isolation between users is real, so the remaining gap is only the credential.
- [The signing key lets its holder act as any user] → It lives only in the AppHost's generated parameter and the two processes' environment (desktop: in memory). Tokens live 5 minutes. Asymmetric keys go to `TODOS.md`.
- [Tickets are kept in one web process's memory] → There is one web instance. A restart only invalidates tickets that are seconds old; the visitor clicks again.
- [The `AddCallCredentials` scope assumption (D9) is wrong] → Checked first, in task 1, with a fallback named in D9.
- [A circuit keeps the authentication state it started with] → Every sign-in and sign-out is a full reload on the web, so a circuit never outlives its user.
- [Per-call user lookup] → One snapshot read, the same cost as the existing owner checks. Revisit if traces show it.
- [Old `/users/{id}` bookmarks break] → Accepted (D12). Nobody but the developer has used them.
- [The contract change breaks old clients] → Web and API ship together through one AppHost, and the desktop is one process. There is no other client.

## Migration Plan

- **Data:** none. Users, archives and documents are unchanged. Every existing document already has an owner, which is now also the only user who can see it.
- **Deploy:** web, API and AppHost change together. A configured environment outside Aspire must set `CallerTokens__SigningKey` for both processes, or neither starts.
- **Rollback:** revert the change. The data stays compatible both ways, since nothing new is stored.
