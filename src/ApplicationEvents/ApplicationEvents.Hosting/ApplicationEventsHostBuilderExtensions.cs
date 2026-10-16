using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Patware.ApplicationEvents.Hosting;

/// <summary>
/// Registers automatic application events with a host builder.
/// </summary>
public static class ApplicationEventsHostBuilderExtensions
{
    /// <summary>
    /// Adds automatic application lifecycle event logging.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The supplied builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// The builder is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Registers one lifecycle observer. Repeated calls do not register
    /// additional instances of that observer.
    /// </para>
    /// <para>
    /// Events use the application's existing logging pipeline.
    /// Registration does not add providers or change logging filters.
    /// </para>
    /// <para>
    /// Register early when possible. Startup timing begins when the
    /// observer's StartingAsync callback runs and ends when the host
    /// raises ApplicationStarted.
    /// </para>
    /// </remarks>
    public static TBuilder AddPatwareApplicationEvents<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddHostedService<ApplicationEventsLifecycleService>();

        return builder;
    }
}