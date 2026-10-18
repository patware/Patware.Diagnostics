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

    /// <summary>
    /// Gets or sets whether runtime unhandled-exception notifications
    /// are observed. The default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While the observer is active, terminating notifications carrying an
    /// Exception are reported through event 701. Reporting is best effort;
    /// buffered providers may not flush before process termination.
    /// </para>
    /// <para>
    /// This option does not capture exceptions handled by the application,
    /// the Generic Host, or a UI framework. It does not change exception
    /// handling or shutdown policy.
    /// </para>
    /// <para>
    /// The subscription is process-wide. Enable it on one host per process
    /// to avoid duplicate reporting.
    /// </para>
    /// </remarks>
    public bool LogUnhandledExceptions { get; set; }
}