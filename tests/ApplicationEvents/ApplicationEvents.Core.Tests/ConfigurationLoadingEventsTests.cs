using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies configuration-loading beginning and success event contracts.
/// </summary>
/// <remarks>
/// These tests protect explicit emission and correlation.
/// Loading, validation, and application of settings remain caller responsibilities.
/// </remarks>
[TestClass]
public sealed class ConfigurationLoadingEventsTests
{
    /// <summary>
    /// Verifies identity, severity, and structured properties.
    /// </summary>
    /// <param name="loaded">Whether to emit the success event.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoadingEvent_EmitsExpectedContract(bool loaded)
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        if (loaded)
        {
            logger.Configuration.Loaded("Polling", "Sample{Settings}", "load-001");
        }
        else
        {
            logger.Configuration.Loading("Polling", "Sample{Settings}", "load-001");
        }

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(loaded ? 120 : 109, entry.EventId.Id);
        Assert.AreEqual(loaded ? "Configuration_Loaded" : "Configuration_Loading", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.IsNull(entry.Exception);
        Assert.HasCount(4, entry.Properties);
        Assert.AreEqual("Polling", (string)entry.Properties["ConfigurationName"]!);
        Assert.AreEqual("Sample{Settings}", (string)entry.Properties["SourceName"]!);
        Assert.AreEqual("load-001", (string)entry.Properties["LoadId"]!);
        Assert.AreEqual(
            loaded
                ? "Configuration loaded. Configuration: {ConfigurationName}. " +
                  "Source: {SourceName}. Load: {LoadId}."
                : "Loading configuration. Configuration: {ConfigurationName}. " +
                  "Source: {SourceName}. Load: {LoadId}.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies filtering of both Information events.
    /// </summary>
    /// <param name="minimumLevel">The minimum enabled severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.None)]
    public void LoadingEvents_WhenInformationDisabled_DoNotEmit(LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        logger.Configuration.Loading("Polling", "SampleSettings", "load-001");
        logger.Configuration.Loaded("Polling", "SampleSettings", "load-001");

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that default groups reject both event calls.
    /// </summary>
    /// <param name="loaded">Whether to invoke the success event.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoadingEvent_WithDefaultGroup_Throws(bool loaded)
    {
        // Arrange
        ConfigurationEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                if (loaded)
                {
                    events.Loaded("Polling", "SampleSettings", "load-001");
                }
                else
                {
                    events.Loading("Polling", "SampleSettings", "load-001");
                }
            });

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies that interleaved attempts retain their own outcome identifiers.
    /// </summary>
    [TestMethod]
    public void LoadingEvents_PreserveInterleavedAttemptIdentifiers()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Configuration.Loading("Polling", "SampleSettings", "load-001");
        logger.Configuration.Loading("Polling", "SampleSettings", "load-002");
        logger.Configuration.LoadFailed("Polling", "SampleSettings", "load-002", "SourceParseFailed", null);
        logger.Configuration.Loaded("Polling", "SampleSettings", "load-001");

        // Assert
        Assert.HasCount(4, logger.Entries);
        Assert.AreEqual(109, logger.Entries[0].EventId.Id);
        Assert.AreEqual(109, logger.Entries[1].EventId.Id);
        Assert.AreEqual(501, logger.Entries[2].EventId.Id);
        Assert.AreEqual(120, logger.Entries[3].EventId.Id);
        Assert.AreEqual("load-002", (string)logger.Entries[2].Properties["LoadId"]!);
        Assert.AreEqual("load-001", (string)logger.Entries[3].Properties["LoadId"]!);
    }

    /// <summary>
    /// Verifies that beginning records do not infer outcomes or deduplicate calls.
    /// </summary>
    [TestMethod]
    public void Loading_RepeatedCalls_EmitOnlyRequestedEvents()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Configuration.Loading("Polling", "SampleSettings", "load-001");
        logger.Configuration.Loading("Polling", "SampleSettings", "load-001");

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(109, logger.Entries[0].EventId.Id);
        Assert.AreEqual(109, logger.Entries[1].EventId.Id);
    }
}