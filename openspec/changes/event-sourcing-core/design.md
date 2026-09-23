# Design

## Context

See proposal.md - Why. The only persistence code today is an unused WIP sketch (`Data/EventStore.cs`, `Data/EventRecord.cs`, `Shared/ISpecification.cs`, `Shared/IEntity.cs`) with no consumers and no migrations yet applied (`Data/Migrations/` is empty). This design replaces that sketch rather than migrating live data.

Stack constraints: EF Core over SQLite (via .NET Aspire), single API process. `EntityBase` is already a `record` with init-only properties, which the projection approach below relies on.

## Goals / Non-Goals

**Goals:**
- Deterministic, fully replayable state per entity from its ordered event history
- Optimistic-concurrency-safe appends
- A materialized read state that is never stale relative to the last successful append
- A consistent place for every entity's command validation, present even before an entity has real invariants
- Soft delete that preserves full history instead of destroying data

**Non-Goals:**
- The file-based document store (separate proposal)
- `EntityReader` and specification composition/querying (separate proposal) - this change only guarantees materialized rows exist and retain deleted entities; how callers query them is out of scope here
- Snapshotting long event streams - the synchronously-materialized row already serves as an always-current snapshot (see "`Decide` reads materialized state directly" below), so there is no replay-cost problem left to solve with snapshotting
- Outbound event notification (e.g. webhooks) - the append pipeline is designed so this can be added later as another consumer, but it is not built now
- Any change to API transport (gRPC stays as-is)

## Decisions

### Full heterogeneous replay, not per-event-type filtering
The WIP `EventStore.GetByEntityIdAsync<TPayload>` filters by both entity ID and event type, which cannot reconstruct an entity's true current state when it has more than one event type in its history. Replay must return every event for an entity, ordered by version, regardless of type, and let the caller's projection fold across all of them. This is the entire point of choosing full event-sourcing over a narrower typed-fact-log, which was the explicit intent for this change.

### Per-entity `Version`, not timestamp, for ordering and concurrency
`EventRecord` gains an integer `Version`, scoped per entity, assigned sequentially on append. Timestamps were rejected as an ordering key: clock resolution and lack of uniqueness make them unsuitable both for deterministic replay order and for detecting concurrent writes. `Version` serves both purposes - `AppendAsync` takes an `expectedVersion` and rejects the append if the entity's current stored version has moved past it, giving optimistic concurrency for free from the same column that orders replay.

### Three-way split per entity: Entity, Projector, Aggregate
Each entity has:
- **Entity** (`record`): pure state, no behavior - what `EntityReader` will eventually query.
- **Projector**: `Apply(entity, event) -> entity`, a pure fold. The single definition of "how do events become state," invoked by the synchronous materializer after every append.
- **Aggregate**: `Decide(state, command) -> events`, validates a command against current state and emits events. See below for where that state comes from.

Alternatives considered:
- *Entity implements `Apply` on itself* - rejected because it couples the queryable read type to projection mechanics, and doesn't leave a home for `Decide` without re-mixing behavior back into the data type.
- *Self-aggregate (one type does both `Apply` and `Decide`, matching Marten's simplest usage pattern)* - rejected because the materializer needs to run `Apply` on every append and shouldn't have to go through the larger `Decide`/validation surface just to fold one event.
- *Separate DI-resolved handler class per (entity, event) pair* - more SRP-per-event, but scatters projection logic away from the entity it produces and adds registration ceremony with no team-scale need for it yet.

### `Decide` reads materialized state directly, not by replay
Because materialization is synchronous and transactional with every append (see below), the materialized row is always exactly what a full replay would produce - there is no staleness window between them. `Decide` therefore rehydrates by reading the entity's current materialized row, not by replaying its event history through the `Projector`. The `Projector` remains the only place the fold logic is defined - it just has one consumer (the materializer) instead of two.

This is a correctness-preserving optimization only as long as one invariant holds: every event append goes through the single `EventStore.AppendAsync` path that also updates materialization in the same transaction. No other code path (a future bulk import, an admin tool, a manual fix) may insert an event without going through it - doing so would silently desynchronize materialized state from the event log, and `Decide` would then validate against wrong state without any way to detect it.

To support this, the materialized entity table also carries the entity's current `Version`, copied from the same transaction that wrote it, so `Decide` can supply `expectedVersion` to `AppendAsync` after reading the row - without a separate replay just to learn the current version.

### Every entity gets an `Aggregate`, uniformly
Decided explicitly in favor of consistency over minimalism: even entities with no invariants today get a `Decide` that trivially turns a command into its event. This keeps one predictable place to look for "what can happen to this entity and what's checked," and means a later invariant is a local edit instead of a refactor that relocates validation logic out of a gRPC handler or similar ad hoc location.

### Synchronous, mandatory materialization
Every `EventStore` append updates the entity's materialized row in the same transaction. Rejected alternative: eventual/async materialization (the async-daemon style some event-sourcing frameworks default to) - rejected because it would let a read immediately following a write observe stale state, which was explicitly ruled out for this project.

### `EntityState` with `Unknown` as the zero value
`EntityBase`/`IEntity` gain `EntityState { Unknown, Active, Deleted }`. `Unknown` is deliberately the default (`0`) value so that an entity materialized without ever having its state set by a projector - which should never happen in correct code - is visibly wrong (`Unknown`) instead of silently indistinguishable from a valid `Active` row.

### Deleted entities remain in the materialized store, visible by default
Deletion is an event like any other; the projector transitions `EntityState` to `Deleted` rather than removing the row. This change deliberately does not filter deleted entities out of anything by default - any exclusion is the responsibility of the caller (or, later, a reusable specification in the query-layer change). This was a conscious choice over hiding deleted entities by default: it keeps materialized retrieval behavior simple and unsurprising at this layer, at the cost of pushing the "don't leak deleted entities" responsibility onto every future caller.

## Risks / Trade-offs

- **[Risk] `Decide`'s correctness depends on materialized state always matching the event log** → if any code path ever appends an event without going through the single transactional `AppendAsync`/materialize pipeline, `Decide` silently validates against wrong state with no way to detect the drift. Mitigation: `AppendAsync` is the only supported way to append an event; no other write path should be introduced without also updating materialization in the same transaction.
- **[Risk] Deleted entities are visible by default** → a caller that forgets to filter them can leak a deleted document into a future feature (e.g. search). Mitigation: the forthcoming specification-query-layer change should provide a single reusable "exclude deleted" specification so callers opt in by composition rather than reimplementing the filter.
- **[Risk] Uniform `Aggregate` per entity adds a class even where there's no invariant yet** → minor boilerplate. Mitigation: keep the `Decide` contract minimal enough that the trivial case is a one-line pass-through, not a burden.
- **[Risk] SQLite as the append-only store** → write contention under concurrent writers is a real limitation of SQLite in general. Mitigation: not a near-term concern for a single local API process (current Aspire/SQLite deployment model); revisit if the deployment model changes.

## Migration Plan

No live data exists under the current WIP schema (no migrations have been generated yet). This change replaces the WIP `EventStore`/`EventRecord` sketch outright rather than migrating it, then adds the initial EF Core migration for the new `events` table shape (including `Version`) and any materialized entity tables introduced alongside it, each carrying its own `Version` column.
