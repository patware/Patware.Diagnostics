# Configuration

## Philosophy

The default experience should be useful with one registration call and no mandatory configuration.

Conceptually:

```csharp
builder.AddCommonEvents();
```

Configuration exists to change legitimate behavior, not to expose every internal implementation detail.

## Desired characteristics

- sensible defaults;
- consistent API across application models;
- additive specialization;
- configuration through standard .NET options patterns where needed;
- validation of invalid combinations at startup where practical;
- no requirement to configure a logging destination;
- no hidden modification of the application's existing logging providers.

## Candidate options

Potential options may include enabling optional high-volume instrumentation, controlling selected automatic event families, adding non-sensitive static application metadata, or configuring health state-transition behavior.

Options should be introduced only when a demonstrated use case exists.

## Logging levels

Standard event definitions own their default levels. Applications can still use normal `Microsoft.Extensions.Logging` category filtering to control emission. Avoid creating a second parallel filtering system unless standard logging filters cannot meet a real requirement.

## Registration idempotency

Where feasible, calling registration more than once should not create duplicate observers or duplicate events. If full idempotency is impractical, duplicate registration should be detectable and documented.

## Environment behavior

The semantic meaning of an event must not change between Development, Test, and Production. Configuration may affect whether optional telemetry is emitted, but `ApplicationStarted` must mean the same thing everywhere.
