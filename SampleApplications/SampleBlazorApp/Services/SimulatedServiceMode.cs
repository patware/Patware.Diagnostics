namespace SampleBlazorApp.Services;

/// <summary>
/// Selects the outcome of subsequent simulated dependency requests.
/// </summary>
/// <remarks>
/// Changing this setting does not establish application degradation or
/// recovery. Those transitions follow an actual refresh result.
/// </remarks>
public enum SimulatedServiceMode
{
    /// <summary>
    /// Returns fresh values successfully.
    /// </summary>
    Available,

    /// <summary>
    /// Waits until the caller cancels the request at its deadline.
    /// </summary>
    Unresponsive,

    /// <summary>
    /// Immediately returns an explicit unavailable result.
    /// </summary>
    Unavailable
}