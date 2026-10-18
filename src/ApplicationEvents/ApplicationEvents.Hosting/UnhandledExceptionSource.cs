namespace Patware.Diagnostics.ApplicationEvents.Hosting;

/// <summary>
/// Provides runtime unhandled-exception notifications.
/// </summary>
/// <remarks>
/// This abstraction permits isolated tests without raising real
/// process-terminating exceptions.
/// </remarks>
internal interface IUnhandledExceptionSource
{
    /// <summary>
    /// Occurs when the runtime reports an unhandled exception.
    /// </summary>
    event UnhandledExceptionEventHandler? UnhandledException;
}

/// <summary>
/// Connects observers to the current application domain.
/// </summary>
internal sealed class AppDomainUnhandledExceptionSource : IUnhandledExceptionSource
{
    /// <inheritdoc />
    public event UnhandledExceptionEventHandler? UnhandledException
    {
        add => AppDomain.CurrentDomain.UnhandledException += value;
        remove => AppDomain.CurrentDomain.UnhandledException -= value;
    }
}