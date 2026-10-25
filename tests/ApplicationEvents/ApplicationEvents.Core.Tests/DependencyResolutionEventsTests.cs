using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies dependency resolution beginning and success event contracts.
/// </summary>
/// <remarks>
/// These tests verify explicit emission. The application owns resolution,
/// correlation, and selection of the appropriate outcome.
/// </remarks>
[TestClass]
public sealed class DependencyResolutionEventsTests
{
    /// <summary>
    /// Verifies identity, severity, and structured properties.
    /// </summary>
    /// <param name="resolved">Whether to emit the success event.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResolutionEvent_EmitsExpectedContract(bool resolved)
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        if (resolved)
        {
            logger.Dependency.Resolved("Report{Formatter}", "GenerateReport", "resolution-001");
        }
        else
        {
            logger.Dependency.Resolving("Report{Formatter}", "GenerateReport", "resolution-001");
        }

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(resolved ? 116 : 115, entry.EventId.Id);
        Assert.AreEqual(resolved ? "Dependency_Resolved" : "Dependency_Resolving", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.IsNull(entry.Exception);
        Assert.HasCount(4, entry.Properties);
        Assert.AreEqual("Report{Formatter}", (string)entry.Properties["DependencyName"]!);
        Assert.AreEqual("GenerateReport", (string)entry.Properties["OperationName"]!);
        Assert.AreEqual("resolution-001", (string)entry.Properties["ResolutionId"]!);
        Assert.AreEqual(
            resolved
                ? "Dependency {DependencyName} resolved. " +
                  "Operation: {OperationName}. Resolution: {ResolutionId}."
                : "Resolving dependency {DependencyName}. " +
                  "Operation: {OperationName}. Resolution: {ResolutionId}.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies filtering of both Information events.
    /// </summary>
    /// <param name="minimumLevel">The minimum enabled severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.None)]
    public void ResolutionEvents_WhenInformationDisabled_DoNotEmit(LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        logger.Dependency.Resolving("Formatter", "Report", "resolution-001");
        logger.Dependency.Resolved("Formatter", "Report", "resolution-001");

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that default groups reject both event calls.
    /// </summary>
    /// <param name="resolved">Whether to invoke the success event.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResolutionEvent_WithDefaultGroup_Throws(bool resolved)
    {
        // Arrange
        DependencyEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                if (resolved)
                {
                    events.Resolved("Formatter", "Report", "resolution-001");
                }
                else
                {
                    events.Resolving("Formatter", "Report", "resolution-001");
                }
            });

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies explicit outcomes and correlation for interleaved attempts.
    /// </summary>
    [TestMethod]
    public void ResolutionEvents_PreserveInterleavedAttemptIdentifiers()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Dependency.Resolving("Formatter", "Report", "resolution-001");
        logger.Dependency.Resolving("Formatter", "Report", "resolution-002");
        logger.Dependency.ResolutionFailed("Formatter", "Report", "resolution-002", "ResolutionThrew", null);
        logger.Dependency.Resolved("Formatter", "Report", "resolution-001");

        // Assert
        Assert.HasCount(4, logger.Entries);
        Assert.AreEqual(115, logger.Entries[0].EventId.Id);
        Assert.AreEqual(115, logger.Entries[1].EventId.Id);
        Assert.AreEqual(504, logger.Entries[2].EventId.Id);
        Assert.AreEqual(116, logger.Entries[3].EventId.Id);
        Assert.AreEqual("resolution-002", (string)logger.Entries[2].Properties["ResolutionId"]!);
        Assert.AreEqual("resolution-001", (string)logger.Entries[3].Properties["ResolutionId"]!);
    }

    /// <summary>
    /// Verifies that repeated emission is not deduplicated or completed implicitly.
    /// </summary>
    [TestMethod]
    public void Resolving_RepeatedCalls_EmitOnlyRequestedEvents()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Dependency.Resolving("Formatter", "Report", "resolution-001");
        logger.Dependency.Resolving("Formatter", "Report", "resolution-001");

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(115, logger.Entries[0].EventId.Id);
        Assert.AreEqual(115, logger.Entries[1].EventId.Id);
    }
}