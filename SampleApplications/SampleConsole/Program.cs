using Microsoft.Extensions.Hosting;

using Patware.ApplicationEvents.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddPatwareApplicationEvents(options =>
{
    options.FormatLogs = true;
});



await builder.Build().RunAsync();