namespace Patware.Diagnostics.ApplicationEvents.Hosting;

/// <summary>
/// Configures application event registration.
/// </summary>
/// <remarks>
/// These options are evaluated during registration. They are not
/// dynamically reloaded after the application has been configured.
/// </remarks>
public sealed class ApplicationEventsOptions
{
    /// <summary>
    /// Gets or sets whether the Patware console formatter is selected.
    /// The default is false.
    /// </summary>
    /// <remarks>
    /// When enabled, console event identifiers are bold and colored
    /// according to log level. Formatting applies to all console records,
    /// including framework logs. Other logging providers are unaffected.
    /// </remarks>
    public bool FormatLogs { get; set; }
}