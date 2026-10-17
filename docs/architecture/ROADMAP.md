# Roadmap and Open Decisions

This roadmap for Patware Diagnostics. It prioritizes settling contracts and boundaries before implementation.

## Phase 0 — Product definition

- Finalize vision, terminology, and non-goals.
- Decide supported .NET versions and target frameworks.
- Finalize initial event ranges and catalog rules.
- Decide whether the product scope is logging events specifically or broader instrumentation.

## Phase 1 — Core event contract

- Define the authoritative event representation.
- Define initial lifecycle events and exact IDs.
- Define structured property conventions.
- Define explicit event API shape.
- Establish generated human/machine-readable catalog approach.
- Add contract tests.

## Phase 2 — Generic Host MVP

- Implement minimal registration API.
- Instrument reliable Generic Host lifecycle events.
- Validate event ordering and shutdown behavior.
- Create Worker Service sample.
- Benchmark startup/shutdown overhead.

A successful MVP should demonstrate that a developer can add one package, one registration call, and receive useful standardized lifecycle telemetry through their existing `ILogger` configuration.

## Phase 3 — ASP.NET Core

- Determine web-specific events that add value beyond built-in ASP.NET Core/OpenTelemetry telemetry.
- Implement only reliable, low-noise automatic instrumentation.
- Define exception semantics carefully.
- Create Web API sample.

## Phase 4 — Blazor

- Analyze Blazor Web App hosting/render modes separately.
- Add server-side circuit events where stable public hooks exist.
- Avoid implying support for client-side scenarios until implemented and tested.
- Create Blazor sample.

## Phase 5 — Observability refinement

- Validate OpenTelemetry export behavior.
- Align structured properties with applicable semantic conventions.
- Evaluate health state-transition events.
- Evaluate whether any metrics or activities belong in project scope.

## Open design decisions

| Decision | Key question |
| --- | --- |
| Registration API | `builder.AddCommonEvents()`, `services.Add...`, or application-model-specific overloads? |
| Event source of truth | C# definitions, generated source, JSON/YAML specification, or another model? |
| Event ID allocation | Which exact events occupy the initial ranges? |
| Startup semantics | Which framework hook accurately means "application starting"? |
| Exception semantics | At what boundary is an exception truly unhandled? |
| Health events | Log every result, only transitions, or leave to existing health tooling? |
| HTTP events | Which, if any, web request outcomes deserve Common Events? |
| OpenTelemetry | Logging-only integration initially, or explicit Activity/Meter features? |
| Application custom events | Should a reserved range/convention exist? |
| Version support | Current LTS only, current + previous, or another policy? |

## Definition of v1.0

Version 1.0 should mean the public API and standard event contract are considered stable enough for production monitoring rules to depend upon them. It should not mean that every .NET application model has been instrumented.
