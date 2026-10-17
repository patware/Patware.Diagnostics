# Instrumentation

## Objective

Adoption should require as little developer-written instrumentation as practical. Registration activates safe automatic instrumentation appropriate to the application model.

Conceptually:

```csharp
builder.AddCommonEvents();
```

The exact API is not yet fixed.

## Golden rule

> Automatically emit an event only when the framework provides enough information to know that the event's documented semantics are true.

Automatic instrumentation must not guess business state.

## Generic Host

`IHostApplicationLifetime` and related supported hosting abstractions provide natural lifecycle signals. Candidate events include:

- application/host starting, where a reliable hook exists;
- application started;
- application stopping;
- application stopped.

Care is required around "starting": registration time is not necessarily equivalent to the host beginning execution. Event semantics must reflect the actual framework hook rather than a desired narrative.

## Worker Service

A Worker Service is fundamentally a Generic Host application. Hosting instrumentation should therefore provide the baseline automatically. A Worker-specific package is unnecessary unless future Worker-specific semantics justify one.

The library must not assume that a `BackgroundService` completing means the entire application stopped or failed; framework semantics determine what can be inferred.

## ASP.NET Core / Web API

Potential automatic integration points include middleware, exception handling boundaries, hosting diagnostics, endpoint/request completion signals, and health-check infrastructure.

Default behavior should avoid turning the package into generic request logging. Per-request "started/completed" events can be extremely high volume and duplicate ASP.NET Core/OpenTelemetry instrumentation. Such behavior should be omitted or explicitly opt-in unless it carries unique Common Event semantics.

HTTP status codes should not automatically be mapped to Common Event categories without careful semantics. A `404` can be routine; a `500` response may already be captured by existing framework telemetry.

## Blazor

Blazor-specific instrumentation should add only meaningful Blazor semantics. For server-side interactive components, candidate hooks include supported circuit lifecycle callbacks such as circuit opened, connection changes, and circuit closed.

Blazor instrumentation inherits ASP.NET Core and Hosting behavior rather than duplicating it.

Client-side/WebAssembly scenarios require separate analysis because logging destination, lifecycle, execution environment, and telemetry transport differ materially from server-side applications.

## Unhandled exceptions

"Unhandled" must have a precise definition. An exception observed by middleware is not necessarily unhandled if a later handler converts it into a valid response. The library should emit an unhandled-failure event only at a point where the framework semantics justify that classification.

Do not add global exception handlers that change application behavior merely to obtain telemetry.

## Health checks

Health-check results can provide useful signals, but the library should avoid duplicating every probe execution as a log event. State-transition-oriented instrumentation—healthy to degraded/unhealthy and recovery—may be more operationally valuable, subject to reliable implementation and state management.

## Dependency failures

OpenTelemetry already captures many dependency spans. A Common Event should represent an operational semantic condition, such as a dependency becoming unavailable or the application entering degraded mode, rather than duplicate every failed outbound call.

## Explicit API

Explicit APIs remain appropriate when application code possesses required meaning:

```csharp
logger.DependencyUnavailable(...);
logger.FallbackActivated(...);
logger.ConfigurationInvalid(...);
```

Names above are illustrative. The catalog determines the eventual API.

## Duplicate suppression

Specialized layers must coordinate ownership of events so that one framework condition produces one Common Event. Automatic instrumentation should also coexist with built-in ASP.NET Core logging and OpenTelemetry without pretending to replace or suppress those systems.
