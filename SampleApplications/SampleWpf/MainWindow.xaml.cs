using System.Windows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleWpf;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public MainWindow(ILogger<MainWindow> logger, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
        InitializeComponent();
    }

    private void Exit_Button_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("[MainWindow.xaml.cs] Exit button clicked, requesting graceful shutdown");
        _lifetime.StopApplication();
        _logger.LogInformation("[MainWindow.xaml.cs] graceful shutdown requested");
    }
}