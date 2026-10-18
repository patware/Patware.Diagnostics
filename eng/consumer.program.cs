using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

using Patware.Diagnostics.ApplicationEvents;
using Patware.Diagnostics.ApplicationEvents.Hosting;
using Patware.Diagnostics.ApplicationEvents.Hosting.Formatting;

/// <summary>
/// Exercises the packaged APIs from an external-style console consumer.
/// </summary>
/// <remarks>
/// The generated project references Hosting without a direct Core reference.
/// Successful compilation and execution therefore verify that Hosting
/// exposes the required transitive package dependency.
/// </remarks>
internal static class Program
{
    private const string HostingCategory =
        "Patware.Diagnostics.ApplicationEvents.Hosting";

    /// <summary>
    /// Verifies assembly loading, manual events, host events, and formatting.
    /// </summary>
    public static async Task Main()
    {
        string[] assemblies = __ASSEMBLIES__;

        foreach (string name in assemblies)
        {
            Assembly.Load(new AssemblyName(name));
        }

        await VerifyAsync(formatLogs: false);
        await VerifyAsync(formatLogs: true);

        Console.WriteLine("Diagnostics package smoke test passed.");
    }

    private static async Task VerifyAsync(bool formatLogs)
    {
        HostApplicationBuilder builder = new(
            new HostApplicationBuilderSettings
            {
                DisableDefaults = true,
                EnvironmentName = Environments.Production
            });

        using CaptureProvider capture = new();

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddProvider(capture);

        builder.AddApplicationEvents(options =>
            options.FormatLogs = formatLogs);

        if (formatLogs)
        {
            // Force deterministic ANSI output even when CI redirects stdout.
            builder.Services.Configure<EventIdConsoleFormatterOptions>(
                options =>
                {
                    options.TimestampFormat = null;
                    options.ColorBehavior = LoggerColorBehavior.Enabled;
                });
        }

        using IHost host = builder.Build();

        ILogger manualLogger = host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Manual");

        manualLogger.Application.Starting();
        manualLogger.Application.Started(totalMilliseconds: 12.5);
        manualLogger.Application.Stopping(reason: "Smoke test");
        manualLogger.Application.Stopped();

        AssertEvents(capture, "Manual", 101, 102, 103, 104);

        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(15));

        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);

        AssertEvents(capture, HostingCategory, 101, 102, 103, 104);

        if (formatLogs)
        {
            VerifyFormatter(host);
        }
    }

    private static void AssertEvents(
        CaptureProvider capture,
        string category,
        params int[] expected)
    {
        int[] actual = capture.Entries
            .Where(entry => entry.Category == category)
            .Select(entry => entry.Id)
            .ToArray();

        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                $"Unexpected events for {category}: " +
                string.Join(", ", actual));
        }
    }

    private static void VerifyFormatter(IHost host)
    {
        const string formatterName = "patware-event-ids";

        ConsoleLoggerOptions options = host.Services
            .GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>()
            .CurrentValue;

        if (options.FormatterName != formatterName)
        {
            throw new InvalidOperationException(
                "The Patware console formatter was not selected.");
        }

        ConsoleFormatter formatter = host.Services
            .GetServices<ConsoleFormatter>()
            .Single(candidate => candidate.Name == formatterName);

        using StringWriter writer = new()
        {
            NewLine = "\n"
        };

        LogEntry<string> entry = new(
            LogLevel.Information,
            "Smoke",
            new EventId(101),
            "Message",
            null,
            static (state, _) => state);

        formatter.Write(in entry, null, writer);

        const string expected =
            "info: Smoke\u001b[1;32m[101]\u001b[0m Message\n";

        if (writer.ToString() != expected)
        {
            throw new InvalidOperationException(
                "The packaged formatter produced unexpected output.");
        }
    }

    private sealed class CaptureProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Category, int Id)> _entries =
            new();

        internal (string Category, int Id)[] Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName)
        {
            return new CaptureLogger(categoryName, _entries);
        }

        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(
        string category,
        ConcurrentQueue<(string Category, int Id)> entries) : ILogger
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
            entries.Enqueue((category, eventId.Id));
        }
    }
}