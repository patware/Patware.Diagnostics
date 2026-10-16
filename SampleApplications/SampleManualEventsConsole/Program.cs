using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.ApplicationEvents;

Console.WriteLine("Starting... please wait");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff";
});

// Register application services here, before building the host.

using IHost host = builder.Build();

ILogger logger = host.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("SampleManualEventsConsole");

IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

Stopwatch startupTimer = new();

// Register before starting the host so the startup notification
// cannot occur before our callback is attached.
using CancellationTokenRegistration startedRegistration =
    lifetime.ApplicationStarted.Register(() =>
    {
        startupTimer.Stop();

        logger.Application.Started(
            totalMilliseconds: startupTimer.Elapsed.TotalMilliseconds);
    });

using CancellationTokenRegistration stoppingRegistration =
    lifetime.ApplicationStopping.Register(() =>
    {
        // This notification does not identify the shutdown cause.
        // Do not assume that every shutdown was requested with Ctrl+C.
        logger.Application.Stopping(reason: "Unknown");
    });

using CancellationTokenRegistration stoppedRegistration =
    lifetime.ApplicationStopped.Register(() =>
    {
        // Logging is still available here, before host disposal.
        logger.Application.Stopped();
    });

// IHostApplicationLifetime has no ApplicationStarting notification.
// Emit this explicitly at the beginning of the measured startup phase.
startupTimer.Start();

logger.Application.Starting();

await host.StartAsync();

Console.WriteLine("Application is running. Press Ctrl+C to stop.");

// Wait for a shutdown request and then execute the host's shutdown sequence.
// The default console lifetime handles Ctrl+C.
await host.WaitForShutdownAsync();