namespace SampleApplications.Shared;

/// <summary>
/// Identifies the result of a completed simulated dependency request.
/// </summary>
/// <remarks>
/// Timeout and shutdown cancellation propagate as exceptions to the caller.
/// They are not completed dependency results.
/// </remarks>
public enum ProductServiceStatus
{
    /// <summary>
    /// The request succeeded, possibly returning no products.
    /// </summary>
    Success,

    /// <summary>
    /// The dependency reported that it could not serve the request.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The dependency rejected the simulated application's authentication.
    /// </summary>
    AuthenticationRejected,

    /// <summary>
    /// The dependency denied the simulated application's requested operation.
    /// </summary>
    AuthorizationDenied
}