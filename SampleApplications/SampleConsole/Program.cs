using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;
using Patware.Diagnostics.ApplicationEvents.Hosting;

using SampleConsole.Configuration;
using SampleConsole.Connections;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddApplicationEvents(options =>
{
    options.FormatLogs = true;
    options.LogUnhandledExceptions = true;
});

// Read once before registration. Changing this option requires restarting
// the application because the built service provider is not reconfigured.
bool demonstrateFailureAtStartup =
    builder.Configuration.GetValue<bool>(
        "Samples:DependencyResolution:FailAtStartup");

if (demonstrateFailureAtStartup)
{
    Console.WriteLine("Simulation: Skipping the registration of the IReportFormatterSample to demonstrate a 504 failure");
}
else
{
    Console.WriteLine("Registering the IReportFormatterSample service.");
    Console.WriteLine("If you want to simulate a 504, restart this argument:  --Samples:DependencyResolution:FailAtStartup=true");
    builder.Services.AddSingleton<
        SampleConsole.Dependencies.IReportFormatterSample,
        SampleConsole.Dependencies.ReportFormatterSample>();
}


using IHost host = builder.Build();

ILogger logger = host.Services.GetRequiredService<ILogger<Program>>();

ReloadablePollingConfiguration pollingConfiguration = new(logger, Path.Combine(AppContext.BaseDirectory, "sample-polling.json"));

pollingConfiguration.TryReload();

IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

const string echoConfigurationName = "WebSocketEcho";
const string endpointSettingName = "Samples:WebSocket:Endpoint";

string webSocketEndpointText = builder.Configuration[endpointSettingName] ?? "wss://ws.postman-echo.com/raw";

string echoValidationId = Guid.NewGuid().ToString("N");

logger.Configuration.Validating(echoConfigurationName, echoValidationId);

Uri? webSocketEndpoint = null;

if (Uri.TryCreate(webSocketEndpointText, UriKind.Absolute, out Uri? configuredEndpoint)
    && (configuredEndpoint.Scheme == "ws" || configuredEndpoint.Scheme == "wss")
    && !string.IsNullOrWhiteSpace(configuredEndpoint.Host))
{
    logger.Configuration.ValueIsValid(echoConfigurationName, echoValidationId, settingName: endpointSettingName);

    logger.Configuration.Validated(echoConfigurationName, echoValidationId);

    // Application policy: apply only the accepted candidate.
    webSocketEndpoint = configuredEndpoint;
}
else
{
    const string reason = "Endpoint must be an absolute ws:// or wss:// address with a host";

    logger.Configuration.ValueIsInvalid(echoConfigurationName, echoValidationId, settingName: endpointSettingName, reason: reason);

    logger.Configuration.ValidationFailed(echoConfigurationName, echoValidationId, reason: reason);
}

SampleConsole.Dependencies.IReportFormatterSample reportFormatter;

string resolutionId = Guid.NewGuid().ToString("N");

logger.Dependency.Resolving(
    dependencyName: nameof(SampleConsole.Dependencies.IReportFormatterSample),
    operationName: "Program Startup",
    resolutionId: resolutionId);

try
{

    reportFormatter = host.Services.GetRequiredService<SampleConsole.Dependencies.IReportFormatterSample>();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    logger.Dependency.ResolutionFailed(
        dependencyName: nameof(SampleConsole.Dependencies.IReportFormatterSample),
        operationName: "Program Startup",
        resolutionId: resolutionId,
        reasonCode: "ResolutionThrew",
        exception: null); // exception: if null, call stack not displayed, else yes.

    logger.LogDebug(
        ex,
        "Dependency resolution exception details. Resolution: {ResolutionId}.",
        resolutionId);

    //throw;  // stop with an exception

    Environment.ExitCode = 1;
    return;
}

logger.Dependency.Resolved(
    dependencyName: nameof(SampleConsole.Dependencies.IReportFormatterSample),
    operationName: "Program Startup",
    resolutionId: resolutionId);

// Complete startup before accepting input so the exception observer
// is subscribed before the simulated failure can occur.
await host.StartAsync();



