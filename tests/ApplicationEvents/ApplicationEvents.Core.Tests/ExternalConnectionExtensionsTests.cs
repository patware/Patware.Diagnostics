using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies external connection observation and failure propagation.
/// </summary>
/// <remarks>
/// Tests use controlled delegates rather than network services.
/// </remarks>
[TestClass]
public sealed class ExternalConnectionExtensionsTests
{
    /// <summary>
    /// Verifies invocation, ordering, and correlation.
    /// </summary>
    [TestMethod]
    public async Task ConnectExternalAsync_OnSuccess_EmitsCorrelatedEvents()
    {
        // Arrange
        RecordingLogger logger = new();
        int calls = 0;

        // Act
        await logger.ConnectExternalAsync(
            "Echo",
            "OpenWebSocket",
            TimeSpan.FromSeconds(10),
            token =>
            {
                calls++;
                Assert.IsTrue(token.CanBeCanceled);
                Assert.HasCount(1, logger.Entries);
                Assert.AreEqual(201, logger.Entries[0].EventId.Id);
                return Task.CompletedTask;
            });

        // Assert
        Assert.AreEqual(1, calls);
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(202, logger.Entries[1].EventId.Id);

        string operationId = (string)logger.Entries[0].Properties["OperationId"]!;

        Assert.IsTrue(Guid.TryParseExact(operationId, "N", out _));
        Assert.AreEqual(operationId, (string)logger.Entries[1].Properties["OperationId"]!);
    }

    /// <summary>
    /// Verifies that unclassified failures propagate without inferred outcomes.
    /// </summary>
    [TestMethod]
    public async Task ConnectExternalAsync_OnFailure_PreservesException()
    {
        // Arrange
        RecordingLogger logger = new();
        InvalidOperationException original = new("Simulated failure.");

        // Act
        InvalidOperationException caught =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => logger.ConnectExternalAsync(
                    "Echo",
                    "OpenWebSocket",
                    TimeSpan.FromSeconds(10),
                    _ => Task.FromException(original)));

        // Assert
        Assert.AreSame(original, caught);
        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(201, logger.Entries[0].EventId.Id);
    }

    /// <summary>
    /// Verifies that caller-supplied correlation is preserved.
    /// </summary>
    [TestMethod]
    public async Task ConnectExternalAsync_WithOperationId_PreservesIdentifier()
    {
        // Arrange
        RecordingLogger logger = new();
        const string operationId = "caller-connection-001";

        // Act
        await logger.ConnectExternalAsync(
            externalName: "Echo",
            operationName: "OpenWebSocket",
            timeout: TimeSpan.FromSeconds(10),
            connectAsync: _ => Task.CompletedTask,
            operationId: operationId);

        // Assert
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(201, logger.Entries[0].EventId.Id);
        Assert.AreEqual(202, logger.Entries[1].EventId.Id);

        foreach (RecordedLog entry in logger.Entries)
        {
            Assert.AreEqual(operationId, (string)entry.Properties["OperationId"]!);
        }
    }
}