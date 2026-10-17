# Event Model

## Purpose

A Common Application Event is a stable operational contract, not merely a log message. Each standard event should be independently identifiable and documented.

## Proposed event definition

Each event should define:

| Field | Description |
| --- | --- |
| Event ID | Stable numeric identifier |
| Event name | Stable symbolic/machine-readable name |
| Category | Semantic event family |
| Default level | Standard `LogLevel` |
| Message template | Human-readable structured logging template |
| Required properties | Values necessary for the event to be meaningful |
| Optional properties | Additional standardized context |
| Trigger semantics | Exact condition under which the event is valid |
| Related events | Preceding, succeeding, recovery, or escalation events |
| Operational guidance | What support/monitoring should infer or consider |
| Since | Version in which the event became public |
| Status | Proposed, stable, deprecated, or retired |

## Event ranges

The initial taxonomy retains the [original project's](https://github.com/patware/CommonLoggingEvents) intuitive hundreds-based classification.

| Range | Category | Intent |
| ---: | --- | --- |
| 100–199 | Low-level / lifecycle / diagnostic | Routine technical state and lifecycle transitions |
| 200–299 | Positive / success | Successful completion or healthy state |
| 300–399 | Warning | Abnormal condition worth operational attention |
| 400–499 | Recoverable application condition | Failure or problem the application expects to survive |
| 500–599 | Unrecoverable application condition | Application cannot correctly continue the affected capability/process |
| 600–699 | External/dependency failure | Failure outside the application's direct control; degraded operation may remain possible |
| 700–799 | Unhandled failure | Unexpected/unhandled application exception or equivalent terminal defect |

The range communicates broad semantics; the individual event definition remains authoritative.

## Log level is not derived mechanically from the range

Ranges and levels are related but not identical concepts. An event's default level must be explicitly defined. For example, lifecycle events may be `Information`, diagnostics may be `Debug`, warnings `Warning`, and severe failures `Error` or `Critical`.

Do not implement a simplistic rule such as "all 500 events are Critical" unless the catalog explicitly establishes that invariant.

## Event identity

The numeric ID and symbolic name should both be stable. Example only:

```text
EventId: 201
EventName: ApplicationStarted
Level: Information
```

Exact IDs remain subject to catalog design before v1.0.

## Structured properties

Prefer stable property names over embedding values into prose. Example:

```text
EventId: 603
EventName: DependencyUnavailable
DependencyType: SqlDatabase
DependencyName: OrdersDatabase
RetryCount: 5
```

The message remains readable, for example:

```text
Dependency {DependencyName} of type {DependencyType} is unavailable after {RetryCount} retries.
```

## Property rules

Property names should be stable, PascalCase unless a future OpenTelemetry semantic convention warrants another representation, and reusable across events when they mean the same thing. Avoid duplicating high-cardinality payloads, secrets, request bodies, SQL text, tokens, or personal information.

## Event relationships

Documentation should identify meaningful sequences. For example, an `ApplicationStarting` event may be related to `ApplicationStarted`, startup failure, stopping, and stopped events. Monitoring can then detect incomplete transitions without parsing text.

## Application-defined events

The standard catalog cannot represent every business/domain event. Applications remain free to use `ILogger` for their own events. A future reserved numeric range for application-specific events may be considered, but should not be invented without a governance decision.
