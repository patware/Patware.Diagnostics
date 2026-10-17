namespace SampleWorkerService;

/// <summary>
/// Runs until cancellation or detection of a stop request file.
/// </summary>
/// <param name="logger">The logger for worker activity.</param>
/// <param name="lifetime">The host lifetime used to request shutdown.</param>
/// <remarks>
/// StopApplication requests coordinated host shutdown rather than
/// terminating the process immediately. Returning afterward allows
/// this worker to finish without waiting for another polling interval.
/// </remarks>
public class Worker(ILogger<Worker> logger, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string stopFilePath = Path.GetFullPath("stop.txt");

        logger.LogInformation("To request graceful shutdown, create {StopFilePath}", stopFilePath);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }

                if (System.IO.File.Exists(stopFilePath))
                {
                    System.IO.File.Delete(stopFilePath);

                    logger.LogInformation("Stop file detected at {StopFilePath}. Requesting shutdown.", stopFilePath);

                    lifetime.StopApplication();

                    return;
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Cancellation during the polling delay is normal shutdown.
        }
    }
}
