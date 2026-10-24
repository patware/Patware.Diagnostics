using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Patware.Diagnostics.ApplicationEvents;

namespace SampleConsole.Dependencies;

/// <summary>
/// Demonstrates component resolution with a basic fallback.
/// </summary>
/// <remarks>
/// The preferred component produces a more detailed report. The basic fallback
/// still satisfies this sample's requirements, so resolution failure does not
/// automatically change application mode.
/// </remarks>
internal sealed class ReportFormatterSample(IHost host, ILogger<ReportFormatterSample> logger) : IReportFormatterSample
{

    /// <summary>
    /// Generates a report using the requested formatter or a basic fallback.
    /// </summary>
    /// <returns>The formatted report.</returns>
    /// <remarks>
    /// A fresh resolution identifier distinguishes each unsuccessful attempt.
    /// Required-service resolution throws when the component cannot be supplied.
    /// The sample retains a basic report as an explicit fallback policy.
    /// Formatting happens outside the resolution catch so business-operation
    /// failures are not misclassified as dependency resolution failures.
    /// </remarks>
    public string GenerateReport()
    {

        var unformatted = "Basic report: Sample data";

        IReportFormatter formatter;

        string resolutionId = Guid.NewGuid().ToString("N");

        try
        {
            formatter = host.Services.GetRequiredService<IReportFormatter>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Dependency.ResolutionFailed(
                dependencyName: nameof(IReportFormatter),
                operationName: nameof(GenerateReport),
                resolutionId: resolutionId,
                reasonCode: "ResolutionThrew",
                exception: null); // exception: if null, call stack not displayed, else yes.

            logger.LogDebug(
                ex,
                "Dependency resolution exception details. Resolution: {ResolutionId}.",
                resolutionId);

            return unformatted;
        }

        return formatter.Format(unformatted);
    }
}