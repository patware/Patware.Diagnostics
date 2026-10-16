using System.Globalization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Patware.ApplicationEvents.Hosting.Formatting;

/// <summary>
/// Formats console records with bold, severity-colored event identifiers.
/// </summary>
/// <remarks>
/// ANSI sequences are added only to the console representation.
/// Event identities, templates, and structured values remain unchanged.
/// </remarks>
internal sealed class EventIdConsoleFormatter : ConsoleFormatter
{
    internal const string FormatterName = "patware-event-ids";

    private const string Reset = "\u001b[0m";

    private readonly IOptionsMonitor<EventIdConsoleFormatterOptions> _options;

    /// <summary>
    /// Initializes the formatter.
    /// </summary>
    /// <param name="options">The formatter options monitor.</param>
    public EventIdConsoleFormatter(
        IOptionsMonitor<EventIdConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        if (logEntry.LogLevel == LogLevel.None)
        {
            return;
        }

        string? message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
        {
            return;
        }

        EventIdConsoleFormatterOptions options = _options.CurrentValue;

        if (!string.IsNullOrEmpty(options.TimestampFormat))
        {
            DateTimeOffset timestamp = options.UseUtcTimestamp
                ? DateTimeOffset.UtcNow
                : DateTimeOffset.Now;

            textWriter.Write(timestamp.ToString(options.TimestampFormat, CultureInfo.InvariantCulture));

            textWriter.Write(' ');
        }

        textWriter.Write(GetLevelLabel(logEntry.LogLevel));
        textWriter.Write(": ");
        textWriter.Write(logEntry.Category);

        bool useAnsi = ShouldUseAnsi(options.ColorBehavior);

        if (useAnsi)
        {
            textWriter.Write(GetStyle(logEntry.LogLevel));
        }

        textWriter.Write('[');
        textWriter.Write(logEntry.EventId.Id.ToString(CultureInfo.InvariantCulture));
        textWriter.Write(']');

        if (useAnsi)
        {
            textWriter.Write(Reset);
        }

        textWriter.Write(' ');
        textWriter.Write(message);

        if (options.IncludeScopes && scopeProvider is not null)
        {
            scopeProvider.ForEachScope(
                static (scope, writer) =>
                {
                    writer.Write(" => ");
                    writer.Write(scope);
                },
                textWriter);
        }

        textWriter.WriteLine();

        if (logEntry.Exception is not null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }
    }

    private static bool ShouldUseAnsi(LoggerColorBehavior behavior)
    {
        return behavior switch
        {
            LoggerColorBehavior.Enabled => true,
            LoggerColorBehavior.Disabled => false,
            _ => !Console.IsOutputRedirected
                && !Console.IsErrorRedirected
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
                && !string.Equals(
                    Environment.GetEnvironmentVariable("TERM"),
                    "dumb",
                    StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string GetStyle(LogLevel level)
    {
        return level switch
        {
            LogLevel.Information => "\u001b[1;32m",
            LogLevel.Warning => "\u001b[1;33m",
            LogLevel.Error or LogLevel.Critical => "\u001b[1;31m",
            _ => "\u001b[1m"
        };
    }

    private static string GetLevelLabel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => level.ToString()
        };
    }
}