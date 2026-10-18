using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents.Hosting;

namespace SampleWpf;

/// <summary>
/// Creates the host and runs the WPF application.
/// </summary>
/// <remarks>
/// WPF owns the UI thread and dispatcher. Hosted services in this sample
/// run independently of the UI and must not require the dispatcher during
/// startup or shutdown.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Runs the desktop application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The application exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // WinExe has no normal console window. View logging in the
        // Visual Studio Debug output while debugging.
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();

        builder.AddPatwareApplicationEvents(options =>
        {
            options.LogUnhandledExceptions = true;
        });

        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<MainWindow>();

        using IHost host = builder.Build();

        App application = host.Services.GetRequiredService<App>();
        application.InitializeComponent();

        IHostApplicationLifetime lifetime =
            host.Services.GetRequiredService<IHostApplicationLifetime>();

        using CancellationTokenRegistration stoppingRegistration =
            lifetime.ApplicationStopping.Register(() =>
            {
                // A worker can request host shutdown from any thread.
                // Forward that request to WPF's UI thread.
                if (!application.Dispatcher.HasShutdownStarted)
                {
                    application.Dispatcher.BeginInvoke(
                        new Action(() => application.Shutdown()));
                }
            });

        try
        {
            // Keep hosted-service execution independent of WPF's
            // synchronization context while preserving the STA UI thread.
            Task.Run(() => host.StartAsync()).GetAwaiter().GetResult();

            MainWindow mainWindow =
                host.Services.GetRequiredService<MainWindow>();

            return application.Run(mainWindow);
        }
        finally
        {
            // WPF does not await an async OnExit override.
            // Keep the process alive until host shutdown completes.
            Task.Run(() => host.StopAsync()).GetAwaiter().GetResult();
        }
    }
}