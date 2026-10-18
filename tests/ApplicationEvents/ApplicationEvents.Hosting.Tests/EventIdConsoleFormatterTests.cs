using System.Globalization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

using Patware.Diagnostics.ApplicationEvents.Hosting;
using Patware.Diagnostics.ApplicationEvents.Hosting.Formatting;

namespace ApplicationEvents.Hosting.Tests;

/// <summary>
/// Verifies console rendering and formatter registration.
/// </summary>
/// <remarks>
/// The formatter is resolved through its public base class so tests
/// exercise public registration without exposing implementation types.
/// Each test owns its host and writer, allowing parallel execution
/// without redirecting console output or changing environment variables.
/// </remarks>
[TestClass]
public sealed class EventIdConsoleFormatterTests
{
    private const string FormatterName = "patware-event-ids";
    private const string Category = "SampleApplication";
    private const string Message = "Application is starting.";

    /// <summary>
    /// Verifies that only the event identifier receives ANSI styling.
    /// </summary>
    /// <param name="level">The record severity.</param>
    /// <param name="label">The expected severity label.</param>
    /// <param name="style">The expected bold and color sequence.</param>
    /// <remarks>
    /// Comparing the complete output detects missing resets and styling
    /// that accidentally extends into the category or message.
    /// Explicitly enabling colors avoids dependence on the test runner's
    /// terminal capabilities.
    /// </remarks>
    [TestMethod]
    [DataRow(LogLevel.Trace, "trce", "\u001b[1m")]
    [DataRow(LogLevel.Debug, "dbug", "\u001b[1m")]
    [DataRow(LogLevel.Information, "info", "\u001b[1;32m")]
    [DataRow(LogLevel.Warning, "warn", "\u001b[1;33m")]
    [DataRow(LogLevel.Error, "fail", "\u001b[1;31m")]
    [DataRow(LogLevel.Critical, "crit", "\u001b[1;31m")]
    public void Write_WithColorsEnabled_StylesOnlyEventId(
        LogLevel level,
        string label,
        string style)
    {
        // Arrange
        using IHost host = CreateHost(options => options.ColorBehavior = LoggerColorBehavior.Enabled);

        ConsoleFormatter formatter = GetFormatter(host);

        // Act
        string output = Render(formatter, level);

        // Assert
        Assert.AreEqual(
            $"{label}: {Category}{style}[101]\u001b[0m {Message}\n",
            output);
    }

    /// <summary>
    /// Verifies that disabling colors produces plain text.
    /// </summary>
    [TestMethod]
    public void Write_WithColorsDisabled_ContainsNoAnsiSequences()
    {
        // Arrange
        using IHost host = CreateHost();
        ConsoleFormatter formatter = GetFormatter(host);

        // Act
        string output = Render(formatter, LogLevel.Information);

        // Assert
        Assert.AreEqual(
            $"info: {Category}[101] {Message}\n",
            output);

        Assert.IsFalse(output.Contains('\u001b'));
    }

    /// <summary>
    /// Verifies that message and exception text are preserved.
    /// </summary>
    /// <param name="message">The formatted message.</param>
    /// <remarks>
    /// An exception-only record must remain visible even when its
    /// formatted message is empty.
    /// </remarks>
    [TestMethod]
    [DataRow("Operation failed.")]
    [DataRow("")]
    public void Write_WithException_PreservesMessageAndException(
        string message)
    {
        // Arrange
        using IHost host = CreateHost();
        ConsoleFormatter formatter = GetFormatter(host);
        InvalidOperationException exception = new("Test failure.");

        // Act
        string output = Render(
            formatter,
            LogLevel.Error,
            message,
            exception);

        // Assert
        Assert.AreEqual(
            $"fail: {Category}[101] {message}\n{exception}\n",
            output);
    }

    /// <summary>
    /// Verifies that scope output follows the configured option.
    /// </summary>
    /// <param name="includeScopes">Whether scopes should be rendered.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Write_WithScopes_RespectsIncludeScopes(bool includeScopes)
    {
        // Arrange
        using IHost host = CreateHost(options => options.IncludeScopes = includeScopes);

        ConsoleFormatter formatter = GetFormatter(host);
        LoggerExternalScopeProvider scopes = new();

        using IDisposable outer = scopes.Push("Outer");
        using IDisposable inner = scopes.Push("Inner");

        // Act
        string output = Render(
            formatter,
            LogLevel.Information,
            scopeProvider: scopes);

        // Assert
        string suffix = includeScopes
            ? " => Outer => Inner"
            : string.Empty;

        Assert.AreEqual(
            $"info: {Category}[101] {Message}{suffix}\n",
            output);
    }

