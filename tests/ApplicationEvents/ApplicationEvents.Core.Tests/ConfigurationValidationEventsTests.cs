using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace ApplicationEvents.Core.Tests;

/// <summary>
/// Verifies the public contracts of configuration validation events.
/// </summary>
/// <remarks>
/// Expected identities, templates, and properties are independent of the
/// catalog so accidental contract changes cause failures.
/// These tests verify explicit emission, not configuration validation logic.
/// </remarks>
[TestClass]
public sealed class ConfigurationValidationEventsTests
{
    private const string ConfigurationName = "ProductService";
    private const string ValidationId = "attempt-001";
    private const string SettingName = "TimeoutSeconds";
    private const string Reason = "Must satisfy {RequiredRange}.";

    /// <summary>
    /// Verifies event identity, severity, template, and structured arguments.
    /// </summary>
    /// <param name="eventId">The event method to exercise.</param>
    /// <param name="eventName">The expected symbolic identity.</param>
    /// <param name="level">The expected severity.</param>
    /// <param name="template">The expected message template.</param>
    /// <remarks>
    /// Exact property counts reject accidental payload additions.
    /// Braces in a reason remain argument data rather than template syntax.
    /// </remarks>
    [TestMethod]
    [DataRow(
        110,
        "Configuration_Validating",
        LogLevel.Information,
        "Validating configuration {ConfigurationName}. Validation: {ValidationId}.")]
    [DataRow(
        111,
        "Configuration_ValueIsValid",
        LogLevel.Information,
        "Setting {SettingName} in configuration {ConfigurationName} passed validation. Validation: {ValidationId}.")]
    [DataRow(
        112,
        "Configuration_Validated",
        LogLevel.Information,
        "Configuration {ConfigurationName} passed validation. Validation: {ValidationId}.")]
    [DataRow(
        506,
        "Configuration_ValueIsInvalid",
        LogLevel.Warning,
        "Setting {SettingName} in configuration {ConfigurationName} failed validation. Reason: {Reason}. Validation: {ValidationId}.")]
    [DataRow(
        507,
        "Configuration_ValidationFailed",
        LogLevel.Warning,
        "Configuration {ConfigurationName} failed validation. Reason: {Reason}. Validation: {ValidationId}.")]
    public void ConfigurationEvent_EmitsExpectedContract(
        int eventId,
        string eventName,
        LogLevel level,
        string template)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel: level);

        Dictionary<string, object?> expectedProperties = new()
        {
            ["ConfigurationName"] = ConfigurationName,
            ["ValidationId"] = ValidationId,
            ["{OriginalFormat}"] = template
        };

        if (eventId is 111 or 506)
        {
            expectedProperties["SettingName"] = SettingName;
        }

        if (eventId is 506 or 507)
        {
            expectedProperties["Reason"] = Reason;
        }

        // Act
        Emit(logger, eventId);

        // Assert
        Assert.HasCount(1, logger.Entries);

        RecordedLog entry = logger.Entries[0];

        Assert.AreEqual(eventId, entry.EventId.Id);
        Assert.AreEqual(eventName, entry.EventId.Name);
        Assert.AreEqual(level, entry.Level);
        Assert.IsNull(entry.Exception);
        Assert.HasCount(expectedProperties.Count, entry.Properties);

        foreach (KeyValuePair<string, object?> property in expectedProperties)
        {
            Assert.IsTrue(
                entry.Properties.TryGetValue(property.Key, out object? actual),
                $"Missing structured property '{property.Key}'.");

            Assert.AreEqual(property.Value, actual);
        }
    }

    /// <summary>
    /// Verifies that disabled event levels do not invoke the logger.
    /// </summary>
    /// <param name="eventId">The event method to exercise.</param>
    /// <param name="minimumLevel">The logger's configured minimum level.</param>
    /// <remarks>
    /// RecordingLogger captures every Log invocation without filtering,
    /// so this verifies filtering performed by the generated emitters.
    /// </remarks>
    [TestMethod]
    [DataRow(110, LogLevel.Warning)]
    [DataRow(111, LogLevel.Warning)]
    [DataRow(112, LogLevel.Warning)]
    [DataRow(506, LogLevel.Error)]
    [DataRow(507, LogLevel.Error)]
    [DataRow(110, LogLevel.None)]
    [DataRow(111, LogLevel.None)]
    [DataRow(112, LogLevel.None)]
    [DataRow(506, LogLevel.None)]
    [DataRow(507, LogLevel.None)]
    public void ConfigurationEvent_WhenLevelDisabled_DoesNotEmit(
        int eventId,
        LogLevel minimumLevel)
    {
        // Arrange
        RecordingLogger logger = new(minimumLevel: minimumLevel);

        // Act
        Emit(logger, eventId);

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that accessing the event group emits nothing.
    /// </summary>
    [TestMethod]
    public void ConfigurationGroup_Access_DoesNotEmit()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        ConfigurationEvents events = logger.Configuration;

        // Assert
        Assert.HasCount(0, logger.Entries);
    }

    /// <summary>
    /// Verifies that a null logger cannot provide an event group.
    /// </summary>
    [TestMethod]
    public void ConfigurationGroup_WithNullLogger_Throws()
    {
        // Arrange
        ILogger logger = null!;

        // Act
        ArgumentNullException exception =
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                _ = logger.Configuration;
            });

        // Assert
        Assert.AreEqual("logger", exception.ParamName);
    }

    /// <summary>
    /// Verifies that every method rejects a default-initialized event group.
    /// </summary>
    /// <param name="eventId">The event method to exercise.</param>
    /// <remarks>
    /// Event groups must retain an actual logger obtained through the
    /// Configuration extension property.
    /// </remarks>
    [TestMethod]
    [DataRow(110)]
    [DataRow(111)]
    [DataRow(112)]
    [DataRow(506)]
    [DataRow(507)]
    public void ConfigurationEvent_WithDefaultGroup_Throws(int eventId)
    {
        // Arrange
        ConfigurationEvents events = default;

        // Act
        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(
                () => Emit(events, eventId));

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message));
    }

    /// <summary>
    /// Verifies that interleaved attempts retain their supplied identifiers.
    /// </summary>
    /// <remarks>
    /// Correlation belongs to the caller. The emitters must not replace an
    /// identifier with state retained from another validation attempt.
    /// </remarks>
    [TestMethod]
    public void InterleavedAttempts_PreserveCorrelation()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Configuration.Validating("Products", "attempt-a");
        logger.Configuration.Validating("Orders", "attempt-b");
        logger.Configuration.ValidationFailed("Orders", "attempt-b", "Required endpoint is missing");
        logger.Configuration.Validated("Products", "attempt-a");

        // Assert
        CollectionAssert.AreEqual(
            new[] { 110, 110, 507, 112 },
            logger.Entries.Select(entry => entry.EventId.Id).ToArray());

        CollectionAssert.AreEqual(
            new[] { "Products", "Orders", "Orders", "Products" },
            logger.Entries
                .Select(entry => (string)entry.Properties["ConfigurationName"]!)
                .ToArray());

        CollectionAssert.AreEqual(
            new[] { "attempt-a", "attempt-b", "attempt-b", "attempt-a" },
            logger.Entries
                .Select(entry => (string)entry.Properties["ValidationId"]!)
                .ToArray());
    }

    /// <summary>
    /// Verifies that detail events neither deduplicate nor emit outcomes.
    /// </summary>
    /// <remarks>
    /// The application decides when validation completes and whether its
    /// operating mode changes. A detail emitter must not infer either.
    /// </remarks>
    [TestMethod]
    public void DetailEvents_EmitOnlyExplicitlyRequestedRecords()
    {
        // Arrange
        RecordingLogger logger = new();

        // Act
        logger.Configuration.ValueIsInvalid(ConfigurationName, ValidationId, SettingName, Reason);
        logger.Configuration.ValueIsInvalid(ConfigurationName, ValidationId, SettingName, Reason);
        logger.Configuration.ValueIsValid(ConfigurationName, ValidationId, SettingName);

        // Assert
        CollectionAssert.AreEqual(
            new[] { 506, 506, 111 },
            logger.Entries.Select(entry => entry.EventId.Id).ToArray());
    }

    private static void Emit(ILogger logger, int eventId)
    {
        Emit(logger.Configuration, eventId);
    }

    private static void Emit(ConfigurationEvents events, int eventId)
    {
        switch (eventId)
        {
            case 110:
                events.Validating(ConfigurationName, ValidationId);
                break;

            case 111:
                events.ValueIsValid(
                    ConfigurationName, ValidationId, SettingName);
                break;

            case 112:
                events.Validated(ConfigurationName, ValidationId);
                break;

            case 506:
                events.ValueIsInvalid(
                    ConfigurationName, ValidationId, SettingName, Reason);
                break;

            case 507:
                events.ValidationFailed(
                    ConfigurationName, ValidationId, Reason);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(eventId));
        }
    }
}