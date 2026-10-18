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

await host.RunAsync();