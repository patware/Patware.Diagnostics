using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

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

bool shutdownAttempted = false;

try
{
    await host.StartAsync();

    if (args.Contains("--simulate-fatal", StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Simulated unrecoverable application failure.",
            new ArgumentException("Simulated invalid configuration."));
    }

    if (args.Contains("--simulate-degraded", StringComparer.OrdinalIgnoreCase))
    {
        // Simulate an application-owned transition to reduced functionality.
        // Logging the event does not activate an actual fallback.
        logger.Application.InDegradedMode(
            reason: "Simulated product service outage; using cached data.");

        // Simulate recovery after all degradation conditions have cleared.
        // A real application would emit this after verifying its recovery.
        logger.Application.InNormalMode();
    }

    Console.WriteLine("Application is running. Press Ctrl+C to stop.");

    // WaitForShutdownAsync executes the host shutdown sequence.
    shutdownAttempted = true;
    await host.WaitForShutdownAsync();
}
catch (Exception exception)
{
    // This boundary reports a failure that lower-level code did not recover.
    // The process returns a failure code after cleanup.
    Environment.ExitCode = 1;
    logger.Application.UnhandledException(exception);
}
finally
{
    if (!shutdownAttempted)
    {
        // A startup or application failure bypassed WaitForShutdownAsync.
        // Attempt bounded cleanup while logging is still available.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        try
        {
            await host.StopAsync(timeout.Token);
        }
        catch (Exception cleanupException)
        {
            Environment.ExitCode = 1;
            logger.LogError(
                cleanupException,
                "Cleanup after application failure did not complete.");
        }
    }
}