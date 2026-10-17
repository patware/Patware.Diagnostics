using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace ApplicationEvents.Hosting.Tests;

/// <summary>
/// Captures records emitted through a host's logging pipeline.
/// </summary>
/// <remarks>
/// A provider captures logs from all categories created by the host.
/// Thread-safe snapshots allow lifecycle callbacks to log from different
/// threads without exposing a mutable collection to the tests.
/// </remarks>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLog> _entries = new();

    internal RecordedLog[] Entries => _entries.ToArray();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(categoryName, _entries);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Keep captured records available for assertions after disposal.
    }

    private sealed class RecordingLogger(string categoryName, ConcurrentQueue<RecordedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, object?> properties =
                state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.ToDictionary(
                        item => item.Key,
                        item => item.Value,
                        StringComparer.Ordinal)
                    : new Dictionary<string, object?>(StringComparer.Ordinal);

            entries.Enqueue(new RecordedLog(
                categoryName,
                logLevel,
                eventId,
                exception,
                properties));
        }
    }
}

/// <summary>
/// Represents a captured record from the host's logging pipeline.
/// </summary>
/// <param name="Category">The logger category.</param>
/// <param name="Level">The severity.</param>
/// <param name="EventId">The numeric and symbolic event identity.</param>
/// <param name="Exception">The associated exception, if any.</param>
/// <param name="Properties">A snapshot of the structured properties.</param>
internal sealed record RecordedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);