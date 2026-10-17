using System.Windows;

using Microsoft.Extensions.Logging;

namespace SampleWpf;

/// <summary>
/// Represents the WPF application and observes its UI lifecycle.
/// </summary>
/// <remarks>
/// The Generic Host creates this instance through dependency injection.
/// WPF notifications describe UI activity; the Patware hosting observer
/// separately owns the automatic host lifecycle events 101–104.
/// </remarks>
public partial class App : Application
{
    private readonly ILogger<App> _logger;

    /// <summary>
    /// Initializes the application with its logger.
    /// </summary>
    /// <param name="logger">The logger for WPF application activity.</param>
    /// <exception cref="ArgumentNullException">
    /// The logger is null.
    /// </exception>
    public App(ILogger<App> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        _logger.LogInformation("[App.xaml.cs] --> WPF application startup.");
        base.OnStartup(e);
    }

    /// <inheritdoc />
    protected override void OnActivated(EventArgs e)
    {
        _logger.LogDebug("[App.xaml.cs] --> WPF application activated.");
        base.OnActivated(e);
    }

    /// <inheritdoc />
    protected override void OnDeactivated(EventArgs e)
    {
        _logger.LogDebug("[App.xaml.cs] --> WPF application deactivated.");
        base.OnDeactivated(e);
    }

    /// <inheritdoc />
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _logger.LogInformation("[App.xaml.cs] --> Windows session is ending. Reason: {Reason}", e.ReasonSessionEnding);

        base.OnSessionEnding(e);
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _logger.LogInformation("[App.xaml.cs] --> WPF application exiting with code {ExitCode}.", e.ApplicationExitCode);

        base.OnExit(e);
    }
}