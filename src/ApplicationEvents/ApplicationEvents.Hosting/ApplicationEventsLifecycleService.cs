using System.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Patware.Diagnostics.ApplicationEvents.Hosting;

/// <summary>
/// Observes host lifecycle notifications and emits application events.
/// </summary>
/// <remarks>
/// <para>
/// Startup measurement begins at this observer's StartingAsync callback.
/// It excludes builder configuration, host construction, and any work
/// completed before that callback.
/// </para>
/// <para>
/// Lifetime subscriptions remain active until disposal so the stopped
/// notification can be observed after hosted services stop.
/// </para>
/// <para>
/// Exceptions thrown while emitting these diagnostic records are
/// suppressed to prevent this observer from interrupting host lifecycle
/// operations. Such failures are not logged recursively.
/// </para>
/// </remarks>
internal sealed class ApplicationEventsLifecycleService :
    IHostedLifecycleService,
    IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger _logger;
    private readonly Stopwatch _startupTimer = new();

    private CancellationTokenRegistration _startedRegistration;
    private CancellationTokenRegistration _stoppingRegistration;
    private CancellationTokenRegistration _stoppedRegistration;

    private int _initialized;

    /// <summary>
    /// Initializes the lifecycle observer.
    /// </summary>
    /// <param name="lifetime">The host lifecycle notifications.</param>
    /// <param name="loggerFactory">The application's logger factory.</param>
    public ApplicationEventsLifecycleService(
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _lifetime = lifetime;
        _logger = loggerFactory.CreateLogger(
            "Patware.Diagnostics.ApplicationEvents.Hosting");
    }

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return Task.CompletedTask;
        }

        _startupTimer.Start();

        TryEmit(static logger => logger.Application.Starting());

        _startedRegistration =
            _lifetime.ApplicationStarted.Register(OnStarted);

        _stoppingRegistration =
            _lifetime.ApplicationStopping.Register(OnStopping);

        _stoppedRegistration =
            _lifetime.ApplicationStopped.Register(OnStopped);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        // ApplicationStarted represents completion of the host's
        // startup sequence, including other lifecycle services.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        // Observe ApplicationStopping instead so shutdown requests
        // are recorded when the host signals them.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        // Do not unsubscribe here: ApplicationStopped is raised later.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _startedRegistration.Dispose();
        _stoppingRegistration.Dispose();
        _stoppedRegistration.Dispose();
    }

    private void OnStarted()
    {
        _startupTimer.Stop();

        double totalMilliseconds =
            _startupTimer.Elapsed.TotalMilliseconds;

        TryEmit(logger =>
            logger.Application.Started(
                totalMilliseconds: totalMilliseconds));
    }

    private void OnStopping()
    {
        // The notification does not identify the shutdown cause.
        TryEmit(static logger =>
            logger.Application.Stopping(reason: "Unknown"));
    }

    private void OnStopped()
    {
        TryEmit(static logger => logger.Application.Stopped());
    }

    private void TryEmit(Action<ILogger> emit)
    {
        try
        {
            emit(_logger);
        }
        catch (Exception)
        {
            // Diagnostic emission must not interrupt the host.
            // Logging this failure through the same pipeline could recurse.
        }
    }
}