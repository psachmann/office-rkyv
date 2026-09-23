# Tasks

## 1. Event store schema & versioning

- [x] 1.1 Add a per-entity `Version` (int) to `EventRecord` and its EF configuration; verify `dotnet build` succeeds and a new EF Core migration reflects the schema change
- [x] 1.2 Rewrite `EventStore.AppendAsync` to accept an `expectedVersion`, assign the next sequential version per entity, and reject the append on a version mismatch; verify with unit tests covering a successful append and a version-conflict rejection
- [x] 1.3 Rewrite event replay to return an entity's full, ordered event history across all event types (remove the `TPayload`/`EventType` filter from the current WIP `GetByEntityIdAsync`); verify with a unit test asserting correct version order for a stream containing more than one event type

## 2. Entity state & shared abstractions

- [x] 2.1 Add `EntityState { Unknown, Active, Deleted }`; add a `State` property to `IEntity`/`EntityBase` defaulting to `Unknown`; verify with a unit test that a freshly constructed, un-projected entity reports `Unknown` (implemented as `EntityStatus`/`Status` - `EntityState` collides with `Microsoft.EntityFrameworkCore.EntityState`, already in scope via EF Core usings in `Data/*.cs`)
- [x] 2.2 Define the `Projector`/`Apply` abstraction (`Apply(TEntity, TEvent) -> TEntity`) plus a per-entity dispatch mechanism that folds a heterogeneous event stream into final state; verify with a unit test that folds a sequence of mixed event types into the expected final state
- [x] 2.3 Define the `Aggregate`/`Decide` abstraction (`Decide(TEntity state, TCommand command) -> IEvent[]`), taking the entity's current state as an input rather than replaying for it; verify with unit tests covering an accepted command (events produced) and a rejected command (no events produced, state unchanged)

## 3. Synchronous materialization

- [x] 3.1 Add materialized entity table support to `OfficeRkyvContext`, including a `Version` column on each materialized row, and implement the write path so `EventStore.AppendAsync` performs the event insert and the materialized-row upsert (via the entity's `Projector`) in a single transaction; verify with a test asserting a read performed immediately after a successful append reflects that event's effect (implemented as `MaterializedEntityStore<TEntity,TProjector>`, generic over any `DbContext` rather than added to the production `OfficeRkyvContext` directly - see design.md's `EventStore` decision; the production context still only maps real domain entities as they're introduced)
- [x] 3.2 Wire `Decide` to read the entity's current materialized row (not replay) and use its `Version` as `expectedVersion` when appending; verify with a unit test that a command handled through this path produces the same result as folding the full event history would
- [x] 3.3 Verify a rejected append (version conflict or a `Decide` rejection) leaves the materialized row unchanged, via a unit test

## 4. Soft delete

- [x] 4.1 Implement the delete path so a delete event transitions materialized `State` to `Deleted` without removing the row; verify with a unit test that the row remains queryable and its full event history remains replayable after deletion

## 5. End-to-end verification

- [x] 5.1 Add a minimal test-only entity/event/command fixture (in the test project, not domain code) with more than one event type, exercising the full append -> replay -> project -> decide -> materialize pipeline; verify with an integration-style test against the real `OfficeRkyvContext`/SQLite provider (against a sibling `EventSourcingTestContext` using the real `EventRecordConfiguration` and a real SQLite provider, not `OfficeRkyvContext` itself - see the 3.1 note; the production context can't map a test-only entity type without the fixture leaking into domain code)
- [x] 5.2 Run the full test suite and confirm no regressions: `dotnet test` (`OfficeRkyv.UnitTests`: 18/18 passed; `OfficeRkyv.FunctionalTests`: 2/2 passed)
