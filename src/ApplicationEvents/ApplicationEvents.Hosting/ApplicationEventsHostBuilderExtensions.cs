using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents.Hosting.Formatting;

namespace Patware.Diagnostics.ApplicationEvents.Hosting;

/// <summary>
/// Registers automatic application events with a host builder.
/// </summary>
public static class ApplicationEventsHostBuilderExtensions
{
    /// <summary>
    /// Adds automatic application lifecycle events using default options.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The supplied builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// The builder is null.
    /// </exception>
    /// <remarks>
    /// Default registration preserves the application's logging format.
    /// </remarks>
    public static TBuilder AddPatwareApplicationEvents<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        return builder.AddPatwareApplicationEvents(static _ => { });
    }

    /// <summary>
    /// Adds automatic application lifecycle events with explicit options.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">Configures registration options.</param>
    /// <returns>The supplied builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// The builder or configuration delegate is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Repeated registration does not add duplicate lifecycle observers.
    /// </para>
    /// <para>
    /// Enabling formatting selects the Patware console formatter without
    /// removing other providers or changing logging filters.
    /// </para>
    /// <para>
    /// A subsequent call with formatting disabled does not undo an earlier
    /// formatter selection. Configure the feature once during startup.
    /// </para>
    /// </remarks>
    public static TBuilder AddPatwareApplicationEvents<TBuilder>(
        this TBuilder builder,
        Action<ApplicationEventsOptions> configure)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        ApplicationEventsOptions options = new();
        configure(options);

        builder.Services
            .AddHostedService<ApplicationEventsLifecycleService>();

        if (options.FormatLogs)
        {
            builder.Logging.AddConsoleFormatter<
                EventIdConsoleFormatter,
                EventIdConsoleFormatterOptions>();

            builder.Logging.AddConsole(consoleOptions =>
            {
                consoleOptions.FormatterName = EventIdConsoleFormatter.FormatterName;
            });
        }

        return builder;
    }
}