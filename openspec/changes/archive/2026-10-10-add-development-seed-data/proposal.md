# Proposal

## Why

The development database is a temporary file that is new on every start of the AppHost, so every session begins with an empty app: a user has to be created by hand before the workspace, the archives or the documents can be looked at. That is repeated many times a day and makes it slow to check any change behind the landing page.

## What Changes

- When the API server starts in the Development environment, it seeds demo data: one user (`demo@example.com`, "Demo User"), one empty archive ("Taxes 2026") owned by that user, and three documents in that user's inbox ("Electricity bill March", "Rental contract", "Insurance policy").
- The seed is all-or-nothing and runs once per database: if the demo user already exists, nothing is added. A restart of the API alone, which keeps the database, therefore does not duplicate archives or documents, and does not bring back demo data the developer deleted.
- The seed goes through the same commands the UI uses, so seeded data is indistinguishable from data created by hand.
- A configuration setting turns the seed on or off. It defaults to on in Development and is off everywhere else, whatever the setting says.
- The functional tests switch the setting off, so they keep starting from an empty database.
- The desktop app never seeds. It keeps the user's real data in a persistent directory, also when started in Development.
- A failed seed is logged and does not stop the API from starting.

No breaking changes. Nothing changes outside Development.

Guideline docs whose conventions this change sets or changes: `docs/architecture.md` (what a host may add around the shared API composition: the API server now also adds the development seed) and `docs/testing.md` (the functional tests run with the seed switched off). `docs/api.md` and `docs/observability.md` are followed, not changed.

## Capabilities

### New Capabilities

- `development-seed-data`: which demo data the API server creates in Development, when it does and does not, and how it is switched off.

### Modified Capabilities

None. `user-management`, `archive-management` and `document-management` are used as they are; the seed adds no rule to them.

## Impact

- `src/OfficeRkyv.Api`: a new seeder and its registration in `Program.cs` (host-specific, not in `AddOfficeRkyvApi`); the setting in `appsettings.Development.json`.
- `src/OfficeRkyv.Desktop`, `src/OfficeRkyv.UI`, `src/OfficeRkyv.Web`, `src/OfficeRkyv.Contracts`: no change.
- `tests/OfficeRkyv.UnitTests`: new pipeline tests for the seeder and a unit test for the on/off decision.
- `tests/OfficeRkyv.FunctionalTests`: the fixture switches the seed off for the API it starts.
- Logs: one Information line when demo data was seeded, one Warning when seeding failed.
- No new dependency, no contract change, no change to stored data outside Development.