// This task waits for a shutdown request and executes host shutdown.
Task shutdown = host.WaitForShutdownAsync();

if (Console.IsInputRedirected)
{
    Console.Write("Keyboard commands are unavailable because input is redirected.");

    await shutdown;
    return;
}

Console.WriteLine();
Console.WriteLine("Application is running.");
Console.WriteLine($"Configuration file: {pollingConfiguration.SourcePath}");
Console.WriteLine($"Active polling interval: {pollingConfiguration.Current.PollingIntervalMilliseconds}ms");
Console.WriteLine("  R       Reload polling configuration");
Console.WriteLine("  E       Connect to the WebSocket Echo service");
Console.WriteLine("  G       Generate a report - simulate unresolved DI");
Console.WriteLine("  D       Simulate App in degraded mode");
Console.WriteLine("  N       Simulate App in back to normal mode");
Console.WriteLine("  X       Simulate an unhandled exception (terminates the process)");
Console.WriteLine("  Q       Shut down gracefully");
Console.WriteLine("  Ctrl+C  Shut down gracefully");
Console.WriteLine();

// The sample owns transition detection; event methods only emit records.
bool isDegraded = false;

while (!shutdown.IsCompleted)
{
    // Poll briefly instead of blocking in ReadKey, so Ctrl+C can complete
    // shutdown without requiring another key press.
    if (!Console.KeyAvailable)
    {
        await Task.WhenAny(shutdown, Task.Delay(pollingConfiguration.Current.PollingIntervalMilliseconds));

        continue;
    }

    ConsoleKey key = Console.ReadKey(intercept: true).Key;

    if (key == ConsoleKey.R)
    {
        bool applied = pollingConfiguration.TryReload();

        Console.WriteLine(applied
            ? "Configuration loaded, validated, and applied."
            : "Reload failed; retaining the active configuration.");

        Console.WriteLine(
            $"Active polling interval: " +
            $"{pollingConfiguration.Current.PollingIntervalMilliseconds} ms");

        continue;
    }

    if (key == ConsoleKey.E)
    {
        if (webSocketEndpoint is null)
        {
            Console.WriteLine("Configure Samples:WebSocket:Endpoint with an absolute ws:// or wss:// address.");

            continue;
        }

        try
        {
            await WebSocketConnectionSample.RunAsync(logger, webSocketEndpoint, lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
            when (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            break;
        }

        continue;
    }


    if (key == ConsoleKey.G)
    {
        Console.WriteLine(reportFormatter.GenerateReport());
        continue;
    }


    if (key == ConsoleKey.D)
    {
        if (isDegraded)
        {
            Console.WriteLine("Already in degraded mode.  Press N to recover.");
            continue;
        }

        isDegraded = true;

        Console.WriteLine("Simulating Degraded mode  (just hit N to simulate it's running in normal mode ;)");

        logger.Application.InDegradedMode("Network is down, can't connect to service XYZ");

        continue;
    }

    if (key == ConsoleKey.N)
    {
        if (!isDegraded)
        {
            Console.WriteLine("Operating within normal specifications");
            continue;
        }

        isDegraded = false;

        Console.WriteLine("Simulating that we're Back to normal");
        logger.Application.InNormalMode();

        continue;
    }


    if (key == ConsoleKey.Q)
    {
        Console.WriteLine("Requesting graceful shutdown...");
        lifetime.StopApplication();
        break;
    }

    if (key == ConsoleKey.X)
    {
        Console.WriteLine("Triggering an unhandled exception...");

        // A dedicated thread lets the exception reach the runtime directly.
        // Do not catch it or replace this with Task.Run: this scenario
        // specifically exercises AppDomain.UnhandledException.
        Thread crashThread = new(static () =>
        {
            throw new InvalidOperationException(
                "Simulated fatal exception triggered by pressing X.",
                new ArgumentException("Simulated underlying failure."));
        })
        {
            Name = "SimulatedFatalException",
            IsBackground = true
        };

        crashThread.Start();

        // Keep the host and its observer alive until the runtime terminates
        // the process. Do not initiate graceful shutdown for this scenario.
        break;
    }
}

await shutdown;