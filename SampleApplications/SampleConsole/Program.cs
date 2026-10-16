using Microsoft.Extensions.Hosting;

using Patware.ApplicationEvents.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddPatwareApplicationEvents();

await builder.Build().RunAsync();