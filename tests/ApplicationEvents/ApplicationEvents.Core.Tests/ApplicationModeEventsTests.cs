using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the public contracts of application operating-mode events.
/// </summary>
/// <remarks>
/// Expected identities and templates are independent of the catalog so
/// unintended contract changes fail tests.
/// The application owns mode transitions; these methods only emit records.
/// </remarks>
[TestClass]
public sealed class ApplicationModeEventsTests
{
    /// <summary>
    /// Verifies that degradation emits a warning with a structured reason.
    /// </summary>
    /// <remarks>
    /// Braces in the reason must remain literal argument data rather than
    /// becoming additional message-template placeholders.
    /// </remarks>
    [TestMethod]
    public void InDegradedMode_EmitsWarningWithStructuredReason()
    {
        // Arrange
        RecordingLogger logger = new();
        const string reason = "Product service unavailable; using cached data for {Region}.";

        // Act
        logger.Application.InDegradedMode(reason: reason);

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(401, entry.EventId.Id);
        Assert.AreEqual("Application_InDegradedMode", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Warning, entry.Level);
        Assert.IsNull(entry.Exception);

        Assert.HasCount(2, entry.Properties);
        Assert.AreEqual(reason, (string)entry.Properties["Reason"]!);
        Assert.AreEqual(
            "Application is operating in degraded mode. Reason: {Reason}",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies that recovery emits an informational event without payload.
    /// </summary>
    [TestMethod]
    public void InNormalMode_EmitsInformationWithoutAdditionalProperties()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Application.InNormalMode();

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(204, entry.EventId.Id);
        Assert.AreEqual("Application_InNormalMode", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.IsNull(entry.Exception);

        Assert.HasCount(1, entry.Properties);
        Assert.AreEqual(
            "Application has returned to normal operation.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies filtering at the configured minimum severity.
    /// </summary>
    /// <param name="minimumLevel">The logger's minimum severity.</param>
    /// <param name="expectedCount">The expected number of records.</param>
    /// <remarks>
    /// RecordingLogger captures every Log invocation, so this verifies
    /// filtering by the generated emitters rather than by the test logger.
    /// </remarks>
    [TestMethod]
    [DataRow(LogLevel.Information, 2)]
    [DataRow(LogLevel.Warning, 1)]
    [DataRow(LogLevel.Error, 0)]
    [DataRow(LogLevel.None, 0)]
    public void ModeEvents_RespectMinimumLevel(LogLevel minimumLevel, int expectedCount)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel: minimumLevel);

        // Act
        logger.Application.InDegradedMode(reason: "Product service unavailable; using cached data.");
        logger.Application.InNormalMode();

        // Assert
        Assert.HasCount(expectedCount, logger.Entries);

        if (expectedCount == 1)
        {
            Assert.AreEqual(401, logger.Entries[0].EventId.Id);
        }
    }

    /// <summary>
    /// Verifies that repeated calls are emitted without hidden state tracking.
    /// </summary>
    /// <remarks>
    /// Transition detection belongs to the application. Suppression inside
    /// an emitter could conceal a later incident or couple unrelated callers.
    /// </remarks>
    [TestMethod]
    public void ModeEvents_EmitEveryExplicitCall()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Application.InDegradedMode(reason: "Using cached data.");
        logger.Application.InDegradedMode(reason: "Using cached data.");
        logger.Application.InNormalMode();
        logger.Application.InNormalMode();

        // Assert
        CollectionAssert.AreEqual(
            new[] { 401, 401, 204, 204 },
            logger.Entries.Select(entry => entry.EventId.Id).ToArray());
    }
}