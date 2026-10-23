namespace SampleApplications.Shared;

/// <summary>
/// Simulates a product dependency for the sample applications.
/// </summary>
/// <remarks>
/// This service owns no cache, operating mode, logger, or timeout policy.
/// Callers select request behavior and supply cancellation. This keeps
/// hosting concerns and diagnostic event emission visible in each sample.
/// </remarks>
public static class SimulatedProductService
{
    /// <summary>
    /// Fetches simulated product prices using the requested behavior.
    /// </summary>
    /// <param name="mode">The behavior of this individual request.</param>
    /// <param name="cancellationToken">
    /// Cancellation supplied by the caller, including its timeout or shutdown.
    /// </param>
    /// <returns>
    /// A successful response containing Coffee, Tea, and Chocolate prices,
    /// or an explicit unavailable result.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The mode is not defined.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation was requested.
    /// </exception>
    /// <remarks>
    /// Unresponsive mode waits asynchronously until cancellation. The caller
    /// determines whether cancellation represents timeout or shutdown.
    /// Unavailable mode returns normally without manufacturing an exception.
    /// This method does not emit diagnostic events.
    /// </remarks>
    public static async Task<ProductServiceResult> FetchAsync(SimulatedServiceMode mode, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (mode == SimulatedServiceMode.Unavailable)
        {
            return ProductServiceResult.Unavailable;
        }

        if (mode == SimulatedServiceMode.Unresponsive)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);

        return ProductServiceResult.Success(
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