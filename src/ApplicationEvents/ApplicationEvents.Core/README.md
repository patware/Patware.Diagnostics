# Patware.Diagnostics.ApplicationEvents.Core

Structured application lifecycle events for `Microsoft.Extensions.Logging`.

Group related events through `logger.Application`, with consistent event
IDs, names, severity levels, and structured properties.

## Requirements

- .NET 10.
- C# 14 or later for the `logger.Application` extension property syntax.
- An `ILogger` supplied by your application's logging configuration.

## Installation

```shell
dotnet add package Patware.Diagnostics.ApplicationEvents.Core --version 0.0.1
```

Core provides event methods. Your application chooses and configures its
logging providers.

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

All four events use the Information log level.

| ID | Event name | Method | Structured properties |
|---|---|---|---|
| 101 | `Application_Starting` | `Starting()` | None |
| 102 | `Application_Started` | `Started(double totalMilliseconds)` | `TotalMilliseconds` |
| 103 | `Application_Stopping` | `Stopping(string reason)` | `Reason` |
| 104 | `Application_Stopped` | `Stopped()` | None |

The methods preserve structured values so compatible logging providers can
store and query them independently of the rendered message.

Accessing `logger.Application` preserves the original logger category.
It does not create a new logging category or emit an event.

## Lifecycle behavior

- Events are emitted when you call the methods. Core does not automatically
  observe lifecycle notifications or start and stop the application.
- Supply startup duration in milliseconds. Core does not manage the timer.
- Supply a non-sensitive shutdown reason. Use `"Unknown"` when the cause
  is unavailable.
- Emit `Stopped()` after application cleanup completes. It does not prove
  that the operating-system process has exited.
- Abrupt termination can prevent shutdown events from being emitted.
- Normal logging filters apply; disabling Information logging suppresses
  these events.

## Automatic host integration

For applications using the .NET Generic Host, use
`Patware.Diagnostics.ApplicationEvents.Hosting` to emit lifecycle events
automatically:

```csharp
builder.AddPatwareApplicationEvents();
```

Import `Patware.Diagnostics.ApplicationEvents.Hosting` for that extension.

The Hosting package depends on Core and also offers optional console
formatting. Avoid manually emitting the same lifecycle events when automatic
logging is enabled for that host.

## Source and license

- [Source repository](https://github.com/patware/Patware.Diagnostics)
- [Release history](https://github.com/patware/Patware.Diagnostics/releases)
- [MIT license](https://github.com/patware/Patware.Diagnostics/blob/main/LICENSE)
