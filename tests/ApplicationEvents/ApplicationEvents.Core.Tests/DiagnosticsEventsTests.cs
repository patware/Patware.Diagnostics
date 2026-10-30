using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;
namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the explicitly emitted teapot event.
/// </summary>
/// <remarks>
/// The novelty event is informational and does not imply degradation,
/// recovery, or an HTTP response.
/// </remarks>
[TestClass]
public sealed class DiagnosticsEventsTests
{
    /// <summary>
    /// Verifies the teapot identity, severity, and message without payload.
    /// </summary>
    [TestMethod]
    public void ImaTeapot_EmitsExpectedContract()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Diagnostics.ImaTeapot();

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(418, entry.EventId.Id);
        Assert.AreEqual("Diagnostics_ImaTeapot", entry.EventId.Name);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.IsNull(entry.Exception);
        Assert.HasCount(1, entry.Properties);
        Assert.AreEqual("I'm a teapot.", (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies that normal severity filtering applies to the novelty event.
    /// </summary>
    /// <param name="minimumLevel">The minimum enabled severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.None)]
    public void ImaTeapot_WhenInformationDisabled_DoesNotEmit(
        LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        logger.Diagnostics.ImaTeapot();

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that a default-initialized event group rejects emission.
    /// </summary>
    [TestMethod]
    public void ImaTeapot_WithDefaultGroup_Throws()
    {
        // Arrange
        DiagnosticsEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(
                () => events.ImaTeapot());

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies repeated explicit emission without inferred application events.
    /// </summary>
    [TestMethod]
    public void ImaTeapot_RepeatedCalls_EmitOnlyTeapotEvents()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Diagnostics.ImaTeapot();
        logger.Diagnostics.ImaTeapot();

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(418, logger.Entries[0].EventId.Id);
        Assert.AreEqual(418, logger.Entries[1].EventId.Id);
    }
}