namespace SampleApplications.Shared;

/// <summary>
/// Selects the behavior of a simulated dependency request.
/// </summary>
/// <remarks>
/// The caller supplies the mode for each request. Changing application
/// controls afterward does not alter an already pending request.
/// </remarks>
public enum SimulatedServiceMode
{
    /// <summary>
    /// Returns fresh product prices after a short simulated delay.
    /// </summary>
    Available,

    /// <summary>
    /// Waits until the caller cancels the request.
    /// </summary>
    Unresponsive,

    /// <summary>
    /// Immediately throws an explicit unavailable result.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Immediately throws rejection of the simulated application's authentication.
    /// </summary>
    AuthenticationRejected,

    /// <summary>
    /// Immediately throws denial of the simulated application's operation.
    /// </summary>
    AuthorizationDenied
}