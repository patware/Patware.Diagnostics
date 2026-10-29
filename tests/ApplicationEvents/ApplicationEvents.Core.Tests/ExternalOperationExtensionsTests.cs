using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;


namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies external-operation observation and exception propagation.
/// </summary>
/// <remarks>
/// Controlled delegates exercise the helper without network dependencies.
/// Normal completion is distinct from business success.
/// </remarks>
[TestClass]
public sealed class ExternalOperationExtensionsTests
{
    /// <summary>
    /// Verifies minimal overloads and caller-name forwarding.
    /// </summary>
    /// <param name="acceptsToken">Whether to use the token-taking overload.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MinimalCall_EmitsBeginningAndCompletion(bool acceptsToken)
    {
        // Arrange
        RecordingLogger logger = new();
        object expected = new();

        // Act
        object actual = acceptsToken
            ? await logger.RunExternalAsync(_ => Task.FromResult(expected))
            : await logger.RunExternalAsync(() => Task.FromResult(expected));

        // Assert
        Assert.AreSame(expected, actual);
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(203, logger.Entries[0].EventId.Id);
        Assert.AreEqual(214, logger.Entries[1].EventId.Id);

        string operationId = (string)logger.Entries[0].Properties["OperationId"]!;

        Assert.IsTrue(Guid.TryParseExact(operationId, "N", out _));

        foreach (RecordedLog entry in logger.Entries)
        {
            Assert.AreEqual("UnspecifiedExternal", (string)entry.Properties["ExternalName"]!);
            Assert.AreEqual(nameof(MinimalCall_EmitsBeginningAndCompletion), (string)entry.Properties["OperationName"]!);
            Assert.AreEqual(operationId, (string)entry.Properties["OperationId"]!);
        }
    }

    /// <summary>
    /// Verifies explicit correlation and names.
    /// </summary>
    [TestMethod]
    public async Task SuppliedMetadata_IsPreserved()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        int result = await logger.RunExternalAsync(
            _ => Task.FromResult(42),
            externalName: "Products",
            operationName: "Fetch",
            operationId: "attempt-001");

        // Assert
        Assert.AreEqual(42, result);
        Assert.HasCount(2, logger.Entries);

