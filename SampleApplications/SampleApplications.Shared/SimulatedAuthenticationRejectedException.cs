namespace SampleApplications.Shared;

/// <summary>
/// Represents an explicit authentication rejection by the simulated dependency.
/// </summary>
/// <remarks>
/// This exception belongs to the sample contract. It is not a required
/// exception type for consumers of Patware.Diagnostics.
/// </remarks>
public sealed class SimulatedAuthenticationRejectedException : Exception
{
    /// <summary>
    /// Initializes the simulated authentication rejection.
    /// </summary>
    public SimulatedAuthenticationRejectedException() : base("The simulated product service rejected authentication.")
    {
    }
}

/// <summary>
/// Represents an explicit authorization denial by the simulated dependency.
/// </summary>
/// <remarks>
/// This exception describes the application's access to its dependency,
/// not authentication or authorization of a UI user.
/// </remarks>
public sealed class SimulatedAuthorizationDeniedException : Exception
{
    /// <summary>
    /// Initializes the simulated authorization denial.
    /// </summary>
    public SimulatedAuthorizationDeniedException() : base("The simulated product service denied permission.")
    {
    }
}

/// <summary>
/// Represents explicit unavailability of the simulated dependency.
/// </summary>
/// <remarks>
/// Applications can classify this exception because its sample contract
/// explicitly establishes unavailability.
/// </remarks>
public sealed class SimulatedServiceUnavailableException : Exception
{
    /// <summary>
    /// Initializes the simulated availability failure.
    /// </summary>
    public SimulatedServiceUnavailableException() : base("The simulated product service is unavailable.")
    {
    }
}