# Patware.ApplicationEvents.Hosting

Automatic application lifecycle events for the .NET Generic Host.

```csharp
using Microsoft.Extensions.Hosting;
using Patware.ApplicationEvents;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddPatwareApplicationEvents();

await builder.Build().RunAsync();
```

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
