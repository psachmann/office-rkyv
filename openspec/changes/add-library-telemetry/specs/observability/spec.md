# Spec Delta

## Purpose

Defines the traces and metrics the Web and Api services export, so that one user action can be followed in a single trace from the UI's data layer through the API's message handling down to the event store, and so that event-store write contention is measurable.

## ADDED Requirements

### Requirement: API message handling is traced
The Api SHALL export a span for every message it handles in-process, whether the message is a command or a query. That span SHALL be a child of the span that was active when the message was dispatched, so the handler span sits under the incoming gRPC request span that caused it.

#### Scenario: A gRPC call produces a handler span under the request span
- **WHEN** a client calls the Api's `ListUsers` RPC
- **THEN** the exported trace contains the server span for the RPC and, as its descendant, a span for handling the list-users query

#### Scenario: Two dispatches in one RPC produce two handler spans
- **WHEN** a client calls the Api's `UpdateUser` RPC, which dispatches an update command and then a find-by-id query
- **THEN** the exported trace contains a handler span for each message, both under the RPC's server span

### Requirement: Event-store work is traced
The Api SHALL export spans for event-store operations (loading, querying, appending events, and committing a unit of work). Each span SHALL be a child of the handler span in which the operation ran. Event-store spans SHALL NOT contain bound parameter values or document contents.

#### Scenario: A command's store work appears under its handler
- **WHEN** a create-user command is handled
- **THEN** the exported trace contains at least one event-store span as a descendant of that command's handler span

#### Scenario: No user data leaks into store spans
- **WHEN** a create-user command with email `ada@example.com` is handled
- **THEN** no attribute of any exported event-store span contains the value `ada@example.com`

### Requirement: API message handling and event-store metrics are exported
The Api SHALL export the metrics that its message handling layer and its event store publish. It SHALL also export the event store's write-lock contention metrics: the time a writer waited for the database write lock, and the number of write-lock retries.

#### Scenario: Write-lock wait is measured on commit
- **WHEN** a command that writes to the event store is handled
- **THEN** a write-lock wait measurement is exported for that commit

#### Scenario: Message handling metrics are exported
- **WHEN** the Api has handled at least one message
- **THEN** at least one metric from the message handling layer is exported

### Requirement: Web client-side queries are traced and measured
The Web app SHALL export a span for every data query fetch and every mutation its UI data layer runs. Any outgoing call to the Api made during that fetch or mutation SHALL be a descendant of that span. The Web app SHALL also export the data layer's metrics: fetch and mutation durations, cache hits and misses, cache entries and evictions, and retry counts.

#### Scenario: A page load's query contains the API call
- **WHEN** a user opens a page that loads data through a query
- **THEN** the exported trace contains a query-fetch span with the outgoing gRPC client call as its descendant

#### Scenario: A background refetch starts its own trace
- **WHEN** an open page's query is refetched on its interval with no user request in progress
- **THEN** the query-fetch span is the root of a trace that continues through the Api's request, handler and event-store spans

### Requirement: Traces cross the Web-to-Api boundary
A trace started in the Web app SHALL continue in the Api. The Api's server span for an incoming gRPC call SHALL have the same trace id as the Web app's outgoing client span, and SHALL be its child.

#### Scenario: One trace id end to end
- **WHEN** a user action in the Web app results in a gRPC call to the Api
- **THEN** the Web app's query-fetch span, the gRPC client span, the Api's server span, the handler span and the event-store spans all share one trace id

### Requirement: Telemetry export stays optional
Adding these signals SHALL NOT change the rule for when telemetry is exported: signals are exported only when an OTLP endpoint is configured. When none is configured, both services SHALL start and serve requests normally.

#### Scenario: No endpoint configured
- **WHEN** the Api starts without an OTLP exporter endpoint configured
- **THEN** it starts and handles gRPC calls successfully