        foreach (RecordedLog entry in logger.Entries)
        {
            Assert.AreEqual("Products", (string)entry.Properties["ExternalName"]!);
            Assert.AreEqual("Fetch", (string)entry.Properties["OperationName"]!);
            Assert.AreEqual("attempt-001", (string)entry.Properties["OperationId"]!);
        }
    }

    /// <summary>
    /// Verifies original exceptions without inferred completion or failure events.
    /// </summary>
    /// <param name="withTimeout">Whether a deadline is configured.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OperationFailure_PreservesException(bool withTimeout)
    {
        // Arrange
        RecordingLogger logger = new();
        InvalidOperationException original = new("Simulated failure.");

        // Act
        InvalidOperationException caught =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => logger.RunExternalAsync(
                    _ => Task.FromException<int>(original),
                    timeout: withTimeout
                        ? TimeSpan.FromSeconds(10)
                        : null));

        // Assert
        Assert.AreSame(original, caught);
        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(203, logger.Entries[0].EventId.Id);
    }

    /// <summary>
    /// Verifies caller cancellation without timeout classification.
    /// </summary>
    /// <param name="withTimeout">Whether a deadline is configured.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CallerCancellation_DoesNotEmitTimeout(bool withTimeout)
    {
        // Arrange
        RecordingLogger logger = new();
        using CancellationTokenSource cancellation = new();
        OperationCanceledException original = new(cancellation.Token);

        // Act
        OperationCanceledException caught =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => logger.RunExternalAsync(
                    _ =>
                    {
                        cancellation.Cancel();
                        return Task.FromException<int>(original);
                    },
                    timeout: withTimeout
                        ? TimeSpan.FromSeconds(10)
                        : null,
                    cancellationToken: cancellation.Token));

        // Assert
        Assert.AreSame(original, caught);
        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(203, logger.Entries[0].EventId.Id);
    }

    /// <summary>
    /// Verifies deadline classification, correlation, and exception preservation.
    /// </summary>
    [TestMethod]
    public async Task DeadlineCancellation_EmitsOneTimeout()
    {
        // Arrange
        RecordingLogger logger = new();
        TimeSpan budget = TimeSpan.FromMilliseconds(25);
        OperationCanceledException? original = null;
        OperationCanceledException? caught = null;

        // Act
        try
        {
            await logger.RunExternalAsync(
                async token =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    }
                    catch (OperationCanceledException exception)
                    {
                        original = exception;
                        throw;
                    }

                    return 42;
                },
                operationId: "timeout-001",
                timeout: budget)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException exception)
        {
            caught = exception;
        }

        // Assert
        Assert.IsNotNull(original);
        Assert.AreSame(original, caught);
        Assert.HasCount(2, logger.Entries);
        Assert.AreEqual(203, logger.Entries[0].EventId.Id);
        Assert.AreEqual(601, logger.Entries[1].EventId.Id);
        Assert.AreSame(original, logger.Entries[1].Exception);
        Assert.AreEqual(budget.TotalMilliseconds, (double)logger.Entries[1].Properties["TimeoutMilliseconds"]!);
        Assert.AreEqual("timeout-001", (string)logger.Entries[1].Properties["OperationId"]!);
    }

    /// <summary>
    /// Verifies that pre-existing cancellation prevents execution and emission.
    /// </summary>
    [TestMethod]
    public async Task PreCanceledToken_DoesNotInvokeOperation()
    {
        // Arrange
        RecordingLogger logger = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        bool invoked = false;

        // Act
        OperationCanceledException exception =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => logger.RunExternalAsync(
                    _ =>
                    {
                        invoked = true;
                        return Task.FromResult(42);
                    },
                    cancellationToken: cancellation.Token));

        // Assert
        Assert.IsFalse(invoked);
        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that filtering does not disable execution.
    /// </summary>
    /// <param name="minimumLevel">The minimum enabled severity.</param>
    [TestMethod]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.None)]
    public async Task LoggingDisabled_StillExecutesOperation(LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel);
        int calls = 0;

        // Act
        int result = await logger.RunExternalAsync(
            _ =>
            {
                calls++;
                return Task.FromResult(42);
            });

        // Assert
        Assert.AreEqual(42, result);
        Assert.AreEqual(1, calls);
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies rejection of invalid metadata and timeout values before execution.
    /// </summary>
    /// <param name="invalidArgument">The argument to invalidate.</param>
    [TestMethod]
    [DataRow("externalName")]
    [DataRow("operationName")]
    [DataRow("operationId")]
    [DataRow("timeout")]
    public async Task InvalidArgument_DoesNotInvokeOperation(
        string invalidArgument)
    {
        // Arrange
        RecordingLogger logger = new();
        bool invoked = false;
        ArgumentException? caught = null;

        // Act
        try
        {
            await logger.RunExternalAsync(
                _ =>
                {
                    invoked = true;
                    return Task.FromResult(42);
                },
                externalName: invalidArgument == "externalName"
                    ? " "
                    : "Products",
                operationName: invalidArgument == "operationName"
                    ? " "
                    : "Fetch",
                operationId: invalidArgument == "operationId"
                    ? " "
                    : "attempt-001",
                timeout: invalidArgument == "timeout"
                    ? TimeSpan.Zero
                    : null);
        }
        catch (ArgumentException exception)
        {
            caught = exception;
        }

        // Assert
        Assert.IsNotNull(caught);
        Assert.AreEqual(invalidArgument, caught.ParamName);
        Assert.IsFalse(invoked);
        Assert.HasCount(0, logger.Entries);
    }
}