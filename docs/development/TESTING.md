# Testing Strategy

## Objectives

Tests must protect the semantic contract as strongly as the implementation. A regression that changes an event ID, name, level, or trigger condition can break monitoring even when application behavior appears correct.

## Test layers

### Event catalog tests

Verify uniqueness and stability of event IDs and names, valid range/category assignments, required metadata, message-template/property consistency, and deprecation rules.

### Unit tests

Test event emitters, options validation, filtering decisions, state transitions, and failure-isolation behavior independently of full hosts where possible.

### Host integration tests

Start real Generic Host test applications and verify lifecycle event ordering and cardinality. Ensure registration does not alter host behavior.

### ASP.NET Core integration tests

Use supported test hosting infrastructure to exercise requests, exception paths, status outcomes, shutdown, and optional instrumentation. Verify that routine framework behavior does not produce misleading Common Events.

### Blazor integration tests

Where practical, exercise supported circuit lifecycle hooks and ensure inherited ASP.NET Core/Hosting events are not duplicated.

### Provider-neutral tests

Capture `ILogger` records using a test provider rather than asserting against console-formatted strings. Assert `EventId`, name, level, message template/structured state, exception, and relevant scope/activity context.

## Contract snapshots

Consider generating a machine-readable catalog artifact from the event definitions and comparing it in tests. Any intentional contract change should be obvious in code review.

## Compatibility matrix

For each supported .NET version, CI should build and run the appropriate integration suite. Framework-specific instrumentation must be verified against the versions claimed by the NuGet package.

## Performance tests

Benchmark hot paths introduced by middleware or frequently invoked instrumentation. Measure allocation and throughput impact with instrumentation enabled and with relevant log levels filtered out.

## Negative tests

Tests should explicitly prove that the library does **not** emit events for ambiguous conditions. This is central to the semantic-certainty principle.

## Failure tests

Verify that logging-provider failures or optional enrichment failures do not unexpectedly crash functional application paths, subject to the guarantees of `Microsoft.Extensions.Logging` itself.

## Configuration validation event contracts

ConfigurationValidationEventsTests verifies all five configuration event
identities, levels, templates, and exact structured property sets.

Coverage includes filtering, literal argument preservation, event-group
initialization, interleaved attempt correlation, and explicit emission
without deduplication or inferred aggregate/application-mode events.

These are emitter contract tests. They do not establish that an application
validates configuration correctly or emits exactly one outcome per attempt;
those responsibilities require tests of the application's validator.
