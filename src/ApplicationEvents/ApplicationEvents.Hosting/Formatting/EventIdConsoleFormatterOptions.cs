using Microsoft.Extensions.Logging.Console;

namespace Patware.ApplicationEvents.Hosting.Formatting;

/// <summary>
/// Configures the Patware event identifier console formatter.
/// </summary>
public sealed class EventIdConsoleFormatterOptions : ConsoleFormatterOptions
{
    /// <summary>
    /// Initializes the formatter options with a millisecond timestamp.
    /// </summary>
    public EventIdConsoleFormatterOptions()
    {
        TimestampFormat = "HH:mm:ss.fff";
    }

    /// <summary>
    /// Gets or sets whether ANSI styling is enabled.
    /// The default uses automatic detection.
    /// </summary>
    /// <remarks>
    /// Automatic detection disables styling when either console stream
    /// is redirected, NO_COLOR is set to a nonempty value, or TERM is dumb.
    /// Enabled explicitly requests ANSI output; Disabled produces plain text.
    /// </remarks>
    public LoggerColorBehavior ColorBehavior { get; set; } = LoggerColorBehavior.Default;
}