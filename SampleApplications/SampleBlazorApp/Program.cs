using Patware.Diagnostics.ApplicationEvents;
using Patware.Diagnostics.ApplicationEvents.Hosting;

using SampleBlazorApp.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.AddApplicationEvents(options =>
{
    options.FormatLogs = true;
    options.LogUnhandledExceptions = true;
});

// Cache and operating mode belong to the application instance,
// rather than to individual Blazor circuits.
builder.Services.AddSingleton<SampleBlazorApp.Services.CachedProductCatalog>();

var app = builder.Build();

/* The following code demonstrate how to raise configuration events */

ILogger logger = app.Services.GetRequiredService<ILogger<Program>>();

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
