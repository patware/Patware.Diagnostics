using System.Text.Json;

using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace SampleConsole.Configuration;

/// <summary>
/// Demonstrates configuration loading, validation, and retention of working settings.
/// </summary>
/// <remarks>
/// The console calls this class sequentially, so no synchronization is required.
/// A reload reads a separate sample file after host construction, allowing the
/// existing logger to report failures. It does not alter the host's own
/// configuration providers or depend on automatic reload behavior.
/// </remarks>
internal sealed class ReloadablePollingConfiguration
{
    private const string ConfigurationName = "KeyboardPolling";
    private const string SourceName = "SamplePollingFile";

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the reload demonstration with a valid built-in fallback.
    /// </summary>
    /// <param name="logger">The logger receiving configuration events.</param>
    /// <param name="sourcePath">The file read by subsequent reload attempts.</param>
    internal ReloadablePollingConfiguration(ILogger logger, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        _logger = logger;
        SourcePath = Path.GetFullPath(sourcePath);
    }

    /// <summary>
    /// Gets the absolute sample-file path displayed to the operator.
    /// </summary>
    internal string SourcePath { get; }

    /// <summary>
    /// Gets the last accepted settings, initially the built-in fallback.
    /// </summary>
    internal PollingSettings Current { get; private set; } = new(100);

    /// <summary>
    /// Attempts to read, validate, and apply a new settings snapshot.
    /// </summary>
    /// <returns>
    /// True when a candidate was accepted and applied; otherwise false.
    /// </returns>
    /// <remarks>
    /// Read and deserialization failures produce 501 before validation begins.
    /// A deserialized candidate outside the supported range produces 506/507.
    /// Neither failure replaces Current or automatically changes application mode.
    /// The original exceptions are logged here because this demonstration file
    /// contains only a non-sensitive polling interval.
    /// <para>
    /// Expected loading failures emit a concise Warning event. The original
    /// exception is logged separately at Debug for diagnosis using the same
    /// load identifier. Exception details must be appropriate for the destination.
    /// </para>
    /// </remarks>
    internal bool TryReload()
    {
        string loadId = Guid.NewGuid().ToString("N");
        PollingSettings candidate;

        _logger.Configuration.Loading(ConfigurationName, SourceName, loadId);

        try
        {
            string json = File.ReadAllText(SourcePath);

            candidate = JsonSerializer.Deserialize<PollingSettings>(json)
                ?? throw new JsonException(
                    "A configuration object was expected.");
        }
        catch (Exception exception)
            when (exception is IOException
                  or UnauthorizedAccessException
                  or JsonException)
        {
            string reasonCode = exception switch
            {
                FileNotFoundException or DirectoryNotFoundException => "SourceNotFound",

                UnauthorizedAccessException => "SourceAccessDenied",

                JsonException => "SourceParseFailed",

                _ => "SourceReadFailed"
            };

            _logger.Configuration.LoadFailed(
                configurationName: ConfigurationName,
                sourceName: SourceName,
                loadId: loadId,
                reasonCode: reasonCode,
                exception: null);

            _logger.LogDebug(
                exception,
                "Configuration loading diagnostics. Configuration: {ConfigurationName}. " +
                "Source: {SourceName}. Load: {LoadId}.",
                ConfigurationName,
                SourceName,
                loadId);

            return false;
        }

        _logger.Configuration.Loaded(ConfigurationName, SourceName, loadId);

        string validationId = Guid.NewGuid().ToString("N");

        _logger.Configuration.Validating(ConfigurationName, validationId);

        if (candidate.PollingIntervalMilliseconds is < 50 or > 1000)
        {
            const string reason = "PollingIntervalMilliseconds must be between 50 and 1000";

            _logger.Configuration.ValueIsInvalid(
                ConfigurationName,
                validationId,
                settingName: nameof(PollingSettings.PollingIntervalMilliseconds),
                reason: reason);

            _logger.Configuration.ValidationFailed(
                ConfigurationName,
                validationId,
                reason: reason);

            return false;
        }

        _logger.Configuration.ValueIsValid(ConfigurationName, validationId, settingName: nameof(PollingSettings.PollingIntervalMilliseconds));

        _logger.Configuration.Validated(ConfigurationName, validationId);

        Current = candidate;

        _logger.LogInformation("Accepted polling configuration is now active.");

        return true;
    }
}