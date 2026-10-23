# Changelog

User-visible changes are recorded here. `VERSION` is the current build version;
published releases are identified by package and release date below.

## 0.0.8 - Unpublished

### Added

- Event 501, `Configuration_LoadFailed`, through
  `logger.Configuration.LoadFailed()`.
  - Reports the configuration, logical source, loading-attempt identifier,
    and reason code, with an optional exception.
- Contract tests covering event identity, severity, structured properties,
  exception preservation, filtering, and independent loading attempts.
- Interactive configuration reloading in SampleConsole through the R key.
  - Accepted settings control the keyboard-polling interval.
  - Loading and validation failures retain the last accepted settings.
  - Initial loading failures retain a valid built-in fallback.
  - Expected loading failures produce concise Warning records, with
    original exception details available separately at Debug.
- Configuration-loading guidance distinguishing event 501 from
  setting-validation and aggregate-validation failures.

### Changed

- Replaced SampleConsole's simulated startup validation block with actual
  JSON loading, validation, and configuration application.
- Added catalog-defined workflow guide links to generated category pages
  and clarified beginning/completion relationship diagrams.

## 0.0.7 - 2026-10-23

### Added

- External dependency rejection events through `logger.External`:
  - 502, `External_AuthenticationFailed`.
  - 503, `External_AuthorizationFailed`.
- Structured dependency, operation, attempt, and reason-code properties,
  with nullable exceptions for rejection results returned without throwing.
- Authentication-rejection and authorization-denial simulations in the
  Blazor, Worker Service, and WPF samples.
- Contract tests covering event identities, Warning severity, structured
  properties, exception preservation, filtering, and event-group guards.
- Operational guidance distinguishing authentication, authorization,
  timeout, and availability failures.
- Published DocFX documentation through GitHub Pages, updated automatically
  after successful validation of `main`.
- Dedicated Events navigation with an event index, category summaries,
  individual event pages, and links to the C# API reference.
- Generator regression checks covering documentation links, cross-category
  relationships, stale pages, and protection of authored files.

### Changed

- Replaced the shared sample result's availability flag with explicit
  outcome status, preserving successful empty responses as distinct
  from dependency failures.
- Reorganized event documentation into category and event pages, with
  related transitions and Mermaid lifecycle diagrams.
- Extended documentation generation and verification to manage multiple
  pages and detect stale generated output.
- Preserved existing event-catalog anchors through a compatibility page.

### Fixed

- Separated release finalization from NuGet publication so availability
  delays can be retried without uploading packages again.
- Added verification of existing GitHub release assets and tag identity
  before finalization.
- Corrected Markdown escaping so lifecycle arrows and special characters
  display correctly in generated documentation.

## 0.0.6 - 2026-10-22

### Added

- Event 602, `External_ServiceUnavailable`, through
  `logger.External.ServiceUnavailable()`.
  - Reports the dependency, operation, attempt identifier, and reason code.
  - Accepts the original exception or null for an unavailable result returned
    without an exception.
- Immediate unavailability demonstrations in the Blazor, Worker Service, and
  WPF samples, alongside the existing timeout and cached-fallback scenarios.
- Contract tests covering nullable exceptions, structured properties,
  filtering, event-group guards, and independent attempts.
- Guidance distinguishing operation timeouts from service unavailability,
  including Worker trigger-file precedence and application recovery.

### Changed

- Extended catalog generation to support nullable exception arguments while
  preserving required-exception validation for events 601 and 701.
- Extracted shared product-service simulation and data types into the
  non-packable `SampleApplications.Shared` project. Each sample retains
  its own caching, timeout handling, and diagnostic event emission.

## 0.0.5 - 2026-10-20

### Added

- Event 601, `External_OperationTimedOut`, through
  `logger.External.OperationTimedOut()`.
  - Reports the dependency, operation, attempt identifier, and configured
    timeout budget.
  - Preserves the original exception through the logging exception channel.
- Timeout event demonstrations in the Blazor, Worker Service, and WPF samples,
  alongside cached fallback and application degradation/recovery events.
- Contract tests covering event identity, severity, structured properties,
  exception preservation, filtering, argument guards, and separate attempts.
- External operation guidance with a Mermaid diagram illustrating timeout,
  cached fallback, and recovery behavior.

### Changed

- Clarified the Exceptional event class: external failures do not necessarily
  require immediate administrator intervention.
- Linked contributor and branch guidance to the complete release procedure.

## 0.0.4 - 2026-10-19

### Added

- Configuration validation events through `logger.Configuration`:
  - 110, `Configuration_Validating`.
  - 111, `Configuration_ValueIsValid`.
  - 112, `Configuration_Validated`.
  - 506, `Configuration_ValueIsInvalid`.
  - 507, `Configuration_ValidationFailed`.
- Explicit configuration-name and validation-attempt correlation properties.
- Generated relationships linking validation beginning to aggregate success
  and failure outcomes.
- Configuration event demonstrations in the sample applications.
- Contract tests covering identities, severity, structured properties,
  filtering, correlation, and stateless emission.
- Configuration validation guidance with an illustrated outcome flow.

## 0.0.3 - 2026-10-18

### Added

- `AddApplicationEvents()` registration overloads.
- Application operating-mode events:
  - 401, `Application_InDegradedMode`, reports reduced functionality
    with a structured reason.
  - 204, `Application_InNormalMode`, reports recovery from degradation.
- Event relationships generated from the catalog into XML documentation
  cross-references, linked Markdown entries, and Mermaid lifecycle diagrams.
- Interactive degraded/recovered mode simulation in the console sample.
- Manual event simulation through `--simulate-degraded`.
- Cached-fallback demonstrations in the Blazor, Worker Service, and WPF samples:
  - Simulated dependency timeouts preserve the last successful values.
  - Degradation and recovery events are emitted once per mode transition.
  - Blazor and WPF provide availability and refresh controls.
  - Worker Service uses the presence of `degraded.txt` to simulate an outage.
- Contract tests for operating-mode event identities, severity,
  structured properties, filtering, and explicit emission behavior.
- API reference landing page.

### Fixed

- Generated Markdown escaping that displayed apostrophes as literal
  HTML character references.
- Documentation navigation links to the event catalog and API reference.

### Deprecated

- `AddPatwareApplicationEvents()` overloads now forward to
  `AddApplicationEvents()` and emit an obsolete warning.
  Existing behavior is preserved.
- The previously proposed `AddPatwareDiagnostics()` name is deprecated
  in favor of `AddDiagnostics()` for the planned all-in-one registration API.
  Neither umbrella method is implemented yet.

## 0.0.2 - 2026-10-17

### Added

- Critical event 701, `Application_UnhandledException`, preserving the
  original exception through the logging exception channel.
- Opt-in runtime unhandled-exception reporting through
  `AddPatwareApplicationEvents(options => options.LogUnhandledExceptions = true)`.
- Exception simulation scenarios in the console, Blazor, WPF, and worker samples.

## 0.0.1 - 2026-10-17

### Added

- Structured application lifecycle events for starting, started, stopping,
  and stopped states.
- Automatic lifecycle logging through the .NET Generic Host.
- Optional console formatting with bold, severity-colored event identifiers.
- Core and Hosting packages targeting .NET 10.
