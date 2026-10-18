using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the contract of application failure events.
/// </summary>
/// <remarks>
/// Exception identity is checked to ensure providers receive the original
/// exception through ILogger rather than a stringified message property.
/// </remarks>
[TestClass]
public sealed class ApplicationFailureEventsTests
{
    /// <summary>
    /// Verifies the event identity, severity, template, and exception channel.
    /// </summary>
    [TestMethod]
    public void UnhandledException_EmitsCriticalEventWithOriginalException()
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel: LogLevel.Critical);

        InvalidOperationException exception = new(
            "Initialization failed.",
            new ArgumentException("Invalid configuration."));

        // Act
        logger.Application.UnhandledException(exception);

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(701, entry.EventId.Id);
        Assert.AreEqual(
            "Application_UnhandledException",
            entry.EventId.Name);
        Assert.AreEqual(LogLevel.Critical, entry.Level);
        Assert.AreSame(exception, entry.Exception);

        // The exception must not become a structured template property.
        Assert.HasCount(1, entry.Properties);
        Assert.AreEqual(
            "Application encountered an unrecoverable exception.",
            (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies that disabled Critical logging prevents emission.
    /// </summary>
    [TestMethod]
    public void UnhandledException_WhenLoggingDisabled_DoesNotEmit()
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel: LogLevel.None);
        InvalidOperationException exception = new("Test failure.");

        // Act
        logger.Application.UnhandledException(exception);

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that an exception is required regardless of logging filters.
    /// </summary>
    /// <param name="minimumLevel">The logger's minimum severity.</param>
    /// <remarks>
    /// Argument validation remains consistent when logging is disabled.
    /// </remarks>
    [TestMethod]
    [DataRow(LogLevel.Trace)]
    [DataRow(LogLevel.None)]
    public void UnhandledException_WithNullException_Throws(
        LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        ArgumentNullException exception =
            Assert.ThrowsExactly<ArgumentNullException>(
                () => logger.Application.UnhandledException(null!));

        // Assert
        Assert.AreEqual("exception", exception.ParamName);
        Assert.HasCount(0, logger.Entries);
    }
}