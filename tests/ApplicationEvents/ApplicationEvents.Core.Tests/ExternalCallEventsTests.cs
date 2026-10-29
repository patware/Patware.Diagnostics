using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies external call beginning and normal-completion event contracts.
/// </summary>
/// <remarks>
/// Completion describes normal return, not business success.
/// Tests assert the public logging contract independently of the catalog.
/// </remarks>
[TestClass]
public sealed class ExternalCallEventsTests
{
    /// <summary>
    /// Verifies identity, severity, template, and structured properties.
    /// </summary>
    /// <param name="completed">Whether to emit normal completion.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CallEvent_EmitsExpectedContract(bool completed)
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        if (completed)
        {
            logger.External.CallCompleted("Product{Service}", "FetchPrice", "attempt-001");
        }
        else
        {
            logger.External.Calling("Product{Service}", "FetchPrice", "attempt-001");
        }

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(completed ? 214 : 203, entry.EventId.Id);
        Assert.AreEqual(completed ? "External_CallCompleted" : "External_Calling", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.IsNull(entry.Exception);
        Assert.HasCount(4, entry.Properties);
        Assert.AreEqual("Product{Service}", (string)entry.Properties["ExternalName"]!);
        Assert.AreEqual("FetchPrice", (string)entry.Properties["OperationName"]!);
        Assert.AreEqual("attempt-001", (string)entry.Properties["OperationId"]!);
        Assert.AreEqual(
            completed
                ? "External dependency {ExternalName} operation completed. Operation: {OperationName}. Attempt: {OperationId}."
                : "Calling external dependency {ExternalName}. Operation: {OperationName}. Attempt: {OperationId}.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies filtering of both Information events.
    /// </summary>
    /// <param name="minimumLevel">The minimum enabled severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.None)]
    public void CallEvents_WhenInformationDisabled_DoNotEmit(LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        logger.External.Calling("Products", "Fetch", "attempt-001");
        logger.External.CallCompleted("Products", "Fetch", "attempt-001");

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that default groups reject both event calls.
    /// </summary>
    /// <param name="completed">Whether to invoke normal completion.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CallEvent_WithDefaultGroup_Throws(bool completed)
    {
        // Arrange
        ExternalEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                if (completed)
                {
                    events.CallCompleted("Products", "Fetch", "attempt-001");
                }
                else
                {
                    events.Calling("Products", "Fetch", "attempt-001");
                }
            });

        // Assert
        Assert.IsNotNull(exception);
    }
}