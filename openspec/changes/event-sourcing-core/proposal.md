# Proposal

## Why

The API has no real persistence layer yet — only a placeholder Greeter service and an unstructured WIP sketch (`EventStore`, `EventRecord`, `ISpecification`). Before any real entity (starting with documents) can be built, the project needs a foundation for recording state changes as an immutable history, validating those changes against business rules before they're accepted, and always exposing an up-to-date queryable view of current state.

## What Changes

- Add a versioned, append-only `EventStore` that appends events for an entity under optimistic concurrency (expected version) and replays the *full*, ordered, heterogeneous event history for an entity. **BREAKING**: replaces the existing WIP `EventStore.GetByEntityIdAsync<TPayload>`, which filters by a single event type and cannot support full-history replay.
- Add a `Projector` per entity: a pure `Apply(entity, event) -> entity` fold. It is the single definition of how events turn into state, used both to materialize the read-side row and to rehydrate state for command validation.
- Add an `Aggregate` per entity: a `Decide(state, command) -> events` operation that rehydrates current state via the entity's `Projector` and validates invariants before emitting new events. Every entity gets its own aggregate, even ones with no invariants today, so the seam is consistent and already in place when invariants show up later.
- Make materialization synchronous and unconditional: every `EventStore` append updates the entity's materialized row in the same transaction, so a read immediately following a write is never stale.
- Add `EntityState` (`Unknown`, `Active`, `Deleted`) to `EntityBase`/`IEntity`. Deletion appends an event and flips state on the materialized row (soft delete) instead of removing it. Queries return entities in any state by default; callers filter explicitly when they want only active entities.

## Capabilities

### New Capabilities

- `event-sourcing`: append-only, versioned event storage per entity; full-history replay; synchronous read-model materialization via per-entity projectors; per-entity aggregates that validate commands and emit events; soft-delete entity state.

### Modified Capabilities

_None — the existing `ui-localization` capability is unaffected._

## Impact

- `src/OfficeRkyv.Api/Data/*` (`EventStore`, `EventRecord`, `OfficeRkyvContext`, migrations): `EventRecord` gains a `Version` column; `EventStore`'s read API changes shape (breaking relative to the current WIP).
- `src/OfficeRkyv.Api/Shared/*` (`IEntity`, `EntityBase`, `IEvent`, `Event`): `EntityBase`/`IEntity` gain `EntityState`; new `Projector`/`Apply` and `Aggregate`/`Decide` abstractions are introduced.
- No consumers exist yet beyond the Greeter scaffold, so no gRPC service or Blazor UI code is affected — this change lays the foundation only.
- Out of scope, deferred to separate proposals: the file-based document store, the `EntityReader`/specification query layer, and any API-transport change.
