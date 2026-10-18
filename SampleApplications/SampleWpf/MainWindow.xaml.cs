using System.Windows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    private bool BeginAction(string status)
    {
        if (_actionRequested)
        {
            return false;
        }

        _actionRequested = true;
        ExceptionButton.IsEnabled = false;
        ExitButton.IsEnabled = false;
        StatusText.Text = status;

        return true;
    }
}