using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Patware.ApplicationEvents.Hosting.Tests;

/// <summary>
/// Verifies application events emitted through the real Generic Host.
/// </summary>
/// <remarks>
/// Tests use public registration and host lifecycle APIs.
/// Default configuration sources are disabled to prevent machine settings
/// and local configuration files from changing the results.
/// No console signals, arbitrary delays, or private implementation access
/// are required.
/// </remarks>
[TestClass]
public sealed class ApplicationEventsHostingTests
{
    private const string EventCategory = "Patware.ApplicationEvents.Hosting";

    /// <summary>
    /// Verifies event order and exactly-once emission across host startup
    /// and shutdown.
    /// </summary>
    /// <param name="registrationCount">
    /// The number of times the registration extension is called.
    /// </param>
    /// <remarks>
    /// Repeated registration must not introduce duplicate observers.
    /// Startup duration is checked for validity rather than an exact
    /// value because elapsed time depends on the execution environment.
    /// </remarks>
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task StartAndStop_EmitLifecycleEventsExactlyOnce(int registrationCount)
    {
        // Arrange
        using RecordingLoggerProvider provider = new();
        using IHost host = CreateHost(provider, registrationCount);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));

        // Act
        await host.StartAsync(timeout.Token);
        RecordedLog[] startupEntries = GetApplicationEvents(provider);

        await host.StopAsync(timeout.Token);
        RecordedLog[] completedEntries = GetApplicationEvents(provider);

        // Assert
        AssertEventIds(startupEntries, 101, 102);
        AssertEventIds(completedEntries, 101, 102, 103, 104);

        CollectionAssert.AreEqual(
            new[]
            {
                "Application_Starting",
                "Application_Started",
                "Application_Stopping",
                "Application_Stopped"
            },
            completedEntries
                .Select(entry => entry.EventId.Name)
                .ToArray());

        foreach (RecordedLog entry in completedEntries)
        {
            Assert.AreEqual(LogLevel.Information, entry.Level);
            Assert.IsNull(entry.Exception);
        }

        object? duration = completedEntries[1].Properties["TotalMilliseconds"];

        Assert.IsInstanceOfType<double>(duration);

        double milliseconds = (double)duration!;
        Assert.IsTrue(double.IsFinite(milliseconds));
        Assert.IsTrue(milliseconds >= 0);

        Assert.AreEqual(
            "Unknown",
            (string?)completedEntries[2].Properties["Reason"]);
    }

    /// <summary>
    /// Verifies that requesting shutdown emits Stopping and completing
    /// shutdown emits Stopped.
    /// </summary>
    /// <remarks>
    /// Repeated shutdown requests must not duplicate event 103.
    /// Calling StopApplication alone does not complete the host shutdown;
    /// this test explicitly calls StopAsync afterward, as an application
    /// managing its host lifecycle must do.
    /// </remarks>
    [TestMethod]
    public async Task StopApplication_EmitsStoppingBeforeShutdownCompletes()
    {
        // Arrange
        using RecordingLoggerProvider provider = new();
        using IHost host = CreateHost(provider);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));

        IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        await host.StartAsync(timeout.Token);

        // Act
        lifetime.StopApplication();
        lifetime.StopApplication();

        RecordedLog[] requestedEntries = GetApplicationEvents(provider);

        await host.StopAsync(timeout.Token);
        RecordedLog[] completedEntries = GetApplicationEvents(provider);

        // Assert
        AssertEventIds(requestedEntries, 101, 102, 103);
        AssertEventIds(completedEntries, 101, 102, 103, 104);
        Assert.IsTrue(lifetime.ApplicationStopped.IsCancellationRequested);
    }

    private static IHost CreateHost(RecordingLoggerProvider provider, int registrationCount = 1)
    {
        HostApplicationBuilder builder = new(
            new HostApplicationBuilderSettings
            {
                DisableDefaults = true,
                EnvironmentName = Environments.Production
            });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(provider);

        // Keep the real host lifecycle while avoiding process-wide
        // console signal handlers in the test runner.
        builder.Services.AddSingleton<IHostLifetime>(new TestHostLifetime());

        for (int index = 0; index < registrationCount; index++)
        {
            builder.AddPatwareApplicationEvents();
        }

        return builder.Build();
    }

    private static RecordedLog[] GetApplicationEvents(RecordingLoggerProvider provider)
    {
        return provider.Entries
            .Where(entry => entry.Category == EventCategory)
            .ToArray();
    }

    private static void AssertEventIds(RecordedLog[] entries, params int[] expectedIds)
    {
        CollectionAssert.AreEqual(
            expectedIds,
            entries.Select(entry => entry.EventId.Id).ToArray(),
            "Application events must occur in order without duplicates.");
    }

    private sealed class TestHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}