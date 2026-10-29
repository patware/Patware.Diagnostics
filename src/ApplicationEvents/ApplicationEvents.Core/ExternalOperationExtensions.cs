using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace Patware.Diagnostics.ApplicationEvents;

/// <summary>
/// Observes explicitly supplied external operations and their normal completion.
/// </summary>
public static class ExternalOperationExtensions
{
    /// <summary>
    /// Observes an external operation that does not accept cancellation.
    /// </summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="logger">The logger receiving diagnostic events.</param>
    /// <param name="operationAsync">The operation to invoke.</param>
    /// <param name="externalName">The non-sensitive logical dependency name.</param>
    /// <param name="operationName">
    /// The logical operation name, defaulting to the calling member.
    /// </param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <param name="operationId">The attempt identifier, or null to generate one.</param>
    /// <param name="timeout">The cooperative cancellation budget, or null for none.</param>
    /// <returns>The original result returned by the operation.</returns>
    /// <remarks>
    /// <para>
    /// The parameterless delegate cannot receive the helper's cancellation token.
    /// Caller cancellation prevents invocation when already requested, but cannot
    /// interrupt an operation that has started. A deadline cannot forcibly stop it.
    /// Prefer the token-taking overload when cancellation or timeout matters.
    /// </para>
    /// <para>
    /// The default operation name identifies the calling member, not the method
    /// invoked inside the delegate. Emits 203 before invocation and 214 when
    /// the delegate returns normally. Returned values are not inspected for
    /// business success.
    /// </para>
    /// <para>
    /// A null timeout imposes no additional deadline. Logging filters do not
    /// disable execution or timeout behavior. Logging-provider exceptions
    /// are not suppressed.
    /// </para>
    /// <para>
    /// The helper does not retry, cache results, select fallback, dispose
    /// returned resources, or change application mode.
    /// </para>
    /// </remarks>
    public static Task<T> RunExternalAsync<T>(
        this ILogger logger,
        Func<Task<T>> operationAsync,
        string externalName = "UnspecifiedExternal",
        [CallerMemberName] string? operationName = null,
        string? operationId = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationAsync);

        return RunExternalCoreAsync(
            logger,
            _ => operationAsync(),
            externalName,
            operationName ?? "UnknownOperation",
            operationId,
            timeout,
            cancellationToken);
    }

    /// <summary>
    /// Observes an external operation with optional cooperative timeout cancellation.
    /// </summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="logger">The logger receiving diagnostic events.</param>
    /// <param name="operationAsync">
    /// The operation, which should pass the supplied token to its dependency.
    /// </param>
    /// <param name="externalName">The non-sensitive logical dependency name.</param>
    /// <param name="operationName">
    /// The logical operation name, defaulting to the calling member.
    /// </param>
    /// <param name="cancellationToken">Caller cancellation or application shutdown.</param>
    /// <param name="operationId">The attempt identifier, or null to generate one.</param>
    /// <param name="timeout">A positive, finite cancellation budget, or null for none.</param>
    /// <returns>The original result returned by the operation.</returns>
    /// <remarks>
    /// <para>
    /// Emits 203 before invocation and 214 when the delegate returns normally.
    /// Returned values are not inspected for business success.
    /// </para>
    /// <para>
    /// Deadline cancellation emits 601 and rethrows the original exception.
    /// Caller cancellation takes precedence and does not emit 601.
    /// Other exceptions propagate without automatic classification.
    /// </para>
    /// <para>
    /// Timeout cancellation is cooperative. A delegate that ignores its token
    /// can complete after the requested deadline and still produce 214.
    /// </para>
    /// </remarks>
    public static Task<T> RunExternalAsync<T>(
        this ILogger logger,
        Func<CancellationToken, Task<T>> operationAsync,
        string externalName = "UnspecifiedExternal",
        [CallerMemberName] string? operationName = null,
        string? operationId = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationAsync);

        return RunExternalCoreAsync(
            logger,
            operationAsync,
            externalName,
            operationName ?? "UnknownOperation",
            operationId,
            timeout,
            cancellationToken);
    }




    /// <summary>
    /// Executes the operation with shared observation and cancellation behavior.
    /// </summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="logger">The logger receiving diagnostic events.</param>
    /// <param name="externalName">The non-sensitive logical dependency name.</param>
    /// <param name="operationName">The non-sensitive logical operation name.</param>
    /// <param name="timeout">A positive, finite cancellation budget.</param>
    /// <param name="operationAsync">
    /// The operation, which must use the supplied cancellation token.
    /// </param>
    /// <param name="cancellationToken">Caller cancellation or application shutdown.</param>
    /// <param name="operationId">
    /// The attempt identifier, or null to generate one.
    /// </param>
    /// <returns>The original result returned by the operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// A required reference argument is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A logical name or supplied identifier is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The timeout is nonpositive or exceeds the supported timer range.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation is observed by the operation or was already requested.
    /// </exception>
    private static async Task<T> RunExternalCoreAsync<T>(
        ILogger logger,
        Func<CancellationToken, Task<T>> operationAsync,
        string externalName,
        string operationName,
        string? operationId,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operationAsync);

        if (timeout is TimeSpan budget && (
            budget <= TimeSpan.Zero
            || budget > TimeSpan.FromMilliseconds(uint.MaxValue - 1d)))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be null or positive and within the supported timer range.");
        }

        if (operationId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        }

        cancellationToken.ThrowIfCancellationRequested();

        operationId ??= Guid.NewGuid().ToString("N");

        T result;

        logger.External.Calling(externalName, operationName, operationId);

        if (timeout is TimeSpan timeoutBudget)
        {
            using CancellationTokenSource deadline = new();
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);


            deadline.CancelAfter(timeoutBudget);

            try
            {
                result = await operationAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
                when (!cancellationToken.IsCancellationRequested
                      && deadline.IsCancellationRequested)
            {
                logger.External.OperationTimedOut(
                    externalName,
                    operationName,
                    operationId,
                    timeoutBudget.TotalMilliseconds,
                    ex);

                throw;
            }
        }
        else
        {
            result = await operationAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.External.CallCompleted(externalName, operationName, operationId);

        return result;
    }
}