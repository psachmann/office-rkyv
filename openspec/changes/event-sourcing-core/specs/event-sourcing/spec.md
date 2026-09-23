# Spec Delta

## Purpose

Provides an append-only, versioned event history per entity, together with deterministic state projection and command validation, so every entity's current state is always derivable from — and consistent with — its full recorded history.

## ADDED Requirements

### Requirement: Events are appended with optimistic concurrency
The system SHALL append an event for an entity only when the caller's expected version matches the entity's current stored version, and SHALL assign the event the next sequential version for that entity.

#### Scenario: Successful append matches expected version
- **WHEN** a caller appends an event for an entity, supplying the version they last observed
- **THEN** the event is stored and assigned the next version for that entity

#### Scenario: Append rejected on version conflict
- **WHEN** a caller appends an event for an entity, but the entity's current stored version no longer matches the version the caller supplied
- **THEN** the append is rejected and no event is stored

### Requirement: Full event history can be replayed in order
The system SHALL return the complete, ordered event history for an entity on request, without filtering by event type.

#### Scenario: Replay returns all events for an entity in version order
- **WHEN** a caller requests the event history for an entity that has events of more than one type
- **THEN** every event for that entity is returned, ordered by version, regardless of type

### Requirement: Materialized state reflects every append synchronously
The system SHALL update an entity's materialized (queryable) state in the same operation as appending its event, so no read can observe an event without also observing its effect on materialized state.

#### Scenario: Read immediately after append reflects the new state
- **WHEN** an event is successfully appended for an entity
- **THEN** a read of that entity's materialized state, performed immediately after, reflects the event's effect

### Requirement: Entity state derives deterministically from its event history
The system SHALL compute an entity's materialized state as a pure, deterministic function of its ordered event history: replaying the same history SHALL always produce the same state as the materialized row reflects.

#### Scenario: Replaying the same event history always yields the same state
- **WHEN** an entity's event history is replayed more than once
- **THEN** each replay produces the identical resulting state, matching the entity's materialized state

### Requirement: Commands are validated against current state before being accepted
The system SHALL validate a submitted command against the entity's current materialized state before any resulting event is appended, relying on that materialized state being kept synchronously consistent with the entity's event history.

#### Scenario: Valid command is accepted and produces events
- **WHEN** a command is submitted for an entity and satisfies that entity's invariants given its current state
- **THEN** the command is accepted and the resulting events are appended

#### Scenario: Invalid command is rejected and no event is appended
- **WHEN** a command is submitted for an entity but violates an invariant given its current state
- **THEN** the command is rejected and no event is appended for it

### Requirement: Deleted entities are retained, not removed
The system SHALL represent entity deletion as a state transition recorded in the event history, not as removal of the entity's stored data.

#### Scenario: Deleting an entity marks it Deleted rather than removing its row
- **WHEN** a delete is accepted for an entity
- **THEN** the entity's materialized state transitions to Deleted and its row remains present

#### Scenario: A deleted entity's full history remains replayable
- **WHEN** the event history of a deleted entity is requested
- **THEN** every event recorded before and including the deletion is returned
