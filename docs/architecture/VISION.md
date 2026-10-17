# Vision

## Problem

.NET provides excellent logging abstractions, but those abstractions intentionally do not define a common operational vocabulary. Application teams independently decide when to log, which `EventId` to use, which level is appropriate, what message to write, and which structured properties to include. The result is inconsistent telemetry that is harder for developers, support teams, monitoring systems, and automated analysis to understand.

An event such as "application started" should not require every developer to invent its identifier, severity, wording, and structure.

## Vision

Provide a **standard, structured, documented application-event vocabulary for .NET**, combined with application-type-aware instrumentation that emits events automatically when the .NET framework can determine their meaning reliably.

The project should make good operational telemetry the path of least resistance.

> Instrument the application, not every developer call site.

## HTTP analogy

HTTP status codes are useful because `404`, `401`, and `503` carry shared semantics independent of the application that produced them. This project applies the same principle to application telemetry: stable event identifiers, names, categories, levels, message templates, and structured properties form an operational contract.

The analogy is conceptual, not a requirement to duplicate HTTP semantics or numbering.

## Primary audiences

**Application developers** should obtain useful telemetry with minimal code and without repeatedly deciding routine logging semantics.

**Operations and support teams** should see predictable events across applications, enabling reusable dashboards, alerts, queries, runbooks, and troubleshooting practices.

**Platform and observability teams** should be able to reason about telemetry structurally rather than parse arbitrary message text.

## Goals

1. Standardize common operational application events.
2. Minimize application code required to adopt the standard.
3. Automatically observe framework lifecycle events where semantics are reliable.
4. Preserve explicit APIs for application-specific semantic events.
5. Use `Microsoft.Extensions.Logging` rather than replace it.
6. Produce structured telemetry suitable for humans and machines.
7. Integrate naturally with OpenTelemetry and existing logging providers.
8. Support specialized .NET application models without duplicating common behavior.
9. Make event definitions stable, documented, testable, and governable.
10. Be safe by default with respect to secrets, personal information, performance, and failure isolation.

## Non-goals

The project is not a logging provider, log store, SIEM, APM product, tracing SDK, metrics platform, or replacement for OpenTelemetry. It does not attempt to capture every application action, business event, request, database call, or exception automatically.

It must not infer semantic meaning that the framework cannot know reliably. For example, a failed method invocation does not automatically imply that the application is degraded, that a retry is exhausted, or that a business operation failed.

## Core principles

### Semantic certainty over coverage

Automatic instrumentation is emitted only when the framework provides sufficient evidence to identify the event correctly. Missing an event is preferable to emitting a misleading one.

### Convention over instrumentation

Framework-known events should require registration, not repeated calls such as `logger.ApplicationStarted()`.

### Explicit when only the application knows

Events such as "fallback activated", "dependency retry exhausted", or "business operation rejected" often require application context. Those remain explicit APIs.

### Structured first, readable always

Event IDs, names, categories, and structured properties are the contract. Human-readable messages remain important, but monitoring should not depend on parsing prose.

### Transport independence

The project emits through standard .NET abstractions. Applications retain control of providers, destinations, retention, sampling, and observability backends.

### Safe defaults

No secrets or sensitive payloads by default. Instrumentation must not cause application failure and should have bounded overhead.
