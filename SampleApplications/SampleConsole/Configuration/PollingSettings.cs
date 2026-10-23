namespace SampleConsole.Configuration;

/// <summary>
/// Represents a candidate keyboard-polling configuration.
/// </summary>
/// <param name="PollingIntervalMilliseconds">
/// The delay between keyboard checks, in milliseconds.
/// </param>
/// <remarks>
/// Deserialization creates a candidate. The application validates its range
/// before replacing the active configuration.
/// </remarks>
public sealed record PollingSettings(int PollingIntervalMilliseconds);