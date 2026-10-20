using Patware.Diagnostics.ApplicationEvents;
using Patware.Diagnostics.ApplicationEvents.Hosting;

using SampleWorkerService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Subscribe the exception observer before starting the worker
// under the host's default sequential startup policy.
builder.AddApplicationEvents(options =>
{
    options.FormatLogs = true;
    options.LogUnhandledExceptions = true;
});

builder.Services.AddHostedService<Worker>();

using IHost host = builder.Build();

/* The following code demonstrate how to raise configuration events */

ILogger logger = host.Services.GetRequiredService<ILogger<Program>>();

const string configurationName = "SampleBlazorApp";

string failedAttempt = Guid.NewGuid().ToString("N");

logger.Configuration.Validating(configurationName, failedAttempt);

logger.Configuration.ValueIsValid(
    configurationName,
    failedAttempt,
    settingName: "BaseAddress");

logger.Configuration.ValueIsInvalid(
    configurationName,
    failedAttempt,
    settingName: "TimeoutSeconds",
    reason: "Must be greater than zero");

logger.Configuration.ValidationFailed(
    configurationName,
    failedAttempt,
    reason: "TimeoutSeconds violates its required range");

// Later, after the operator supplies a corrected candidate:
string correctedAttempt = Guid.NewGuid().ToString("N");

logger.Configuration.Validating(configurationName, correctedAttempt);

logger.Configuration.ValueIsValid(
    configurationName,
    correctedAttempt,
    settingName: "BaseAddress");

logger.Configuration.ValueIsValid(
    configurationName,
    correctedAttempt,
    settingName: "TimeoutSeconds");

logger.Configuration.Validated(configurationName, correctedAttempt);

/* End of Configuration testing */

await host.RunAsync();