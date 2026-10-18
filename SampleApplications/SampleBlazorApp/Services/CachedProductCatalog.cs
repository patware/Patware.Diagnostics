using Patware.Diagnostics.ApplicationEvents;

namespace SampleBlazorApp.Services;

/// <summary>
/// Represents a product value returned by the simulated dependency.
/// </summary>
/// <param name="Name">The product name.</param>
/// <param name="Price">The current simulated price in CAD.</param>
public sealed record ProductPrice(string Name, decimal Price);

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

    private CatalogSnapshot _snapshot = new(
        Array.Empty<ProductPrice>(),
        UpdatedAt: null,
        IsDegraded: false);

    private int _available = 1;

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
    /// Gets whether subsequent dependency calls can succeed.
    /// </summary>
    public bool IsAvailable => Volatile.Read(ref _available) != 0;

    /// <summary>
    /// Sets the simulated dependency's availability.
    /// </summary>
    /// <param name="available">Whether subsequent calls should succeed.</param>
    /// <remarks>
    /// This development-only backdoor does not emit operating-mode events.
    /// Availability alone does not establish that a failed operation recovered.
    /// An in-flight request retains the availability observed when it began.
    /// </remarks>
    public void SetAvailability(bool available)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        Interlocked.Exchange(ref _available, available ? 1 : 0);
    }

    /// <summary>
    /// Attempts to refresh the cache within a two-second deadline.
    /// </summary>
    /// <remarks>
    /// Refreshes are serialized across sessions so an older request cannot
    /// overwrite a newer result or produce contradictory mode transitions.
    /// Timeouts preserve the last successful cache. Host shutdown cancellation
    /// is propagated rather than reported as application degradation.
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
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

            using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(stopping, timeout.Token);

            try
            {
                IReadOnlyList<ProductPrice> products = await FetchProductsAsync(request.Token);

                stopping.ThrowIfCancellationRequested();

                CatalogSnapshot previous = Snapshot;

                Volatile.Write(
                    ref _snapshot,
                    new CatalogSnapshot(
                        products,
                        DateTimeOffset.UtcNow,
                        IsDegraded: false));

                if (previous.IsDegraded)
                {
                    _logger.Application.InNormalMode();
                }
            }
            catch (OperationCanceledException)
                when (timeout.IsCancellationRequested
                      && !stopping.IsCancellationRequested)
            {
                CatalogSnapshot previous = Snapshot;

                Volatile.Write(
                    ref _snapshot,
                    previous with { IsDegraded = true });

                if (!previous.IsDegraded)
                {
                    _logger.Application.InDegradedMode(
                        reason: previous.UpdatedAt.HasValue
                            ? "Product service timed out; serving cached prices."
                            : "Product service timed out; no cached prices are available.");
                }
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
    /// Simulates an asynchronous call to the product service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the simulated request.</param>
    /// <returns>A new, read-only collection of simulated prices.</returns>
    /// <remarks>
    /// An unavailable service waits until the caller cancels its request,
    /// demonstrating an actual timeout rather than an immediate failure.
    /// </remarks>
    private async Task<IReadOnlyList<ProductPrice>> FetchProductsAsync(CancellationToken cancellationToken)
    {
        bool availableAtRequestStart = IsAvailable;

        if (!availableAtRequestStart)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);

        return Array.AsReadOnly(
            new[]
            {
                new ProductPrice("Coffee", NextPrice(12m)),
                new ProductPrice("Tea", NextPrice(8m)),
                new ProductPrice("Chocolate", NextPrice(5m))
            });
    }

    private static decimal NextPrice(decimal minimum)
    {
        return minimum + Random.Shared.Next(0, 501) / 100m;
    }
}