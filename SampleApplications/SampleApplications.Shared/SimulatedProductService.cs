namespace SampleApplications.Shared;

/// <summary>
/// Simulates a product dependency for the sample applications.
/// </summary>
/// <remarks>
/// The service returns domain data and throws for simulated failures.
/// It owns no logger, cache, timeout policy, or application operating mode.
/// </remarks>
public static class SimulatedProductService
{
    /// <summary>
    /// Fetches prices using the behavior selected for this attempt.
    /// </summary>
    /// <param name="mode">The behavior captured when the request begins.</param>
    /// <param name="cancellationToken">
    /// Caller cancellation, including timeout and application shutdown.
    /// </param>
    /// <returns>A read-only collection of product prices.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The requested mode is not defined.
    /// </exception>
    /// <exception cref="SimulatedAuthenticationRejectedException">
    /// The selected mode rejects authentication.
    /// </exception>
    /// <exception cref="SimulatedAuthorizationDeniedException">
    /// The selected mode denies permission.
    /// </exception>
    /// <exception cref="SimulatedServiceUnavailableException">
    /// The selected mode reports unavailability.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation was requested.
    /// </exception>
    /// <remarks>
    /// Unresponsive mode waits for cancellation rather than implementing
    /// its own deadline. Changing sample controls does not alter this
    /// attempt's captured mode.
    /// </remarks>
    public static async Task<IReadOnlyList<ProductPrice>> FetchAsync(SimulatedServiceMode mode, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        cancellationToken.ThrowIfCancellationRequested();

        switch (mode)
        {
            case SimulatedServiceMode.AuthenticationRejected:
                throw new SimulatedAuthenticationRejectedException();

            case SimulatedServiceMode.AuthorizationDenied:
                throw new SimulatedAuthorizationDeniedException();

            case SimulatedServiceMode.Unavailable:
                throw new SimulatedServiceUnavailableException();

            case SimulatedServiceMode.Unresponsive:
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                break;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);

        return Array.AsReadOnly(
            new ProductPrice[]
            {
                new("Coffee", NextPrice(12m)),
                new("Tea", NextPrice(8m)),
                new("Chocolate", NextPrice(5m))
            });
    }

    private static decimal NextPrice(decimal minimum)
    {
        return minimum + Random.Shared.Next(0, 501) / 100m;
    }
}