using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies external connection beginning and success event contracts.
/// </summary>
/// <remarks>
/// These tests protect explicit event emission. Applications own connection
/// establishment, outcome classification, and correlation.
/// </remarks>
[TestClass]
public sealed class ExternalConnectionEventsTests
{
    /// <summary>
    /// Verifies identity, severity, template, and structured properties.
    /// </summary>
    /// <param name="connected">Whether to emit the success event.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConnectionEvent_EmitsExpectedContract(bool connected)
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        if (connected)
        {
            logger.External.Connected("Product{Feed}", "OpenSession", "connection-001");
        }
        else
        {
            logger.External.Connecting("Product{Feed}", "OpenSession", "connection-001");
        }

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(connected ? 202 : 201, entry.EventId.Id);
        Assert.AreEqual(connected ? "External_Connected" : "External_Connecting", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.IsNull(entry.Exception);
        Assert.HasCount(4, entry.Properties);
        Assert.AreEqual("Product{Feed}", (string)entry.Properties["ExternalName"]!);
        Assert.AreEqual("OpenSession", (string)entry.Properties["OperationName"]!);
        Assert.AreEqual("connection-001", (string)entry.Properties["OperationId"]!);
        Assert.AreEqual(
            connected
                ? "Connected to external dependency {ExternalName}. " +
                  "Operation: {OperationName}. Attempt: {OperationId}."
                : "Connecting to external dependency {ExternalName}. " +
                  "Operation: {OperationName}. Attempt: {OperationId}.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies filtering of both Information events.
    /// </summary>
    /// <param name="minimumLevel">The minimum enabled severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.None)]
    public void ConnectionEvents_WhenInformationDisabled_DoNotEmit(
        LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        logger.External.Connecting("ProductFeed", "OpenSession", "connection-001");
        logger.External.Connected("ProductFeed", "OpenSession", "connection-001");

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that default event groups reject both calls.
    /// </summary>
    /// <param name="connected">Whether to invoke the success event.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConnectionEvent_WithDefaultGroup_Throws(bool connected)
    {
        // Arrange
        ExternalEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                if (connected)
                {
                    events.Connected("ProductFeed", "OpenSession", "connection-001");
                }
                else
                {
                    events.Connecting("ProductFeed", "OpenSession", "connection-001");
                }
            });

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies correlation across interleaved connection attempts.
    /// </summary>
    [TestMethod]
    public void ConnectionEvents_PreserveInterleavedAttemptIdentifiers()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.External.Connecting("ProductFeed", "OpenSession", "connection-001");
        logger.External.Connecting("ProductFeed", "OpenSession", "connection-002");
        logger.External.ServiceUnavailable("ProductFeed", "OpenSession", "connection-002", "ConnectionRefused", null);
        logger.External.Connected("ProductFeed", "OpenSession", "connection-001");

        // Assert
        Assert.HasCount(4, logger.Entries);
        Assert.AreEqual(201, logger.Entries[0].EventId.Id);
        Assert.AreEqual(201, logger.Entries[1].EventId.Id);
        Assert.AreEqual(602, logger.Entries[2].EventId.Id);
        Assert.AreEqual(202, logger.Entries[3].EventId.Id);
        Assert.AreEqual("connection-002", (string)logger.Entries[2].Properties["OperationId"]!);
        Assert.AreEqual("connection-001", (string)logger.Entries[3].Properties["OperationId"]!);
    }

    /// <summary>
    /// Verifies that beginning events do not generate implicit outcomes.
    /// </summary>
    [TestMethod]
    public void Connecting_RepeatedCalls_EmitOnlyRequestedEvents()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.External.Connecting("ProductFeed", "OpenSession", "connection-001");
        logger.External.Connecting("ProductFeed", "OpenSession", "connection-001");

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(201, logger.Entries[0].EventId.Id);
        Assert.AreEqual(201, logger.Entries[1].EventId.Id);
    }
}