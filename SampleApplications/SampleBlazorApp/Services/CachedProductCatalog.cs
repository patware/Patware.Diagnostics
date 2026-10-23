using Patware.Diagnostics.ApplicationEvents;

using SampleApplications.Shared;

namespace SampleBlazorApp.Services;

/// <summary>
/// Represents the current cached catalog and its operating state.
/// </summary>
/// <param name="Products">The last successfully fetched product values.</param>
/// <param name="UpdatedAt">When the cached values were last refreshed.</param>
/// <param name="IsDegraded">Whether a refresh failure remains unresolved.</param>
public sealed record CatalogSnapshot(
    IReadOnlyList<ProductPrice> Products,
    DateTimeOffset? UpdatedAt,
    bool IsDegraded);

/// <summary>
/// Demonstrates an unreliable dependency with a cached fallback.
/// </summary>
/// <remarks>
/// This singleton owns the sample application's operating-mode transitions.
/// Availability changes affect subsequent requests; recovery is established
/// only by a successful refresh. Logging methods do not manage this state.
/// This service retries on every refresh and does not implement a circuit breaker.
/// </remarks>
public sealed class CachedProductCatalog : IDisposable
{
    private readonly ILogger<CachedProductCatalog> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHostEnvironment _environment;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private int _mode = (int)SimulatedServiceMode.Available;

    private CatalogSnapshot _snapshot = new(
        Array.Empty<ProductPrice>(),
        UpdatedAt: null,
        IsDegraded: false);

    /// <summary>
    /// Initializes the shared simulation.
    /// </summary>
    /// <param name="logger">The logger receiving operating-mode events.</param>
    /// <param name="lifetime">The host shutdown notifications.</param>
    /// <param name="environment">The environment controlling the backdoor.</param>
    public CachedProductCatalog(
        ILogger<CachedProductCatalog> logger,
        IHostApplicationLifetime lifetime,
        IHostEnvironment environment)
    {
        _logger = logger;
        _lifetime = lifetime;
        _environment = environment;
    }

    /// <summary>
    /// Gets the last published cache snapshot.
    /// </summary>
    /// <remarks>
    /// Each snapshot is immutable so readers cannot observe a partially
    /// updated cache or modify values shared with other browser sessions.
    /// </remarks>
    public CatalogSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>
    /// Gets the mode used by subsequent simulated requests.
    /// </summary>
    public SimulatedServiceMode Mode => (SimulatedServiceMode)Volatile.Read(ref _mode);

    /// <summary>
    /// Sets the mode used by subsequent simulated requests.
    /// </summary>
    /// <param name="mode">The dependency behavior to simulate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The supplied mode is not defined.
    /// </exception>
    /// <remarks>
    /// This development-only control does not emit events. A request retains
    /// the mode observed when it begins; recovery requires a successful refresh.
    /// </remarks>
    public void SetMode(SimulatedServiceMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (!_environment.IsDevelopment())
        {
            return;
        }

        Interlocked.Exchange(ref _mode, (int)mode);
    }

    /// <summary>
    /// Attempts to refresh the cache within a two-second deadline.
    /// </summary>
    /// <remarks>
    /// Refreshes are serialized across sessions so an older request cannot
    /// overwrite a newer result or produce contradictory mode transitions.
    /// Failed refreshes preserve the last successful cache. Host shutdown cancellation
    /// is propagated rather than reported as application degradation.
    /// <para>
    /// Confirmed timeouts emit event 601; explicit unavailable results emit
    /// event 602 without an exception. Each failed attempt has its own identifier.
    /// Event 401 occurs only when entering degraded mode, and event 204 occurs
    /// only after a successful refresh. Shutdown cancellation propagates.
    /// </para>
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// The application is shutting down.
    /// </exception>
    public async Task RefreshAsync()
    {
        CancellationToken stopping = _lifetime.ApplicationStopping;

        await _refreshGate.WaitAsync(stopping);

        try
        {
            string operationId = Guid.NewGuid().ToString("N");
            TimeSpan timeoutBudget = TimeSpan.FromSeconds(2);

            using CancellationTokenSource timeout = new(timeoutBudget);

            using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(stopping, timeout.Token);

            try
            {
                ProductServiceResult result = await SimulatedProductService.FetchAsync(Mode, request.Token);

                stopping.ThrowIfCancellationRequested();

                if (!result.IsAvailable)
                {
                    _logger.External.ServiceUnavailable(
                        externalName: "ProductService",
                        operationName: "FetchProducts",
                        operationId: operationId,
                        reasonCode: "ServiceUnavailable",
                        exception: null);

                    MarkDegraded("Product service reported unavailability");
                    return;
                }

                CatalogSnapshot previous = Snapshot;

                Volatile.Write(
                    ref _snapshot,
                    new CatalogSnapshot(
                        result.Products,
                        DateTimeOffset.UtcNow,
                        IsDegraded: false));

                if (previous.IsDegraded)
                {
                    _logger.Application.InNormalMode();
                }
            }
            catch (OperationCanceledException exception)
                when (timeout.IsCancellationRequested
                      && !stopping.IsCancellationRequested)
            {
                _logger.External.OperationTimedOut(
                    externalName: "ProductService",
                    operationName: "FetchProducts",
                    operationId: operationId,
                    timeoutMilliseconds: timeoutBudget.TotalMilliseconds,
                    exception: exception);

                MarkDegraded("Product service timed out");
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Releases synchronization resources.
    /// </summary>
    public void Dispose()
    {
        _refreshGate.Dispose();
    }



    /// <summary>
    /// Preserves the cache and records degradation on the first failed refresh.
    /// </summary>
    /// <param name="failure">A description of the observed dependency failure.</param>
    /// <remarks>
    /// Called while the refresh gate is held. Changing between failure kinds
    /// does not represent recovery and therefore does not repeat event 401.
    /// </remarks>
    private void MarkDegraded(string failure)
    {
        CatalogSnapshot previous = Snapshot;

        Volatile.Write(
            ref _snapshot,
            previous with { IsDegraded = true });

        if (!previous.IsDegraded)
        {
            _logger.Application.InDegradedMode(
                reason: previous.UpdatedAt.HasValue
                    ? $"{failure}; serving cached prices."
                    : $"{failure}; no cached prices are available.");
        }
    }
}
