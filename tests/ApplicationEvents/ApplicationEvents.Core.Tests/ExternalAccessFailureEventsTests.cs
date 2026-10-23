using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies external authentication and authorization failure contracts.
/// </summary>
/// <remarks>
/// The tests cover normal rejection results and exception-based failures.
/// Event emission remains independent of application mode transitions.
/// </remarks>
[TestClass]
public sealed class ExternalAccessFailureEventsTests
{
    /// <summary>
    /// Verifies event identity, severity, structured data, and exception handling.
    /// </summary>
    /// <param name="eventId">The event being verified.</param>
    /// <param name="hasException">Whether an original exception is supplied.</param>
    [TestMethod]
    [DataRow(502, false)]
    [DataRow(502, true)]
    [DataRow(503, false)]
    [DataRow(503, true)]
    public void AccessFailure_EmitsExpectedContract(
        int eventId,
        bool hasException)
    {
        // Arrange
        RecordingLogger logger = new();
        Exception? exception = hasException
            ? new InvalidOperationException("Simulated dependency rejection.")
            : null;

        string eventName = eventId == 502
            ? "External_AuthenticationFailed"
            : "External_AuthorizationFailed";

        string reasonCode = eventId == 502
            ? "AuthenticationRejected"
            : "PermissionDenied";

        string template = eventId == 502
            ? "External authentication failed. Dependency: {ExternalName}. " +
              "Operation: {OperationName}. Attempt: {OperationId}. " +
              "Reason: {ReasonCode}."
            : "External authorization failed. Dependency: {ExternalName}. " +
              "Operation: {OperationName}. Attempt: {OperationId}. " +
              "Reason: {ReasonCode}.";

        // Act
        Emit(logger, eventId, "attempt-001", reasonCode, exception);

        // Assert
        Assert.HasCount(1, logger.Entries);
        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(eventId, entry.EventId.Id);
        Assert.AreEqual(eventName, entry.EventId.Name);
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
        Assert.AreEqual("ProductService", (string)entry.Properties["ExternalName"]!);
        Assert.AreEqual("Fetch{Prices}", (string)entry.Properties["OperationName"]!);
        Assert.AreEqual("attempt-001", (string)entry.Properties["OperationId"]!);
        Assert.AreEqual(reasonCode, (string)entry.Properties["ReasonCode"]!);
        Assert.AreEqual(template, (string)entry.Properties["{OriginalFormat}"]!);
    }

    /// <summary>
    /// Verifies that Warning filtering prevents emission.
    /// </summary>
    /// <param name="eventId">The event being verified.</param>
    /// <param name="minimumLevel">The logger's minimum severity.</param>
    [TestMethod]
    [DataRow(502, LogLevel.Error)]
    [DataRow(502, LogLevel.None)]
    [DataRow(503, LogLevel.Error)]
    [DataRow(503, LogLevel.None)]
    public void AccessFailure_WhenWarningDisabled_DoesNotEmit(int eventId, LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);

        // Act
        Emit(logger, eventId, "attempt-001", "Rejected", null);

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that nullable exceptions do not bypass event-group validation.
    /// </summary>
    /// <param name="eventId">The event being verified.</param>
    [TestMethod]
    [DataRow(502)]
    [DataRow(503)]
    public void AccessFailure_WithDefaultGroup_Throws(int eventId)
    {
        // Arrange
        ExternalEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                if (eventId == 502)
                {
                    events.AuthenticationFailed(
                        "ProductService", "FetchPrices",
                        "attempt-001", "AuthenticationRejected", null);
                }
                else
                {
                    events.AuthorizationFailed(
                        "ProductService", "FetchPrices",
                        "attempt-001", "PermissionDenied", null);
                }
            });

        // Assert
        Assert.IsNotNull(exception);
    }

    /// <summary>
    /// Verifies that separate failures emit records without implicit mode events.
    /// </summary>
    [TestMethod]
    public void AccessFailures_EmitEachAttemptWithoutModeTransitions()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        Emit(logger, 502, "attempt-001", "AuthenticationRejected", null);
        Emit(logger, 503, "attempt-002", "PermissionDenied", null);

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(502, logger.Entries[0].EventId.Id);
        Assert.AreEqual(503, logger.Entries[1].EventId.Id);
        Assert.AreEqual("attempt-001", (string)logger.Entries[0].Properties["OperationId"]!);
        Assert.AreEqual("attempt-002", (string)logger.Entries[1].Properties["OperationId"]!);
    }

    private static void Emit(
        ILogger logger,
        int eventId,
        string operationId,
        string reasonCode,
        Exception? exception)
    {
        switch (eventId)
        {
            case 502:
                logger.External.AuthenticationFailed(
                    "ProductService", "Fetch{Prices}",
                    operationId, reasonCode, exception);
                break;

            case 503:
                logger.External.AuthorizationFailed(
                    "ProductService", "Fetch{Prices}",
                    operationId, reasonCode, exception);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(eventId));
        }
    }
}