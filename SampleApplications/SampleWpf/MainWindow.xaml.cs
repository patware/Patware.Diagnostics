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

    private bool _serviceAvailable = true;
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
    /// Refreshes the simulated price while preserving cached data on timeout.
    /// </summary>
    /// <remarks>
    /// WPF resumes this handler on the dispatcher, keeping cache transitions
    /// and UI updates serialized. Repeated failures emit no additional 401,
    /// and successful startup does not produce recovery event 204.
    /// <para>
    /// Each confirmed timeout emits event 601 with a distinct attempt identifier
    /// and the original exception. Event 401 is emitted only when entering
    /// degraded mode, and event 204 only after a successful refresh restores
    /// normal operation. Shutdown cancellation is not reported as a timeout.
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
            decimal price = await FetchPriceAsync(_serviceAvailable, request.Token);

            // Cancellation can arrive after the service finishes but before
            // the dispatcher resumes this handler.
            request.Token.ThrowIfCancellationRequested();

            if (_closed || _actionRequested)
            {
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

            if (!_isDegraded)
            {
                _isDegraded = true;

                _logger.Application.InDegradedMode(
                    reason: _cachedPrice.HasValue
                        ? "Product service timed out; displaying the cached coffee price."
                        : "Product service timed out; no cached coffee price is available.");
            }

            WarningText.Text = _cachedPrice.HasValue
                ? "We’re having trouble updating prices. Your last saved price "
                  + "is still available below, but may be out of date. "
                  + "Please try refreshing again."
                : "We’re having trouble loading prices and don’t have a saved "
                  + "price to show yet. Please try refreshing again.";

            WarningPanel.Visibility = Visibility.Visible;
            CatalogStatusText.Text = "Refresh timed out.";

            // Leave the displayed price and successful-refresh timestamp intact.
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
    /// Toggles availability for the next simulated service request.
    /// </summary>
    /// <remarks>
    /// Restoring availability does not establish recovery. The warning remains
    /// until a successful refresh replaces the cache and emits event 204.
    /// </remarks>
    private void Availability_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_refreshing || _actionRequested || _closed)
        {
            return;
        }

        _serviceAvailable = !_serviceAvailable;

        AvailabilityButton.Content = _serviceAvailable
            ? "Simulate service outage"
            : "Restore service availability";

        AvailabilityText.Text = _serviceAvailable
            ? "Simulated service: available"
            : "Simulated service: unavailable";

        CatalogStatusText.Text = _serviceAvailable
            ? "Availability restored. Refresh to verify recovery."
            : "Outage enabled. Refresh to exercise the cached fallback.";
    }

    /// <summary>
    /// Simulates fetching a coffee price from an unreliable dependency.
    /// </summary>
    /// <param name="available">Whether this request can succeed.</param>
    /// <param name="cancellationToken">Cancellation for the simulated request.</param>
    /// <returns>The simulated coffee price in CAD.</returns>
    /// <remarks>
    /// An unavailable dependency waits until the caller cancels the request.
    /// This demonstrates a timeout without blocking the WPF dispatcher.
    /// </remarks>
    private static async Task<decimal> FetchPriceAsync(bool available, CancellationToken cancellationToken)
    {
        if (!available)
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
}