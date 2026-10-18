using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Patware.Diagnostics.ApplicationEvents.Hosting;

/// <summary>
/// Reports terminating runtime exceptions through application event 701.
/// </summary>
/// <remarks>
/// <para>
/// Observation starts in StartAsync and remains active through host
/// shutdown until disposal. Failures before subscription or after
/// disposal are outside this observer's coverage.
/// </para>
/// <para>
/// The handler does not initiate shutdown, flush providers, or change
/// runtime exception policy. Logging failures are suppressed to avoid
/// throwing another exception from the notification handler.
/// </para>
/// </remarks>
internal sealed class UnhandledExceptionObserver : IHostedService, IDisposable
{
    private readonly object _subscriptionLock = new();
    private readonly IUnhandledExceptionSource _source;
    private readonly ILogger _logger;

    private bool _subscribed;
    private int _disposed;
    private int _reporting;

    /// <summary>
    /// Initializes the observer.
    /// </summary>
    /// <param name="source">The runtime notification source.</param>
    /// <param name="loggerFactory">The application's logger factory.</param>
    public UnhandledExceptionObserver(IUnhandledExceptionSource source, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _source = source;
        _logger = loggerFactory.CreateLogger("Patware.Diagnostics.ApplicationEvents.Hosting");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_subscriptionLock)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);

            if (!_subscribed)
            {
                _source.UnhandledException += OnUnhandledException;
                _subscribed = true;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Keep observing while the remaining hosted services stop.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_subscriptionLock)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (_subscribed)
            {
                _source.UnhandledException -= OnUnhandledException;
                _subscribed = false;
            }
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            !args.IsTerminating ||
            args.ExceptionObject is not Exception exception)
        {
            return;
        }

        // Do not block a failing thread waiting for another report.
        // Suppress overlapping or recursive notifications.
        if (Interlocked.CompareExchange(ref _reporting, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _logger.Application.UnhandledException(exception);
        }
        catch (Exception)
        {
            // Reporting is best effort. Do not recursively log failures
            // through the same potentially failing logging pipeline.
        }
        finally
        {
            Volatile.Write(ref _reporting, 0);
        }
    }
}