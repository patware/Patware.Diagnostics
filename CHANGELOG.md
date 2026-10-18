# Changelog

User-visible changes are recorded here. `VERSION` is the current build version;
published releases are identified by package and release date below.

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