    /// <summary>
    /// Verifies that the configured timestamp format is applied.
    /// </summary>
    /// <remarks>
    /// A literal timestamp format verifies formatting and placement
    /// without a wall-clock timing assertion that could become flaky.
    /// This test does not verify UTC versus local time selection.
    /// </remarks>
    [TestMethod]
    public void Write_WithTimestampFormat_PrefixesFormattedTimestamp()
    {
        // Arrange
        using IHost host = CreateHost(options => options.TimestampFormat = "'timestamp'");

        ConsoleFormatter formatter = GetFormatter(host);

        // Act
        string output = Render(formatter, LogLevel.Information);

        // Assert
        Assert.AreEqual(
            $"timestamp info: {Category}[101] {Message}\n",
            output);
    }

    /// <summary>
    /// Verifies that suppressed or empty records produce no output.
    /// </summary>
    /// <param name="level">The record severity.</param>
    /// <param name="message">The formatted message.</param>
    [TestMethod]
    [DataRow(LogLevel.None, "Ignored message")]
    [DataRow(LogLevel.Information, "")]
    public void Write_WithSuppressedRecord_ProducesNoOutput(LogLevel level, string message)
    {
        // Arrange
        using IHost host = CreateHost();
        ConsoleFormatter formatter = GetFormatter(host);

        // Act
        string output = Render(formatter, level, message);

        // Assert
        Assert.AreEqual(string.Empty, output);
    }

    /// <summary>
    /// Verifies formatter selection and preservation of existing logging
    /// providers and minimum severity.
    /// </summary>
    /// <param name="formatLogs">Whether Patware formatting is enabled.</param>
    /// <remarks>
    /// Starting with an explicitly selected Simple formatter proves that
    /// disabling the feature preserves the application's selection.
    /// The recording provider is the existing test helper.
    /// </remarks>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Registration_PreservesProvidersAndFilters(bool formatLogs)
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder();
        using RecordingLoggerProvider recordingProvider = new();

        builder.Logging.AddSimpleConsole();
        builder.Logging.AddProvider(recordingProvider);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Act
        builder.AddApplicationEvents(options => options.FormatLogs = formatLogs);

        using IHost host = builder.Build();

        ConsoleLoggerOptions consoleOptions =
            host.Services
                .GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>()
                .CurrentValue;

        ILoggerProvider[] providers =
            host.Services.GetServices<ILoggerProvider>().ToArray();

        ConsoleFormatter[] formatters =
            host.Services.GetServices<ConsoleFormatter>().ToArray();

        ILogger logger =
            host.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger(Category);

        // Assert
        Assert.AreEqual(
            formatLogs ? FormatterName : ConsoleFormatterNames.Simple,
            consoleOptions.FormatterName);

        Assert.IsTrue(providers.Any(provider =>
            ReferenceEquals(provider, recordingProvider)));

        Assert.AreEqual(
            1,
            providers.OfType<ConsoleLoggerProvider>().Count());

        Assert.AreEqual(
            formatLogs ? 1 : 0,
            formatters.Count(formatter => formatter.Name == FormatterName));

        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Warning));
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        return new HostApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                DisableDefaults = true,
                EnvironmentName = Environments.Production
            });
    }

    private static IHost CreateHost(Action<EventIdConsoleFormatterOptions>? configure = null)
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddApplicationEvents(options =>
            options.FormatLogs = true);

        builder.Services.Configure<EventIdConsoleFormatterOptions>(
            options =>
            {
                options.TimestampFormat = null;
                options.ColorBehavior = LoggerColorBehavior.Disabled;
                configure?.Invoke(options);
            });

        return builder.Build();
    }

    private static ConsoleFormatter GetFormatter(IHost host)
    {
        return host.Services.GetServices<ConsoleFormatter>()
            .Single(formatter => formatter.Name == FormatterName);
    }

    private static string Render(
        ConsoleFormatter formatter,
        LogLevel level,
        string message = Message,
        Exception? exception = null,
        IExternalScopeProvider? scopeProvider = null)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture)
        {
            NewLine = "\n"
        };

        LogEntry<string> entry = new(
            level,
            Category,
            new EventId(101, "Application_Starting"),
            message,
            exception,
            static (state, _) => state);

        formatter.Write(in entry, scopeProvider, writer);

        return writer.ToString();
    }
}