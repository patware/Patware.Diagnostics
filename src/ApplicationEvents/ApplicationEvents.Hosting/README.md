# Patware.Diagnostics.ApplicationEvents.Hosting

Automatic application lifecycle events for the .NET Generic Host, with
optional console formatting and runtime unhandled-exception reporting.

## Requirements and installation

- .NET 10.
- An application using the .NET Generic Host, including ASP.NET Core hosts.

```shell
dotnet add package Patware.Diagnostics.ApplicationEvents.Hosting
```

This package depends on `Patware.Diagnostics.ApplicationEvents.Core`.
A console application that constructs a Generic Host also needs
`Microsoft.Extensions.Hosting`; ASP.NET Core applications obtain hosting
support through their framework.

Configuration validation events require version 0.0.4 or later.

## Automatic lifecycle logging

```csharp
using Microsoft.Extensions.Hosting;
using Patware.Diagnostics.ApplicationEvents.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddApplicationEvents();

using IHost host = builder.Build();
await host.RunAsync();
```

Register before building the host. Events use the category
`Patware.Diagnostics.ApplicationEvents.Hosting` and the application's
logging providers and filters.

| ID | Event name | Level | Trigger |
|---|---|---|---|
| 101 | `Application_Starting` | Information | Lifecycle observer's `StartingAsync` callback |
| 102 | `Application_Started` | Information | `ApplicationStarted` notification |
| 103 | `Application_Stopping` | Information | `ApplicationStopping` notification |
| 104 | `Application_Stopped` | Information | `ApplicationStopped` notification |

## Optional console formatting

Replace the default registration with:

```csharp
builder.AddApplicationEvents(options =>
{
    options.FormatLogs = true;
});
```

`FormatLogs` defaults to `false`. Enabling it adds/selects the Patware console
formatter without removing other providers or changing logging filters.
It formats all records received by the console provider, including framework
logs.

Event identifiers are bold and colored by severity: Information is green,
Warning is yellow, and Error/Critical are red. Trace/Debug identifiers use
bold with the terminal's default color. Styling ends immediately after the
identifier. Timestamps, messages, exceptions, and enabled scopes are retained.

Automatic color detection disables ANSI styling when either console stream
is redirected, `NO_COLOR` is nonempty, or `TERM` is `dumb`.

This setting applies to console output. It does not change Visual Studio's
Debug output or the rendering used by other providers. The Microsoft Debug
provider does not display event IDs.

## Lifecycle behavior

The package emits events 101–104 through the application's existing
logging providers.

Startup duration measures the interval from the observer's StartingAsync
callback to ApplicationStarted. It does not include builder configuration,
host construction, or earlier startup work.

The shutdown reason is Unknown because host lifetime notifications do not
provide the cause.

ApplicationStopped means the host reached its stopped notification. It
does not prove process exit or that every cleanup operation succeeded.

Do not also emit the same lifecycle events manually for the same host.

## Optional unhandled-exception reporting

```csharp
builder.AddApplicationEvents(options =>
{
    options.LogUnhandledExceptions = true;
    // Console formatting is independent and may also be enabled.
    options.FormatLogs = true;
});
```

`LogUnhandledExceptions` defaults to `false`. When enabled, a hosted observer
subscribes to `AppDomain.CurrentDomain.UnhandledException`. A terminating
notification carrying an `Exception` emits Critical event 701,
`Application_UnhandledException`, with the original exception.

The observer's coverage is limited:

- Observation starts in the observer's `StartAsync` and lasts until disposal,
  including host shutdown. Earlier and later failures are outside its scope.
- Exceptions already handled by application code, the Generic Host, or a UI
  framework do not necessarily reach this runtime notification. In particular,
  a `BackgroundService` failure handled by the host is not captured by this hook.
- The observer does not subscribe to `TaskScheduler.UnobservedTaskException`.
- Reporting is best effort. Buffered providers may not flush before process
  termination, and logging failures in the notification handler are suppressed.
- The observer does not recover the application, initiate graceful shutdown,
  set an exit code, or change runtime exception policy.

Enable this option on one host per process to avoid duplicate reporting.
With the default sequential host startup, register Patware before hosted
services that may fail so the observer starts first. This does not provide
coverage for every startup failure or for concurrently starting services.

An application-specific failure boundary can still call
`logger.Application.UnhandledException(exception)` explicitly through Core.
Avoid reporting the same failure both manually and through the runtime hook.

## Registration behavior

Options are evaluated during registration, not dynamically reloaded.
Repeated registration does not add duplicate observers within one service
collection. A later call with an option set to `false` does not undo an earlier
formatter selection or exception-observer registration. Configure these
options together during startup.

## Source and license

- [Source repository](https://github.com/patware/Patware.Diagnostics)
- [Release history](https://github.com/patware/Patware.Diagnostics/releases)
- [MIT license](https://github.com/patware/Patware.Diagnostics/blob/main/LICENSE)
