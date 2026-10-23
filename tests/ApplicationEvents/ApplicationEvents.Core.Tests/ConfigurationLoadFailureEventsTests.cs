using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the configuration-loading failure event contract.
/// </summary>
/// <remarks>
/// These tests protect explicit event emission. Loading, fallback, validation,
/// and application-mode decisions remain responsibilities of the application.
/// </remarks>
[TestClass]
public sealed class ConfigurationLoadFailureEventsTests
{
    /// <summary>
    /// Verifies event identity, severity, structured properties, and exception handling.
    /// </summary>
    /// <param name="hasException">Whether an original exception is supplied.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoadFailed_EmitsExpectedContract(bool hasException)
    {
        // Arrange
        RecordingLogger logger = new();
        Exception? exception = hasException
            ? new IOException("Simulated source read failure.")
            : null;

        // Act
        logger.Configuration.LoadFailed(
            configurationName: "Polling",
            sourceName: "Sample{Settings}",
            loadId: "load-001",
            reasonCode: "SourceReadFailed",
            exception: exception);

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(501, entry.EventId.Id);
        Assert.AreEqual("Configuration_LoadFailed", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Warning, entry.Level);

        if (exception is null)
        {
            Assert.IsNull(entry.Exception);
        }
        else
        {
            Assert.AreSame(exception, entry.Exception);
        }

        Assert.HasCount(5, entry.Properties);
        Assert.AreEqual("Polling", (string)entry.Properties["ConfigurationName"]!);
        Assert.AreEqual("Sample{Settings}", (string)entry.Properties["SourceName"]!);
        Assert.AreEqual("load-001", (string)entry.Properties["LoadId"]!);
        Assert.AreEqual("SourceReadFailed", (string)entry.Properties["ReasonCode"]!);
        Assert.AreEqual(
            "Configuration load failed. Configuration: {ConfigurationName}. " +
            "Source: {SourceName}. Load: {LoadId}. Reason: {ReasonCode}.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies that disabled Warning logging prevents emission.
    /// </summary>
    /// <param name="minimumLevel">The logger's minimum severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Error)]
    [DataRow(LogLevel.None)]
    public void LoadFailed_WhenWarningDisabled_DoesNotEmit(
        LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        logger.Configuration.LoadFailed(
            "Polling",
            "SampleSettings",
            "load-001",
            "SourceNotFound",
            null);

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that nullable exceptions do not bypass event-group validation.
    /// </summary>
    [TestMethod]
    public void LoadFailed_WithDefaultGroup_Throws()
    {
        // Arrange
        ConfigurationEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                events.LoadFailed(
                    "Polling",
                    "SampleSettings",
                    "load-001",
                    "SourceNotFound",
                    null));

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies independent attempts without implicit validation or mode events.
    /// </summary>
    [TestMethod]
    public void LoadFailed_ForSeparateAttempts_EmitsEachAttemptOnly()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Configuration.LoadFailed(
            "Polling", "SampleSettings", "load-001",
            "SourceReadFailed", null);

        logger.Configuration.LoadFailed(
            "Polling", "SampleSettings", "load-002",
            "SourceParseFailed", null);

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(501, logger.Entries[0].EventId.Id);
        Assert.AreEqual(501, logger.Entries[1].EventId.Id);
        Assert.AreEqual("load-001", (string)logger.Entries[0].Properties["LoadId"]!);
        Assert.AreEqual("load-002", (string)logger.Entries[1].Properties["LoadId"]!);
    }
}