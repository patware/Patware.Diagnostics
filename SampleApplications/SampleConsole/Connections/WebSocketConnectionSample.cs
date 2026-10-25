using System.Net.WebSockets;

using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace SampleConsole.Connections;

/// <summary>
/// Demonstrates explicit WebSocket connection beginning and success events.
/// </summary>
/// <remarks>
/// Connection establishment and cleanup are separate operations. This sample
/// does not infer application degradation or classify every WebSocket failure
/// as service unavailability.
/// </remarks>
internal static class WebSocketConnectionSample
{
    /// <summary>
    /// Connects to the configured endpoint and then closes the connection.
    /// </summary>
    /// <param name="logger">The existing application logger.</param>
    /// <param name="endpoint">The WebSocket demonstration endpoint.</param>
    /// <param name="shutdownToken">Application shutdown cancellation.</param>
    /// <returns>A task representing the demonstration.</returns>
    /// <remarks>
    /// A new client and operation identifier are used for each attempt.
    /// Event 202 confirms the WebSocket handshake, not a business operation.
    /// </remarks>
    public static async Task RunAsync(ILogger logger, Uri endpoint, CancellationToken shutdownToken)
    {
        const string externalName = "WebSocketEcho";
        const string operationName = "OpenWebSocket";
        string operationId = Guid.NewGuid().ToString("N");

        shutdownToken.ThrowIfCancellationRequested();

        using ClientWebSocket socket = new();

        try
        {
            await logger.ConnectExternalAsync(
                externalName: externalName,
                operationName: operationName,
                timeout: TimeSpan.FromSeconds(10),
                connectAsync: token => socket.ConnectAsync(endpoint, token),
                cancellationToken: shutdownToken,
                operationId: operationId);
        }
        catch (OperationCanceledException)
            when (shutdownToken.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("WebSocket connection was canceled or timed out.");
            return;
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "WebSocket connection exception details.Attempt: {OperationId}.", operationId);

            Console.WriteLine("WebSocket connection failed. See Debug diagnostics for details.");

            return;
        }

        Console.WriteLine("WebSocket handshake completed successfully.");

        // Cleanup failures must not be reported as connection failures.
        using CancellationTokenSource closeCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);

        closeCancellation.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Sample completed", closeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("WebSocket cleanup was canceled or exceeded its budget. Attempt: {OperationId}", operationId);
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "WebSocket cleanup failed after successful connection. Attempt: {OperationId}", operationId);
        }
    }
}