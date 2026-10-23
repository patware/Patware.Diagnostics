using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the external service unavailability event contract.
/// </summary>
/// <remarks>
/// Unavailability can be returned as a normal result or represented by an
/// exception. Both paths must preserve the same structured event contract.
/// Existing timeout and fatal-event tests retain coverage for required
/// exception arguments.
/// </remarks>
[TestClass]
public sealed class ExternalServiceUnavailableEventsTests
{
    /// <summary>
    /// Verifies identity, properties, and the nullable exception channel.
    /// </summary>
    /// <param name="hasException">
    /// Whether an original exception accompanies the unavailable result.
    /// </param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ServiceUnavailable_EmitsExpectedContract(bool hasException)
    {
        // Arrange
        RecordingLogger logger = new();
        Exception? exception = hasException
            ? new InvalidOperationException(
                "Simulated dependency failure.",
                new Exception("Original underlying failure."))
            : null;

        // Act
        logger.External.ServiceUnavailable(
            externalName: "ProductService",
            operationName: "Fetch{Prices}",
            operationId: "attempt-001",
            reasonCode: "ServiceUnavailable",
            exception: exception);

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(602, entry.EventId.Id);
        Assert.AreEqual("External_ServiceUnavailable", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Error, entry.Level);

        if (exception is null)
        {
            Assert.IsNull(entry.Exception);
        }
        else
        {
            Assert.AreSame(exception, entry.Exception);
        }

        Assert.HasCount(5, entry.Properties);
        Assert.AreEqual("ProductService", (string)entry.Properties["ExternalName"]!);
        Assert.AreEqual("Fetch{Prices}", (string)entry.Properties["OperationName"]!);
        Assert.AreEqual("attempt-001", (string)entry.Properties["OperationId"]!);
        Assert.AreEqual("ServiceUnavailable", (string)entry.Properties["ReasonCode"]!);
        Assert.AreEqual(
            "External service unavailable. Dependency: {ExternalName}. " +
            "Operation: {OperationName}. Attempt: {OperationId}. " +
            "Reason: {ReasonCode}.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies filtering with and without an accompanying exception.
    /// </summary>
    /// <param name="minimumLevel">The logger's minimum severity.</param>
    /// <param name="hasException">Whether an exception is supplied.</param>
    [TestMethod]
    [DataRow(LogLevel.Critical, false)]
    [DataRow(LogLevel.Critical, true)]
    [DataRow(LogLevel.None, false)]
    [DataRow(LogLevel.None, true)]
    public void ServiceUnavailable_WhenErrorDisabled_DoesNotEmit(
        LogLevel minimumLevel,
        bool hasException)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);
        Exception? exception = hasException
            ? new InvalidOperationException("Dependency unavailable.")
            : null;

        // Act
        logger.External.ServiceUnavailable(
            "ProductService",
            "FetchPrices",
            "attempt-001",
            "ServiceUnavailable",
            exception);

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that nullable exceptions do not bypass the event-group guard.
    /// </summary>
    [TestMethod]
    public void ServiceUnavailable_WithDefaultGroup_Throws()
    {
        // Arrange
        ExternalEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                events.ServiceUnavailable(
                    "ProductService",
                    "FetchPrices",
                    "attempt-001",
                    "ServiceUnavailable",
                    null));

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies independent attempts without implicit application mode events.
    /// </summary>
    [TestMethod]
    public void ServiceUnavailable_ForSeparateAttempts_EmitsEachAttemptOnly()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.External.ServiceUnavailable("ProductService", "FetchPrices", "attempt-001", "ServiceUnavailable", null);

        logger.External.ServiceUnavailable("ProductService", "FetchPrices", "attempt-002", "ServiceUnavailable", null);

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(602, logger.Entries[0].EventId.Id);
        Assert.AreEqual(602, logger.Entries[1].EventId.Id);
        Assert.AreEqual("attempt-001", (string)logger.Entries[0].Properties["OperationId"]!);
        Assert.AreEqual("attempt-002", (string)logger.Entries[1].Properties["OperationId"]!);
    }
}