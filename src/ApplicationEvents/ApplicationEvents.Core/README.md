# Patware.Diagnostics.ApplicationEvents.Core

Structured application lifecycle, operating-mode, configuration, dependency
resolution, external connection, and failure events for `Microsoft.Extensions.Logging`.

Group related events through `logger.Application`, `logger.Configuration`,
`logger.Dependency`, and `logger.External`, with consistent event IDs, names,
severity levels, and structured properties.

## Requirements

- .NET 10.
- C# 14 or later for the `logger.Application` extension property syntax.
- An `ILogger` supplied by your application's logging configuration.

## Installation

```shell
dotnet add package Patware.Diagnostics.ApplicationEvents.Core
```

Core provides event methods and an opt-in connection observation helper.
Your application chooses and configures its logging providers.

## Usage

Import the event namespace:

```csharp
using Patware.Diagnostics.ApplicationEvents;
```

Call the methods at the corresponding points in your application's lifecycle:

```csharp
// Immediately before application initialization:
logger.Application.Starting();

// After successful initialization, using your measured startup duration:
logger.Application.Started(totalMilliseconds: timer.Elapsed.TotalMilliseconds);

// When shutdown begins:
logger.Application.Stopping(reason: "User requested shutdown");

// After shutdown cleanup completes, while logging remains available:
logger.Application.Stopped();
```

Here, `logger` is your existing `ILogger`, and `timer` is a `Stopwatch`
started when initialization begins. These calls belong at separate lifecycle
stages; they are not a complete application startup/shutdown implementation.

## Available events

| ID | Event name | Level | Method | Structured properties |
|---|---|---|---|---|
| 101 | `Application_Starting` | Information | `Starting()` | None |
| 102 | `Application_Started` | Information | `Started(double totalMilliseconds)` | `TotalMilliseconds` |
| 103 | `Application_Stopping` | Information | `Stopping(string reason)` | `Reason` |
| 104 | `Application_Stopped` | Information | `Stopped()` | None |
| 110 | `Configuration_Validating` | Information | `Configuration.Validating(...)` | `ConfigurationName`, `ValidationId` |
| 111 | `Configuration_ValueIsValid` | Information | `Configuration.ValueIsValid(...)` | `ConfigurationName`, `ValidationId`, `SettingName` |
| 112 | `Configuration_Validated` | Information | `Configuration.Validated(...)` | `ConfigurationName`, `ValidationId` |
| 115 | `Dependency_Resolving` | Information | `Dependency.Resolving(...)` | `DependencyName`, `OperationName`, `ResolutionId` |
| 116 | `Dependency_Resolved` | Information | `Dependency.Resolved(...)` | `DependencyName`, `OperationName`, `ResolutionId` |
| 201 | `External_Connecting` | Information | `External.Connecting(...)` | `ExternalName`, `OperationName`, `OperationId` |
| 202 | `External_Connected` | Information | `External.Connected(...)` | `ExternalName`, `OperationName`, `OperationId` |
| 203 | `External_Calling` | Information | `External.Calling(...)` | `ExternalName`, `OperationName`, `OperationId` |
| 204 | `Application_InNormalMode` | Information | `Application.InNormalMode()` | None |
| 214 | `External_CallCompleted` | Information | `External.CallCompleted(...)` | `ExternalName`, `OperationName`, `OperationId` |
| 401 | `Application_InDegradedMode` | Warning | `Application.InDegradedMode(string reason)` | `Reason` |
| 501 | `Configuration_LoadFailed` | Warning | `Configuration.LoadFailed(...)` | `ConfigurationName`, `SourceName`, `LoadId`, `ReasonCode` |
| 502 | `External_AuthenticationFailed` | Warning | `External.AuthenticationFailed(...)` | `ExternalName`, `OperationName`, `OperationId`, `ReasonCode` |
| 503 | `External_AuthorizationFailed` | Warning | `External.AuthorizationFailed(...)` | `ExternalName`, `OperationName`, `OperationId`, `ReasonCode` |
| 504 | `Dependency_ResolutionFailed` | Warning | `Dependency.ResolutionFailed(...)` | `DependencyName`, `OperationName`, `ResolutionId`, `ReasonCode` |
| 506 | `Configuration_ValueIsInvalid` | Warning | `Configuration.ValueIsInvalid(...)` | `ConfigurationName`, `ValidationId`, `SettingName`, `Reason` |
| 507 | `Configuration_ValidationFailed` | Warning | `Configuration.ValidationFailed(...)` | `ConfigurationName`, `ValidationId`, `Reason` |
| 601 | `External_OperationTimedOut` | Error | `External.OperationTimedOut(...)` | `ExternalName`, `OperationName`, `OperationId`, `TimeoutMilliseconds` |
| 602 | `External_ServiceUnavailable` | Error | `External.ServiceUnavailable(...)` | `ExternalName`, `OperationName`, `OperationId`, `ReasonCode` |
| 701 | `Application_UnhandledException` | Critical | `UnhandledException(Exception exception)` | None; exception passed separately |


