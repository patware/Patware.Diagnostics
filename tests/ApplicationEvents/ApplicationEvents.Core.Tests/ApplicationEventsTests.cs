using Microsoft.Extensions.Logging;

namespace Patware.ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the public contracts of application lifecycle event methods.
/// </summary>
/// <remarks>
/// Assertions cover emitted records rather than console rendering or
/// private source-generator implementation details.
/// Expected identities and templates are independent of the catalog
/// so unintended contract changes cause test failures.
/// </remarks>
[TestClass]
public sealed class ApplicationEventsTests
{
    /// <summary>
    /// Verifies that Starting emits event 101 without additional payload.
    /// </summary>
    [TestMethod]
    public void Starting_EmitsExpectedEventWithoutAdditionalProperties()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Application.Starting();

        // Assert
        RecordedLog entry = AssertSingleEvent(
            logger,
            expectedId: 101,
            expectedName: "Application_Starting",
            expectedTemplate: "Application is starting.");

        Assert.HasCount(1, entry.Properties);
    }

    /// <summary>
    /// Verifies that Started preserves the startup duration as a double.
    /// </summary>
    [TestMethod]
    public void Started_EmitsExpectedEventWithStructuredDuration()
    {
        // Arrange
        RecordingLogger logger = new();
        const double totalMilliseconds = 123.3087;

        // Act
        logger.Application.Started(totalMilliseconds: totalMilliseconds);

        // Assert
        RecordedLog entry = AssertSingleEvent(
            logger,
            expectedId: 102,
            expectedName: "Application_Started",
            expectedTemplate:
                "Application is Started.  Elapsed: {TotalMilliseconds} milliseconds");

        Assert.HasCount(2, entry.Properties);
        Assert.IsTrue(entry.Properties.ContainsKey("TotalMilliseconds"));
        Assert.AreEqual(
            totalMilliseconds,
            (double)entry.Properties["TotalMilliseconds"]!);
    }

    /// <summary>
    /// Verifies that Stopping preserves the supplied reason as structured data.
    /// </summary>
    /// <remarks>
    /// Braces in the reason must remain part of the argument value rather
    /// than becoming additional message-template placeholders.
    /// </remarks>
    [TestMethod]
    public void Stopping_EmitsExpectedEventWithLiteralStructuredReason()
    {
        // Arrange
        RecordingLogger logger = new();
        const string reason = "Operator requested shutdown: {RequestId}.";

        // Act
        logger.Application.Stopping(reason: reason);

        // Assert
        RecordedLog entry = AssertSingleEvent(
            logger,
            expectedId: 103,
            expectedName: "Application_Stopping",
            expectedTemplate: "Application is stopping.  Reason: {Reason}");

        Assert.HasCount(2, entry.Properties);
        Assert.IsTrue(entry.Properties.ContainsKey("Reason"));
        Assert.AreEqual(reason, (string)entry.Properties["Reason"]!);
    }

    /// <summary>
    /// Verifies that Stopped emits event 104 without additional payload.
    /// </summary>
    [TestMethod]
    public void Stopped_EmitsExpectedEventWithoutAdditionalProperties()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Application.Stopped();

        // Assert
        RecordedLog entry = AssertSingleEvent(
            logger,
            expectedId: 104,
            expectedName: "Application_Stopped",
            expectedTemplate: "Application is Stopped");

        Assert.HasCount(1, entry.Properties);
    }

    /// <summary>
    /// Verifies that lifecycle events do not invoke Log when Information
    /// logging is disabled.
    /// </summary>
    /// <param name="eventId">The event method to exercise.</param>
    /// <remarks>
    /// Each data row runs independently with its own logger.
    /// The logger records every Log invocation, including disabled levels,
    /// so this test verifies filtering performed by the event emitter.
    /// </remarks>
    [TestMethod]
    [DataRow(101)]
    [DataRow(102)]
    [DataRow(103)]
    [DataRow(104)]
    public void ApplicationEvent_WhenInformationIsDisabled_DoesNotEmit(
        int eventId)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel: LogLevel.Warning);

        // Act
        switch (eventId)
        {
            case 101:
                logger.Application.Starting();
                break;

            case 102:
                logger.Application.Started(totalMilliseconds: 123.3087);
                break;

            case 103:
                logger.Application.Stopping(reason: "Unknown");
                break;

            case 104:
                logger.Application.Stopped();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(eventId));
        }

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    private static RecordedLog AssertSingleEvent(
        RecordingLogger logger,
        int expectedId,
        string expectedName,
        string expectedTemplate)
    {
        Assert.HasCount(
            1,
            logger.Entries,
            "Each event method should emit exactly one record.");

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.AreEqual(expectedId, entry.EventId.Id);
        Assert.AreEqual(expectedName, entry.EventId.Name);
        Assert.IsNull(entry.Exception);

        Assert.IsTrue(entry.Properties.ContainsKey("{OriginalFormat}"));
        Assert.AreEqual(
            expectedTemplate,
            (string)entry.Properties["{OriginalFormat}"]!);

        return entry;
    }
}