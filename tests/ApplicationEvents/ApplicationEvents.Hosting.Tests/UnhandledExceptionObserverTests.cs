using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents.Hosting;

namespace ApplicationEvents.Hosting.Tests;

/// <summary>
/// Verifies opt-in runtime exception reporting and subscription ownership.
/// </summary>
/// <remarks>
/// Tests use a private notification source. They do not attach handlers
/// to AppDomain or raise actual unhandled exceptions.
/// </remarks>
[TestClass]
public sealed class UnhandledExceptionObserverTests
{
    /// <summary>
    /// Verifies opt-in registration, duplicate registration, and disposal.
    /// </summary>
    /// <param name="enabled">Whether exception observation is enabled.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Registration_ObservesOnlyWhenEnabled(bool enabled)
    {
        // Arrange
        FakeExceptionSource source = new();
        using RecordingLoggerProvider provider = new();

        HostApplicationBuilder builder = new(
            new HostApplicationBuilderSettings
            {
                DisableDefaults = true
            });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(provider);

        builder.Services.AddSingleton<IUnhandledExceptionSource>(source);
        builder.Services.AddSingleton<IHostLifetime>(new TestHostLifetime());

        builder.AddPatwareApplicationEvents(options =>
            options.LogUnhandledExceptions = enabled);

        builder.AddPatwareApplicationEvents(options =>
            options.LogUnhandledExceptions = enabled);

        using IHost host = builder.Build();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));

        InvalidOperationException exception = new("Fatal test failure.");

        // Act
        int subscribersBeforeStart = source.SubscriberCount;

        await host.StartAsync(timeout.Token);
        int subscribersAfterStart = source.SubscriberCount;

        source.Raise(exception, isTerminating: true);

        await host.StopAsync(timeout.Token);
        int subscribersAfterStop = source.SubscriberCount;

        host.Dispose();
        int subscribersAfterDispose = source.SubscriberCount;

        // Disposed hosts must not receive subsequent notifications.
        source.Raise(new Exception("After disposal"), isTerminating: true);

        // Assert
        int expectedCount = enabled ? 1 : 0;

        Assert.AreEqual(0, subscribersBeforeStart);
        Assert.AreEqual(expectedCount, subscribersAfterStart);
        Assert.AreEqual(expectedCount, subscribersAfterStop);
        Assert.AreEqual(0, subscribersAfterDispose);

        RecordedLog[] failures = provider.Entries
            .Where(entry => entry.EventId.Id == 701)
            .ToArray();

        Assert.HasCount(expectedCount, failures);

        if (enabled)
        {
            RecordedLog failure = failures[0];

            Assert.AreEqual(
                "Patware.Diagnostics.ApplicationEvents.Hosting",
                failure.Category);
            Assert.AreEqual("Application_UnhandledException", failure.EventId.Name);
            Assert.AreEqual(LogLevel.Critical, failure.Level);
            Assert.AreSame(exception, failure.Exception);
        }
    }

    /// <summary>
    /// Verifies that notifications outside event 701's contract are ignored.
    /// </summary>
    [TestMethod]
    public async Task Notification_WhenNonterminatingOrNotException_IsIgnored()
    {
        // Arrange
        FakeExceptionSource source = new();
        using RecordingLoggerProvider provider = new();
        using ILoggerFactory factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        using UnhandledExceptionObserver observer = new(source, factory);
        await observer.StartAsync(CancellationToken.None);

        // Act
        source.Raise(new Exception("Nonterminating"), isTerminating: false);
        source.Raise("Not an Exception instance", isTerminating: true);

        // Assert
        Assert.HasCount(0, provider.Entries);
    }

    /// <summary>
    /// Verifies that logging failures do not escape the notification handler.
    /// </summary>
    [TestMethod]
    public async Task Notification_WhenProviderThrows_DoesNotPropagate()
    {
        // Arrange
        FakeExceptionSource source = new();
        using ThrowingProvider provider = new();
        using ILoggerFactory factory = LoggerFactory.Create(logging => logging.AddProvider(provider));

        using UnhandledExceptionObserver observer = new(source, factory);
        await observer.StartAsync(CancellationToken.None);

        // Act
        source.Raise(new Exception("Original failure"), isTerminating: true);

        // Assert
        Assert.AreEqual(1, provider.Attempts);
    }

    private sealed class FakeExceptionSource : IUnhandledExceptionSource
    {
        public event UnhandledExceptionEventHandler? UnhandledException;

        internal int SubscriberCount => UnhandledException?.GetInvocationList().Length ?? 0;

        internal void Raise(object exception, bool isTerminating)
        {
            UnhandledException?.Invoke(
                this,
                new UnhandledExceptionEventArgs(exception, isTerminating));
        }
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

    private sealed class ThrowingProvider : ILoggerProvider
    {
        internal int Attempts { get; private set; }

        public ILogger CreateLogger(string categoryName)
        {
            return new ThrowingLogger(this);
        }

        public void Dispose()
        {
        }

        private sealed class ThrowingLogger(ThrowingProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.Attempts++;
                throw new InvalidOperationException("Provider failed.");
            }
        }
    }
}