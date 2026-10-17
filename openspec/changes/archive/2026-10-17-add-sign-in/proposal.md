# Proposal

## Why

There is no signed-in user. The user id in the page address (`/users/{id}`) stands in for one, every archive and document RPC trusts the `owner_id` the client sends, and the RPCs that take only a document or archive id check no owner at all. Anyone who can reach the API can read or change anyone's documents. A real "who is calling" is also what the folder watcher and a search box outside the workspace are waiting for (`TODOS.md`).

This change makes the signed-in user real end to end, the API included, with the simplest credential there is: choosing yourself from the list. The credential is a placeholder on purpose. Passkeys are the intended successor and are deferred, so this change builds the seams they will plug into, not the passkey ceremony.

## What Changes

- **A real landing page.** `/` shows one card per user. Choosing a card signs in as that user. Creating a user signs in as the new user. A signed-in visitor who opens `/` goes to their workspace.
- **Signing out.** The sidebar shows who is signed in and offers sign-out, which returns to the landing page.
- **The workspace belongs to the signed-in user.** **BREAKING** (addresses): `/users/{id}` and `/users/{id}/archives/{archiveId}` become `/workspace` and `/workspace/archives/{archiveId}`. A signed-out visitor who opens a workspace address is sent to the landing page.
- **The API identifies the caller.** Every RPC on archives and documents requires a signed caller token. The API refuses a missing, invalid or expired token, or one whose user is unknown or deleted, as `UNAUTHENTICATED`.
- **The owner is the caller.** **BREAKING** (contract): `owner_id` leaves `ListArchivesRequest`, `CreateArchiveRequest`, `ListInboxRequest`, `SearchDocumentsRequest` and `UploadDocumentHeader`. The API acts for the caller.
- **Someone else's data is not found.** Renaming, deleting, filing, unfiling or downloading a document, renaming or deleting an archive, and listing an archive's documents check that the caller owns the resource. A resource that belongs to another user is answered exactly as an unknown one. Filing into another user's archive is now "not found" instead of `archive_owner_mismatch`.
- **The user service stays open where the landing page needs it.** Listing, finding and creating users need no caller. Updating or deleting a user is allowed only for that user (`PERMISSION_DENIED` otherwise).
- **Identity is pluggable per host.** The UI reads the signed-in user through Blazor's `AuthenticationStateProvider` and signs in and out through a host-implemented `ISignInService`. This follows the `IUserSettingsService` pattern.
  - **Web:** a cookie on the web host, and a short-lived signed token on every call to the API, validated there as a JWT bearer token.
  - **Desktop:** the signed-in user is kept for the running app. The API in the same process validates the same kind of token, signed with a key made at start.
- **Deferred to `TODOS.md`:** passkeys for the web (ASP.NET Core Identity's passkey support with a Fisher-backed store, and where the ceremony runs), passkeys for the desktop (no WebAuthn in WebKitGTK; the routes and what they would protect), and remembering the desktop's last user. The "Authentication and authorization" item is removed. The folder-watcher and search-box items are updated now that a signed-in user exists.

## Capabilities

### New Capabilities

- `sign-in`: signing in by choosing a user, being signed in after creating a user, signing out, who is shown as signed in, and where a signed-in or signed-out visitor is sent, on the web and on the desktop.
- `api-authorization`: how the API identifies the caller on each RPC, which RPCs need one, what the caller may act on, and how a refusal is answered.

### Modified Capabilities

- `user-landing-page`: users are shown as cards and choosing one signs in instead of opening `/users/{id}`; creating a user signs in; a signed-in visitor is sent to the workspace.
- `user-workspace`: the workspace is the signed-in user's, at `/workspace`; sign-in is required; the unknown-user not-found state goes away.
- `archive-management`: archives are created and listed for the caller; the unknown-owner and deleted-owner scenarios become the caller's; listing an archive's documents covers only the caller's archives.
- `document-management`: documents are uploaded for the caller; filing into another user's archive is not found; downloading another user's document is not found.
- `document-search`: the search covers the caller's documents; the unknown-owner case goes away.
- `api-error-mapping`: `UNAUTHENTICATED` and `PERMISSION_DENIED` join the mapping; `archive_owner_mismatch` and the "on behalf of a deleted user" use of `user_deleted` are no longer produced through the API.
- `desktop-app`: the desktop offers the same landing page and sign-in as the web app.

## Impact

- **Contracts:** `archives.proto`, `documents.proto` lose `owner_id` from five request messages. The DTOs keep `owner_id`. Shared constants for the caller token (issuer, audience, claim) are added.
- **Api:** authentication and authorization in `AddOfficeRkyvApi`/`MapOfficeRkyvApi`; the caller is read in the services and carried on commands and queries; the existence checks of archives and documents also check the owner; new exceptions for an unauthenticated caller and a forbidden action, mapped in `Shared/Errors`. The development seed and the archive-deletion cascade keep acting through the bus with an explicit user.
- **UI:** the landing page, the workspace's routes and its owner handling, the sidebar, the facades (no owner in requests), `AuthorizeRouteView` in both hosts' routers, and new localized texts in English and German.
- **Web:** cookie authentication, sign-in and sign-out endpoints, a token on every gRPC call, and a signing key from configuration.
- **Desktop:** an in-memory sign-in, a per-process signing key shared by the in-process API and its clients.
- **AppHost:** one generated signing key passed to `api-svc` and `web-svc`.
- **Dependencies:** `Microsoft.AspNetCore.Authentication.JwtBearer` (Api) and a JWT token handler for issuing (UI). Both are Microsoft packages.
- **Tests:** the pipeline tests call with a caller token through `PipelineHost`; the functional tests sign in through the landing page; new tests at the unit, pipeline, UI-logic and functional levels; the desktop sign-in is checked by hand.
- **Guideline docs whose conventions this change sets or changes:** `docs/architecture.md` (the request path and both hosts gain the caller), `docs/api.md` (the caller, ownership checks, the new refusals), `docs/ui.md` (the sign-in seam and the routes), `docs/desktop.md` (the desktop's sign-in and key), `docs/testing.md` (signing in, in pipeline and functional tests), `docs/observability.md` (tokens and the signed-in user in telemetry and logs).
