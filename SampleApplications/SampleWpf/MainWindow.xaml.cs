using System.Windows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace SampleWpf;

/// <summary>
/// Provides controls for exercising application lifecycle and failure events.
/// </summary>
/// <remarks>
/// Graceful shutdown uses the existing host-to-dispatcher integration.
/// The fatal scenario throws on a dedicated thread so the exception reaches
/// the runtime notification observed by the Patware hosting integration.
/// </remarks>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private bool _actionRequested;

    private SimulatedServiceMode _serviceMode = SimulatedServiceMode.Available;
    private bool _refreshing;
    private bool _isDegraded;

    private bool _closed;

    private decimal? _cachedPrice;
    private DateTimeOffset? _cachedAt;
    private CancellationTokenSource? _activeRequest;

    /// <summary>
    /// Initializes the window with its logging and host lifetime services.
    /// </summary>
    /// <param name="logger">The logger for window activity.</param>
    /// <param name="lifetime">The host lifetime used to request shutdown.</param>
    /// <exception cref="ArgumentNullException">
    /// The logger or lifetime is null.
    /// </exception>
    public MainWindow(ILogger<MainWindow> logger, IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(lifetime);

        _logger = logger;
        _lifetime = lifetime;

        InitializeComponent();
    }

    /// <summary>
    /// Triggers a simulated fatal exception on a dedicated thread.
    /// </summary>
    /// <remarks>
    /// The exception is intentionally neither caught nor stored in a Task.
    /// This exercises the runtime notification rather than a UI exception
    /// handler. Event 701 is emitted by the registered observer.
    /// </remarks>
    private void Exception_Button_Click(object sender, RoutedEventArgs e)
    {
        if (!BeginAction("Triggering a fatal exception..."))
        {
            return;
        }

        _logger.LogInformation("Fatal exception simulation requested from the WPF window.");

        Thread crashThread = new(static () =>
        {
            throw new InvalidOperationException(
                "Simulated fatal exception triggered from the WPF sample.",
                new ArgumentException("Simulated underlying failure."));
        })
        {
            Name = "WpfSimulatedFatalException",
            IsBackground = true
        };

        crashThread.Start();
    }

    /// <summary>
    /// Requests graceful shutdown through the host.
    /// </summary>
    /// <remarks>
    /// Program forwards the host's stopping notification to the WPF
    /// dispatcher. After the UI exits, Program completes host shutdown.
    /// </remarks>
    private void Exit_Button_Click(object sender, RoutedEventArgs e)
    {
        if (!BeginAction("Requesting graceful shutdown..."))
        {
            return;
        }

        _logger.LogInformation("Graceful shutdown requested from the WPF window.");

        _lifetime.StopApplication();
    }

    /// <summary>
    /// Prevents further interaction while a terminal action is requested.
    /// </summary>
    /// <param name="status">The status displayed to the user.</param>
    /// <returns>
    /// True when this call begins the action; false when one is already pending.
    /// </returns>
    /// <remarks>
    /// Canceling the refresh before shutdown prevents a pending timeout from
    /// being interpreted as a dependency failure.
    /// </remarks>
    private bool BeginAction(string status)
    {
        if (_actionRequested || _closed)
        {
            return false;
        }

        _actionRequested = true;
        _activeRequest?.Cancel();

        RefreshButton.IsEnabled = false;
        AvailabilityButton.IsEnabled = false;
        ExceptionButton.IsEnabled = false;
        ExitButton.IsEnabled = false;
        StatusText.Text = status;

        return true;
    }


    /// <summary>
    /// Refreshes the simulated price while preserving cached data on failure.
    /// </summary>
    /// <remarks>
    /// WPF resumes this handler on the dispatcher, keeping cache transitions
    /// and UI updates serialized. Repeated failures emit no additional 401,
    /// and successful startup does not produce recovery event 204.
    /// <para>
    /// Confirmed timeouts emit event 601; explicit unavailable results emit
    /// event 602 without an exception. Each failed attempt has its own identifier.
    /// Event 401 occurs only when entering degraded mode, and event 204 occurs
    /// only after a successful refresh. Shutdown cancellation is handled separately.
    /// </para>
    /// </remarks>
    private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_refreshing || _actionRequested || _closed)
        {
            return;
        }

        _refreshing = true;
        RefreshButton.IsEnabled = false;
        AvailabilityButton.IsEnabled = false;
        CatalogStatusText.Text = "Contacting the product service…";

        string operationId = Guid.NewGuid().ToString("N");
        TimeSpan timeoutBudget = TimeSpan.FromSeconds(2);

        using CancellationTokenSource timeout = new(timeoutBudget);

        using CancellationTokenSource request = CancellationTokenSource
            .CreateLinkedTokenSource(timeout.Token, _lifetime.ApplicationStopping);

        _activeRequest = request;

        try
        {
            decimal? result = await FetchPriceAsync(_serviceMode, request.Token);

            // Cancellation can arrive after the service finishes but before
            // the dispatcher resumes this handler.
            request.Token.ThrowIfCancellationRequested();

            if (_closed || _actionRequested)
            {
                return;
            }

            if (result is not decimal price)
            {
                _logger.External.ServiceUnavailable(
                    externalName: "ProductService",
                    operationName: "FetchPrice",
                    operationId: operationId,
                    reasonCode: "ServiceUnavailable",
                    exception: null);

                ShowFailedRefresh(
                    failure: "Product service reported unavailability",
                    status: "Service unavailable.");

                return;
            }

            _cachedPrice = price;
            _cachedAt = DateTimeOffset.UtcNow;

            if (_isDegraded)
            {
                _isDegraded = false;
                _logger.Application.InNormalMode();
            }

            WarningPanel.Visibility = Visibility.Collapsed;
            PriceText.Text = $"CAD {price:F2}";
            UpdatedText.Text = $"Last successful refresh: {_cachedAt.Value:yyyy-MM-dd HH:mm:ss} UTC";
            CatalogStatusText.Text = "Price refreshed successfully.";
        }
        catch (OperationCanceledException)
            when (_closed
                  || _actionRequested
                  || _lifetime.ApplicationStopping.IsCancellationRequested)
        {
            // Closing or shutting down is neither degradation nor recovery.
        }
        catch (OperationCanceledException exception)
            when (timeout.IsCancellationRequested)
        {

            _logger.External.OperationTimedOut(
                externalName: "ProductService",
                operationName: "FetchPrice",
                operationId: operationId,
                timeoutMilliseconds: timeoutBudget.TotalMilliseconds,
                exception: exception);

            ShowFailedRefresh(
                failure: "Product service timed out",
                status: "Refresh timed out.");
        }
        catch (Exception exception)
        {
            // Exceptions escaping an async void handler reach the dispatcher.
            // Report unexpected refresh failures separately from simulated timeouts.
            _logger.LogError(exception, "Unexpected product refresh failure.");

            if (!_closed && !_actionRequested)
            {
                CatalogStatusText.Text = "An unexpected refresh error occurred. See the Debug output.";
            }
        }
        finally
        {
            _activeRequest = null;
            _refreshing = false;

            if (!_closed
                && !_actionRequested
                && !_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                RefreshButton.IsEnabled = true;
                AvailabilityButton.IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// Cycles the behavior of subsequent simulated requests.
    /// </summary>
    /// <remarks>
    /// Changing the simulation does not establish recovery. A successful
    /// refresh must replace the cache before event 204 is emitted.
    /// </remarks>
    private void Availability_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_refreshing || _actionRequested || _closed)
        {
            return;
        }

        _serviceMode = _serviceMode switch
        {
            SimulatedServiceMode.Available => SimulatedServiceMode.Unresponsive,
            SimulatedServiceMode.Unresponsive => SimulatedServiceMode.Unavailable,
            _ => SimulatedServiceMode.Available
        };

        AvailabilityButton.Content = _serviceMode switch
        {
            SimulatedServiceMode.Available => "Simulate timeout",
            SimulatedServiceMode.Unresponsive => "Simulate immediate unavailability",
            _ => "Restore service availability"
        };

        AvailabilityText.Text = $"Simulated service: {_serviceMode}";
        CatalogStatusText.Text = "Refresh to observe the selected service behavior.";
    }

    /// <summary>
    /// Simulates a coffee-price request using the selected dependency behavior.
    /// </summary>
    /// <param name="mode">The behavior captured when the request begins.</param>
    /// <param name="cancellationToken">Cancellation for this request.</param>
    /// <returns>
    /// A coffee price, or null when the service explicitly reports unavailability.
    /// </returns>
    /// <remarks>
    /// Unavailable mode returns a normal result without manufacturing an
    /// exception. Unresponsive mode waits for the caller's cancellation.
    /// </remarks>
    private static async Task<decimal?> FetchPriceAsync(
        SimulatedServiceMode mode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (mode == SimulatedServiceMode.Unavailable)
        {
            return null;
        }

        if (mode == SimulatedServiceMode.Unresponsive)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);

        return 12m + Random.Shared.Next(0, 501) / 100m;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _closed = true;

        // Closing the main window can precede the host's stopping notification.
        // Cancel immediately so the refresh cannot report a false outage.
        _activeRequest?.Cancel();

        base.OnClosed(e);
    }

    /// <summary>
    /// Displays a failed refresh while preserving the previous cached value.
    /// </summary>
    /// <param name="failure">The dependency failure used in event 401.</param>
    /// <param name="status">The refresh outcome displayed in the window.</param>
    /// <remarks>
    /// Called on the dispatcher. Failure-kind changes do not repeat event 401
    /// or alter the successful-refresh timestamp.
    /// </remarks>
    private void ShowFailedRefresh(string failure, string status)
    {
        if (!_isDegraded)
        {
            _isDegraded = true;

            _logger.Application.InDegradedMode(
                reason: _cachedPrice.HasValue
                    ? $"{failure}; displaying the cached coffee price."
                    : $"{failure}; no cached coffee price is available.");
        }

        WarningText.Text = _cachedPrice.HasValue
            ? "We’re having trouble updating prices. Your last saved price " +
              "is still available below, but may be out of date. " +
              "Please try refreshing again."
            : "We’re having trouble loading prices and don’t have a saved " +
              "price to show yet. Please try refreshing again.";

        WarningPanel.Visibility = Visibility.Visible;
        CatalogStatusText.Text = status;
    }
}
