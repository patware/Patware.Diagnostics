# Changelog

User-visible changes are recorded here. `VERSION` is the current build version;
published releases are identified by package and release date below.

## Unreleased

### Added

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
