using Microsoft.Extensions.Logging;

namespace Patware.ApplicationEvents.Core.Tests;

/// <summary>
/// Captures logging calls for assertions.
/// </summary>
/// <remarks>
/// Each test owns its logger, allowing tests to run in parallel.
/// Log deliberately records every invocation without filtering so tests
/// can detect whether an emitter respects IsEnabled.
/// </remarks>
internal sealed class RecordingLogger : ILogger
{
    private readonly List<RecordedLog> _entries = [];
    private readonly LogLevel _minimumLevel;

    internal RecordingLogger(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    internal IReadOnlyList<RecordedLog> Entries => _entries;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None
            && _minimumLevel != LogLevel.None
            && logLevel >= _minimumLevel;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> properties)
        {
            throw new InvalidOperationException(
                "Expected a structured logging state.");
        }

        Dictionary<string, object?> snapshot = properties.ToDictionary(
            property => property.Key,
            property => property.Value,
            StringComparer.Ordinal);

        _entries.Add(new RecordedLog(
            logLevel,
            eventId,
            exception,
            snapshot));
    }
}

/// <summary>
/// Represents a captured logging call and its structured properties.
/// </summary>
/// <param name="Level">The severity of the event.</param>
/// <param name="EventId">The numeric and symbolic event identity.</param>
/// <param name="Exception">The exception passed to the logging pipeline.</param>
/// <param name="Properties">A snapshot of the structured logging state.</param>
internal sealed record RecordedLog(
    LogLevel Level,
    EventId EventId,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);