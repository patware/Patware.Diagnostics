using Patware.Diagnostics.ApplicationEvents;

using SampleApplications.Shared;

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
/// actions execute. The degraded and unavailable files remain until removed.
/// </para>
/// <para>
/// The degraded file simulates an unresponsive dependency; the unavailable
/// file simulates immediate rejection and takes precedence when both exist.
/// Failed refreshes establish degradation; a successful refresh establishes
/// recovery. Application mode events occur only when the mode changes.
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
        string unavailableFilePath = Path.GetFullPath("unavailable.txt");

        logger.LogInformation(
            "For graceful shutdown, create {StopFilePath}",
            stopFilePath);

        logger.LogInformation(
            "To simulate a fatal exception, create {ExceptionFilePath}",
            exceptionFilePath);

        logger.LogInformation(
            "To simulate an unresponsive product service, create {DegradedFilePath}.",
            degradedFilePath);

        logger.LogInformation(
            "To simulate immediate service unavailability, create {UnavailableFilePath}. " +
            "This takes precedence over degraded.txt. Remove both files to allow recovery.",
            unavailableFilePath);

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

                await RefreshPriceAsync(degradedFilePath, unavailableFilePath, stoppingToken);

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
    /// Refreshes the cached price or retains it when the dependency fails.
    /// </summary>
    /// <param name="degradedFilePath">The persistent simulated-outage switch.</param>
    /// <param name="unavailableFilePath">
    /// The persistent switch for immediate service unavailability.
    /// </param>
    /// <param name="stoppingToken">Cancellation requested by host shutdown.</param>
    /// <remarks>
    /// The worker executes refreshes sequentially, so no additional locking
    /// is needed for its cache or operating-mode state.
    /// Recovery is recorded only after a successful response replaces the cache.
    /// <para>
    /// Confirmed timeouts emit event 601; explicit unavailable results emit
    /// event 602 without an exception. Each failed attempt has its own identifier.
    /// Event 401 occurs only when entering degraded mode, and event 204 occurs
    /// only after a successful refresh. Shutdown cancellation propagates.
    /// </para>
    /// </remarks>
    private async Task RefreshPriceAsync(
        string degradedFilePath,
        string unavailableFilePath,
        CancellationToken stoppingToken)
    {
        string operationId = Guid.NewGuid().ToString("N");
        TimeSpan timeoutBudget = TimeSpan.FromSeconds(2);

        using CancellationTokenSource timeout = new(timeoutBudget);

        using CancellationTokenSource request =
            CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                timeout.Token);

        try
        {
            SimulatedServiceMode mode = GetServiceMode(
                degradedFilePath,
                unavailableFilePath);

            ProductServiceResult result =
                await SimulatedProductService.FetchAsync(mode, request.Token);

            stoppingToken.ThrowIfCancellationRequested();

            if (!result.IsAvailable)
            {
                logger.External.ServiceUnavailable(
                    externalName: "ProductService",
                    operationName: "FetchPrice",
                    operationId: operationId,
                    reasonCode: "ServiceUnavailable",
                    exception: null);

                UseCachedPrice("Product service reported unavailability");
                return;
            }

            decimal price = result.Products
                .Single(product => product.Name == "Coffee")
                .Price;

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
        catch (OperationCanceledException exception)
            when (timeout.IsCancellationRequested
                  && !stoppingToken.IsCancellationRequested)
        {

            logger.External.OperationTimedOut(
                externalName: "ProductService",
                operationName: "FetchPrice",
                operationId: operationId,
                timeoutMilliseconds: timeoutBudget.TotalMilliseconds,
                exception: exception);


            UseCachedPrice("Product service timed out");
        }
    }

    /// <summary>
    /// Selects dependency behavior from the Worker's trigger files.
    /// </summary>
    /// <param name="degradedFilePath">
    /// The switch for an unresponsive dependency.
    /// </param>
    /// <param name="unavailableFilePath">
    /// The switch for immediate unavailability.
    /// </param>
    /// <returns>The behavior to use for the next request.</returns>
    /// <remarks>
    /// Trigger files belong to the Worker sample, not the shared simulation.
    /// Unavailable takes precedence when both files exist. The returned mode
    /// is captured for the request and is unaffected by later file changes.
    /// </remarks>
    private static SimulatedServiceMode GetServiceMode(
        string degradedFilePath,
        string unavailableFilePath)
    {
        if (File.Exists(unavailableFilePath))
        {
            return SimulatedServiceMode.Unavailable;
        }

        return File.Exists(degradedFilePath)
            ? SimulatedServiceMode.Unresponsive
            : SimulatedServiceMode.Available;
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

    /// <summary>
    /// Retains cached values and reports the consequences of a failed refresh.
    /// </summary>
    /// <param name="failure">The observed dependency failure.</param>
    /// <remarks>
    /// Every failed attempt is reported separately by its caller. Application
    /// degradation is reported only on the first transition into reduced service.
    /// </remarks>
    private void UseCachedPrice(string failure)
    {
        if (!_isDegraded)
        {
            _isDegraded = true;

            logger.Application.InDegradedMode(
                reason: _cachedPrice.HasValue
                    ? $"{failure}; using the cached coffee price."
                    : $"{failure}; no cached coffee price is available.");
        }

        if (_cachedPrice is decimal cachedPrice)
        {
            logger.LogInformation(
                "Refresh failed. Using cached coffee price: " +
                "{Price} CAD, last refreshed at {FetchedAt}",
                cachedPrice,
                _cachedAt);
        }
        else
        {
            logger.LogInformation(
                "Refresh failed. No cached price is available; " +
                "waiting for a successful refresh.");
        }
    }
}
