using Patware.ApplicationEvents.Hosting;

using SampleWorkerService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.AddPatwareApplicationEvents(options =>
{
    options.FormatLogs = true;
});

var host = builder.Build();

await host.RunAsync();
