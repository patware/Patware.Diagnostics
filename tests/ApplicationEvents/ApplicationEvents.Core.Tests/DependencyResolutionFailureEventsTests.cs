using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the dependency resolution failure event contract.
/// </summary>
/// <remarks>
/// Resolution, fallback selection, and application-mode decisions remain
/// responsibilities of the application.
/// </remarks>
[TestClass]
public sealed class DependencyResolutionFailureEventsTests
{
    /// <summary>
    /// Verifies event identity, severity, properties, and exception preservation.
    /// </summary>
    /// <param name="hasException">
    /// Whether the caller supplies an original exception.
    /// </param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResolutionFailed_EmitsExpectedContract(bool hasException)
    {
        // Arrange
        RecordingLogger logger = new();
        Exception? exception = hasException
            ? new InvalidOperationException("Simulated construction failure.")
            : null;

        // Act
        logger.Dependency.ResolutionFailed(
            dependencyName: "Report{Formatter}",
            operationName: "GenerateReport",
            resolutionId: "resolution-001",
            reasonCode: "ConstructionFailed",
            exception: exception);

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(504, entry.EventId.Id);
        Assert.AreEqual("Dependency_ResolutionFailed", entry.EventId.Name);
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
        Assert.AreEqual("Report{Formatter}", (string)entry.Properties["DependencyName"]!);
        Assert.AreEqual("GenerateReport", (string)entry.Properties["OperationName"]!);
        Assert.AreEqual("resolution-001", (string)entry.Properties["ResolutionId"]!);
        Assert.AreEqual("ConstructionFailed", (string)entry.Properties["ReasonCode"]!);
        Assert.AreEqual(
            "Dependency resolution failed. Dependency: {DependencyName}. " +
            "Operation: {OperationName}. Resolution: {ResolutionId}. " +
            "Reason: {ReasonCode}.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies that disabled Warning logging prevents emission.
    /// </summary>
    /// <param name="minimumLevel">The minimum enabled severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Error)]
    [DataRow(LogLevel.None)]
    public void ResolutionFailed_WhenWarningDisabled_DoesNotEmit(LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        logger.Dependency.ResolutionFailed(
            "ReportFormatter",
            "GenerateReport",
            "resolution-001",
            "NotFound",
            null);

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that default event groups reject emission.
    /// </summary>
    [TestMethod]
    public void ResolutionFailed_WithDefaultGroup_Throws()
    {
        // Arrange
        DependencyEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                events.ResolutionFailed(
                    "ReportFormatter",
                    "GenerateReport",
                    "resolution-001",
                    "NotFound",
                    null));

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies independent attempts without implicit application-mode events.
    /// </summary>
    [TestMethod]
    public void ResolutionFailed_ForSeparateAttempts_EmitsEachAttemptOnly()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Dependency.ResolutionFailed("ReportFormatter", "GenerateReport", "resolution-001", "NotFound", null);

        logger.Dependency.ResolutionFailed("ReportFormatter", "GenerateReport", "resolution-002", "LoadFailed", null);

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(504, logger.Entries[0].EventId.Id);
        Assert.AreEqual(504, logger.Entries[1].EventId.Id);
        Assert.AreEqual("resolution-001", (string)logger.Entries[0].Properties["ResolutionId"]!);
        Assert.AreEqual("resolution-002", (string)logger.Entries[1].Properties["ResolutionId"]!);
    }
}