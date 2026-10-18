namespace SampleWorkerService;

/// <summary>
/// Polls for graceful-shutdown and fatal-exception simulation files.
/// </summary>
/// <param name="logger">The logger for worker activity.</param>
/// <param name="lifetime">The host lifetime used to request shutdown.</param>
/// <remarks>
/// <para>
/// Trigger paths are resolved against the current working directory and
/// logged at startup. Each trigger is deleted before its action executes
/// so the consumed request is not repeated on the next run.
/// </para>
/// <para>
/// The fatal scenario throws on a dedicated thread. Throwing directly
/// from ExecuteAsync would instead exercise the Generic Host's
/// background-service exception handling.
/// </para>
/// </remarks>
public sealed class Worker(ILogger<Worker> logger, IHostApplicationLifetime lifetime) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        string stopFilePath = Path.GetFullPath("stop.txt");
        string exceptionFilePath = Path.GetFullPath("exception.txt");

        logger.LogInformation(
            "For graceful shutdown, create {StopFilePath}",
            stopFilePath);

        logger.LogInformation(
            "To simulate a fatal exception and terminate the process, " +
            "create {ExceptionFilePath}",
            exceptionFilePath);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Give graceful shutdown priority if both files exist.
                if (File.Exists(stopFilePath))
                {
                    File.Delete(stopFilePath);

                    logger.LogInformation(
                        "Stop file detected at {StopFilePath}. " +
                        "Requesting graceful shutdown.",
                        stopFilePath);

                    lifetime.StopApplication();
                    return;
                }

                if (File.Exists(exceptionFilePath))
                {
                    File.Delete(exceptionFilePath);

                    logger.LogInformation(
                        "Exception file detected at {ExceptionFilePath}. " +
                        "Triggering a fatal exception.",
                        exceptionFilePath);

                    StartFatalExceptionSimulation();

                    // Returning from this worker does not stop the host.
                    // Keep the host's runtime observer active while the
                    // dedicated thread triggers process termination.
                    return;
                }

                logger.LogInformation("Worker running at {Time}", DateTimeOffset.Now);

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Cancellation during polling is normal host shutdown.
        }
    }

    /// <summary>
    /// Starts a thread that deliberately raises an unhandled exception.
    /// </summary>
    /// <remarks>
    /// The exception is intentionally not caught or stored in a Task.
    /// The registered Patware observer owns emission of event 701.
    /// </remarks>
    private static void StartFatalExceptionSimulation()
    {
        Thread crashThread = new(static () =>
        {
            throw new InvalidOperationException(
                "Simulated fatal exception triggered by exception.txt.",
                new ArgumentException("Simulated underlying failure."));
        })
        {
            Name = "WorkerSimulatedFatalException",
            IsBackground = true
        };

        crashThread.Start();
    }
}