The methods preserve structured values so compatible logging providers can
store and query them independently of the rendered message.

Accessing `logger.Application` preserves the original logger category.
It does not create a new logging category or emit an event.

Exceptions for 501, 502, 503, 504, 601, 602, and 701 travel through the logging
exception channel, not structured message properties. Events 601 and 701 require
an original exception; the other listed failure methods accept null.

## Observe an external connection

`ConnectExternalAsync` observes an explicitly supplied connection operation. It
does not automatically intercept dependency clients or dependency injection.
This example assumes an existing `ILogger` named `logger`, a validated `Uri`
named `endpoint`, and an application shutdown token named `shutdownToken`:

```csharp
using System.Net.WebSockets;
using Patware.Diagnostics.ApplicationEvents;

using ClientWebSocket socket = new();
string operationId = Guid.NewGuid().ToString("N");

await logger.ConnectExternalAsync(
    externalName: "WebSocketEcho",
    operationName: "OpenWebSocket",
    timeout: TimeSpan.FromSeconds(10),
    connectAsync: token => socket.ConnectAsync(endpoint, token),
    cancellationToken: shutdownToken,
    operationId: operationId);

// Use the connection and perform graceful cleanup outside observation.
```

- Emits 201 before invoking the delegate and 202 after successful completion.
- Returns `Task`. An omitted or null `operationId` generates an identifier;
  supply one when caller failure or cleanup records need the same correlation.
- Requests cancellation after a positive, finite timeout. The delegate must
  honor its supplied token; the helper cannot forcibly stop uncooperative code.
- Emits 601 and rethrows the original cancellation exception when the delegate
  observes deadline cancellation without caller cancellation.
- Propagates caller cancellation without 601. Caller cancellation takes
  precedence when both caller and deadline tokens are canceled.
- Propagates other exceptions without guessing authentication, authorization,
  or availability outcomes. Logging-provider exceptions are not suppressed.
- Remains operational when logging is filtered. It does not retry, select a
  fallback, change application mode, or own the connection resource.

Include only connection establishment in the delegate. A WebSocket handshake
does not establish successful message exchange. Do not manually emit another
201/202 pair around the helper. Use a fresh identifier for each attempt.

The individual event methods only emit records; the helper explicitly adds
observation and cooperative timeout orchestration.

## Reporting an unrecoverable exception

At a top-level application failure boundary, pass the original exception:

```csharp
logger.Application.UnhandledException(exception);
```

Here, `exception` is the non-null `Exception` caught at that boundary.
The method passes it through `ILogger`'s exception channel, preserving its
type, stack trace, and inner exceptions for logging providers. It is not
converted into a message-template property or string.

Reserve event 701 for failures that prevent the application from continuing
normally. A recoverable operation or request failure does not imply an
application-ending failure. A top-level catch block may report this event
and initiate shutdown even though it has caught the exception.

The method only emits a record. It does not install exception handlers,
initiate shutdown, terminate the process, or flush providers. Passing null
throws `ArgumentNullException`, including when Critical logging is disabled.

## Lifecycle behavior

- Events are emitted when you call the methods. Core does not automatically
  observe lifecycle notifications or start and stop the application.
- Supply startup duration in milliseconds. Core does not manage the timer.
- Supply a non-sensitive shutdown reason. Use `"Unknown"` when the cause
  is unavailable.
- Emit `Stopped()` after application cleanup completes. It does not prove
  that the operating-system process has exited.
- Abrupt termination can prevent shutdown events from being emitted.
- Normal logging filters apply. Events 101–104 require Information logging;
  event 701 requires Critical logging. A Warning minimum level suppresses
  101–104 but still permits 701.

## Automatic host integration

For applications using the .NET Generic Host, use
`Patware.Diagnostics.ApplicationEvents.Hosting` to emit lifecycle events
automatically:

```csharp
builder.AddApplicationEvents();
```

Import `Patware.Diagnostics.ApplicationEvents.Hosting` for that extension.

The Hosting package depends on Core and also offers optional console
formatting and opt-in runtime unhandled-exception reporting through
`options.LogUnhandledExceptions = true`. This observer has limited coverage;
application-specific failure boundaries may still need explicit logging.
Avoid manually emitting the same lifecycle events when automatic logging
is enabled for that host.

## Source and license

- [Source repository](https://github.com/patware/Patware.Diagnostics)
- [Release history](https://github.com/patware/Patware.Diagnostics/releases)
- [MIT license](https://github.com/patware/Patware.Diagnostics/blob/main/LICENSE)
