using Patware.Diagnostics.ApplicationEvents;

namespace SampleWorkerService;

/// <summary>
/// Demonstrates cached fallback, recovery, and host shutdown scenarios.
/// </summary>
/// <param name="logger">The logger for worker activity and application events.</param>
/// <param name="lifetime">The host lifetime used to request shutdown.</param>
/// <remarks>
/// <para>
/// Trigger paths are resolved against the current working directory and
/// logged at startup. Stop and exception files are consumed before their
/// actions execute. The degraded file remains until the operator removes it.
/// </para>
/// <para>
/// The degraded file simulates an unavailable dependency. A timed-out refresh
/// establishes degradation; a successful refresh establishes recovery.
/// Application events are emitted only when the operating mode changes.
/// </para>
/// <para>
/// The cache is in memory and does not survive a process restart.
/// This sample retries each cycle rather than implementing a circuit breaker.
/// </para>
/// </remarks>
public sealed class Worker(
    ILogger<Worker> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private bool _isDegraded;
    private decimal? _cachedPrice;
    private DateTimeOffset? _cachedAt;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        string stopFilePath = Path.GetFullPath("stop.txt");
        string exceptionFilePath = Path.GetFullPath("exception.txt");
        string degradedFilePath = Path.GetFullPath("degraded.txt");

        logger.LogInformation(
            "For graceful shutdown, create {StopFilePath}",
            stopFilePath);

        logger.LogInformation(
            "To simulate a fatal exception, create {ExceptionFilePath}",
            exceptionFilePath);

        logger.LogInformation(
            "To simulate a product-service outage, create {DegradedFilePath}. " +
            "Remove it to allow the next refresh to recover.",
            degradedFilePath);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Process control requests before beginning another refresh.
                // Graceful shutdown takes priority when multiple files exist.
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

                    // Returning does not stop the host. Keep its runtime
                    // observer active until the fatal thread terminates it.
                    return;
                }

                await RefreshPriceAsync(degradedFilePath, stoppingToken);

                // This delay also bounds routine sample log volume.
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is not a dependency failure or a recovery.
        }
    }

    /// <summary>
    /// Refreshes the cached price or retains it when the service times out.
    /// </summary>
    /// <param name="degradedFilePath">The persistent simulated-outage switch.</param>
    /// <param name="stoppingToken">Cancellation requested by host shutdown.</param>
    /// <remarks>
    /// The worker executes refreshes sequentially, so no additional locking
    /// is needed for its cache or operating-mode state.
    /// Recovery is recorded only after a successful response replaces the cache.
    /// </remarks>
    private async Task RefreshPriceAsync(
        string degradedFilePath,
        CancellationToken stoppingToken)
    {
        using CancellationTokenSource timeout = new(
            TimeSpan.FromSeconds(2));

        using CancellationTokenSource request =
            CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                timeout.Token);

        try
        {
            decimal price = await FetchPriceAsync(
                degradedFilePath,
                request.Token);

            stoppingToken.ThrowIfCancellationRequested();

            _cachedPrice = price;
            _cachedAt = DateTimeOffset.UtcNow;

            if (_isDegraded)
            {
                _isDegraded = false;
                logger.Application.InNormalMode();
            }

            logger.LogInformation(
                "Fetched coffee price: {Price} CAD at {FetchedAt}",
                price,
                _cachedAt);
        }
        catch (OperationCanceledException)
            when (timeout.IsCancellationRequested
                  && !stoppingToken.IsCancellationRequested)
        {
            if (!_isDegraded)
            {
                _isDegraded = true;

                logger.Application.InDegradedMode(
                    reason: _cachedPrice.HasValue
                        ? "Product service timed out; using the cached coffee price."
                        : "Product service timed out; no cached coffee price is available.");
            }

            if (_cachedPrice is decimal cachedPrice)
            {
                logger.LogInformation(
                    "Refresh timed out. Using cached coffee price: " +
                    "{Price} CAD, last refreshed at {FetchedAt}",
                    cachedPrice,
                    _cachedAt);
            }
            else
            {
                logger.LogInformation(
                    "Refresh timed out. No cached price is available; " +
                    "waiting for a successful refresh.");
            }
        }
    }

    /// <summary>
    /// Simulates fetching a coffee price from an unreliable service.
    /// </summary>
    /// <param name="degradedFilePath">The file indicating a simulated outage.</param>
    /// <param name="cancellationToken">Cancellation for this service request.</param>
    /// <returns>The simulated coffee price in CAD.</returns>
    /// <remarks>
    /// Availability is sampled when the request begins. Removing the file
    /// during an unavailable request allows the next request to succeed.
    /// The file's contents are irrelevant; its presence controls the simulation.
    /// </remarks>
    private static async Task<decimal> FetchPriceAsync(
        string degradedFilePath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(degradedFilePath))
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        await Task.Delay(
            TimeSpan.FromMilliseconds(350),
            cancellationToken);

        return 12m + Random.Shared.Next(0, 501) / 100m;
    }

    /// <summary>
    /// Starts a thread that deliberately raises an unhandled exception.
    /// </summary>
    /// <remarks>
    /// Throwing directly from ExecuteAsync would exercise the Generic Host's
    /// background-service exception handling instead. The dedicated thread
    /// allows the registered runtime observer to emit event 701.
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