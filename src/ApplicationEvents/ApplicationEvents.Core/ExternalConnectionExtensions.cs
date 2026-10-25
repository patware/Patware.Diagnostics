using Microsoft.Extensions.Logging;

namespace Patware.Diagnostics.ApplicationEvents;

/// <summary>
/// Observes explicitly supplied external connection operations.
/// </summary>
/// <remarks>
/// These helpers orchestrate observation and cooperative cancellation.
/// The individual event methods remain independent diagnostic emitters.
/// </remarks>
public static class ExternalConnectionExtensions
{
    /// <summary>
    /// Observes connection establishment with a cooperative timeout.
    /// </summary>
    /// <param name="logger">The logger receiving connection events.</param>
    /// <param name="externalName">
    /// The non-sensitive logical dependency name.
    /// </param>
    /// <param name="operationName">
    /// The non-sensitive logical connection operation name.
    /// </param>
    /// <param name="timeout">
    /// A positive, finite cancellation budget supported by the timer.
    /// </param>
    /// <param name="connectAsync">
    /// The connection operation, which must use the supplied cancellation token.
    /// </param>
    /// <param name="cancellationToken">
    /// Caller cancellation, including application shutdown.
    /// </param>
    /// <param name="operationId">
    /// The identifier of this connection attempt, or null to generate one.
    /// Supply an identifier when caller diagnostics must correlate failures
    /// as well as successful connections.
    /// </param>
    /// <returns>
    /// A task representing the observed connection attempt.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// The logger, delegate, or either logical name is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Either logical name is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The timeout is nonpositive or exceeds the supported timer range.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation is observed by the delegate or was already requested
    /// by the caller.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Emits 201 before invoking the delegate and 202 after successful
    /// completion, using the supplied operation identifier or a generated one.
    /// </para>
    /// <para>
    /// The timeout requests cancellation; it cannot forcibly stop a delegate
    /// that ignores its token. Normal delegate completion is treated as success.
    /// </para>
    /// <para>
    /// Deadline cancellation emits 601 with the original exception and
    /// rethrows it. Caller cancellation takes precedence when both tokens
    /// are canceled and does not emit 601.
    /// </para>
    /// <para>
    /// Other exceptions propagate without inferred authentication,
    /// authorization, or availability events.
    /// </para>
    /// <para>
    /// Include only connection establishment in the delegate. Resource
    /// ownership, business operations, cleanup, retries, fallback, and
    /// application mode remain caller responsibilities.
    /// </para>
    /// <para>
    /// Observation and timeout behavior remain active when logging is
    /// filtered. Logging-provider exceptions are not suppressed.
    /// </para>
    /// <para>
    /// Callers needing correlation in failure handlers should supply one
    /// before invocation. Use a new identifier for each attempt, including retries.
    /// </para>
    /// </remarks>
    public static async Task ConnectExternalAsync(
        this ILogger logger,
        string externalName,
        string operationName,
        TimeSpan timeout,
        Func<CancellationToken, Task> connectAsync,
        CancellationToken cancellationToken = default,
        string? operationId = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(connectAsync);


        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Timeout must be positive and within the supported timer range.");
        }

        if (operationId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        }

        operationId ??= Guid.NewGuid().ToString("N");

        cancellationToken.ThrowIfCancellationRequested();



        using CancellationTokenSource deadline = new();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        logger.External.Connecting(externalName, operationName, operationId);

        deadline.CancelAfter(timeout);

        try
        {
            await connectAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
            when (!cancellationToken.IsCancellationRequested
                  && deadline.IsCancellationRequested)
        {
            logger.External.OperationTimedOut(
                externalName,
                operationName,
                operationId,
                timeout.TotalMilliseconds,
                ex);

            throw;
        }

        logger.External.Connected(
            externalName,
            operationName,
            operationId);

    }
}