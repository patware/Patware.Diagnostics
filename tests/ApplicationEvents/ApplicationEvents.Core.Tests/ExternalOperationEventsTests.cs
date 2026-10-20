using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the contract of external operation timeout events.
/// </summary>
/// <remarks>
/// These tests verify emitted records and API guards. The caller remains
/// responsible for distinguishing timeouts from other cancellation causes.
/// </remarks>
[TestClass]
public sealed class ExternalOperationEventsTests
{
    /// <summary>
    /// Verifies identity, severity, structured properties, and exception identity.
    /// </summary>
    [TestMethod]
    public void OperationTimedOut_EmitsExpectedContract()
    {
        // Arrange
        RecordingLogger logger = new();
        OperationCanceledException exception = new("Request deadline expired.");

        // Act
        logger.External.OperationTimedOut(
            externalName: "ProductService",
            operationName: "Fetch{Prices}",
            operationId: "attempt-001",
            timeoutMilliseconds: 2000.5,
            exception: exception);

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(601, entry.EventId.Id);
        Assert.AreEqual("External_OperationTimedOut", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Error, entry.Level);
        Assert.AreSame(exception, entry.Exception);

        Assert.HasCount(5, entry.Properties);
        Assert.AreEqual("ProductService", (string)entry.Properties["ExternalName"]!);
        Assert.AreEqual("Fetch{Prices}", (string)entry.Properties["OperationName"]!);
        Assert.AreEqual("attempt-001", (string)entry.Properties["OperationId"]!);
        Assert.AreEqual(2000.5, (double)entry.Properties["TimeoutMilliseconds"]!);
        Assert.AreEqual(
            "External operation timed out. Dependency: {ExternalName}. " +
            "Operation: {OperationName}. Attempt: {OperationId}. " +
            "Timeout: {TimeoutMilliseconds} milliseconds.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies that filtering prevents emission when Error is disabled.
    /// </summary>
    /// <param name="minimumLevel">The logger's minimum severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Critical)]
    [DataRow(LogLevel.None)]
    public void OperationTimedOut_WhenErrorDisabled_DoesNotEmit(
        LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);
        TimeoutException exception = new("Request deadline expired.");

        // Act
        logger.External.OperationTimedOut(
            "ProductService",
            "FetchPrices",
            "attempt-001",
            2000,
            exception);

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that exception validation is independent of logging filters.
    /// </summary>
    /// <param name="minimumLevel">The logger's minimum severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Trace)]
    [DataRow(LogLevel.None)]
    public void OperationTimedOut_WithNullException_Throws(
        LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        ArgumentNullException exception =
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                logger.External.OperationTimedOut(
                    "ProductService",
                    "FetchPrices",
                    "attempt-001",
                    2000,
                    null!));

        // Assert
        Assert.AreEqual("exception", exception.ParamName);
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that category access rejects a null logger.
    /// </summary>
    [TestMethod]
    public void External_WithNullLogger_Throws()
    {
        // Arrange
        ILogger logger = null!;

        // Act
        ArgumentNullException exception =
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                _ = logger.External;
            });

        // Assert
        Assert.AreEqual("logger", exception.ParamName);
    }

    /// <summary>
    /// Verifies that a default-initialized event group cannot emit.
    /// </summary>
    [TestMethod]
    public void OperationTimedOut_WithDefaultGroup_Throws()
    {
        // Arrange
        ExternalEvents events = default;
        TimeoutException exception = new("Request deadline expired.");

        // Act
        InvalidOperationException failure =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                events.OperationTimedOut(
                    "ProductService",
                    "FetchPrices",
                    "attempt-001",
                    2000,
                    exception));

        // Assert
        Assert.IsNotNull(failure);
    }

    /// <summary>
    /// Verifies that separate attempts remain distinct without mode events.
    /// </summary>
    /// <remarks>
    /// Reporting an operation failure must not implicitly change application
    /// mode or suppress subsequent failed attempts.
    /// </remarks>
    [TestMethod]
    public void OperationTimedOut_ForSeparateAttempts_EmitsEachAttemptOnly()
    {
        // Arrange
        RecordingLogger logger = new();
        TimeoutException first = new("First attempt timed out.");
        TimeoutException second = new("Second attempt timed out.");

        // Act
        logger.External.OperationTimedOut(
            "ProductService", "FetchPrices", "attempt-001", 2000, first);

        logger.External.OperationTimedOut(
            "ProductService", "FetchPrices", "attempt-002", 2000, second);

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(601, logger.Entries[0].EventId.Id);
        Assert.AreEqual(601, logger.Entries[1].EventId.Id);
        Assert.AreEqual("attempt-001", (string)logger.Entries[0].Properties["OperationId"]!);
        Assert.AreEqual("attempt-002", (string)logger.Entries[1].Properties["OperationId"]!);
        Assert.AreSame(first, logger.Entries[0].Exception);
        Assert.AreSame(second, logger.Entries[1].Exception);
    }
}