# Security and Privacy

## Principle

Operational telemetry frequently leaves the process and may be retained, indexed, replicated, or accessible to broader support audiences. Common Application Events must therefore be safe by default.

## Data minimization

Standard events should contain only the data necessary to explain the operational condition. Do not include secrets, credentials, access tokens, authorization headers, connection strings, private keys, request/response bodies, or arbitrary object serialization.

## Personal and sensitive information

Standard event definitions should avoid requiring personally identifiable or sensitive information. If an event needs an entity reference, prefer a non-sensitive technical identifier when possible and document the privacy implications.

## Exception data

Exceptions may contain sensitive values in messages, stack traces, `Data`, or nested exceptions. The project should rely on standard logging practices and document the risk rather than indiscriminately serialize exception objects or arbitrary properties.

## User-provided values

Structured properties derived from external input must not be assumed safe. Avoid logging raw headers, URLs with query secrets, form data, payloads, or unbounded strings by default.

## Telemetry integrity

Event identity is an operational contract. Application code should not be able to accidentally emit a standard event ID with incompatible semantics through convenience APIs. Public APIs should guide callers toward valid event definitions.

## Failure isolation

Instrumentation must not become a new availability dependency. Failures in optional enrichment or observers should not normally break application startup or request processing. Exceptions caused by invalid developer configuration may fail fast when that is safer and clearly documented.

## Logging injection

Structured logging should be used rather than concatenating untrusted values into message templates. Providers may render line breaks or control characters differently; standard event APIs should not encourage free-form message construction.

## Threat review

Before v1.0, perform a focused threat review covering sensitive-data leakage, denial-of-service through high-cardinality/high-volume telemetry, recursion, malformed enrichment, dependency injection misuse, and telemetry spoofing/confusion.
