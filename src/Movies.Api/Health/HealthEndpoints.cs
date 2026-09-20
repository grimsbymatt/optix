using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Movies.Api.Health;

/// <summary>
/// Health endpoints in the shape expected by container orchestrators:
/// <list type="bullet">
///   <item><c>/health/live</c>: the process is running (no dependency checks). Use for liveness probes.</item>
///   <item><c>/health/ready</c>: the service can answer queries (database populated). Use for readiness probes.</item>
///   <item><c>/health</c>: every check, for people and dashboards.</item>
/// </list>
/// Each returns 200 when healthy and 503 when unhealthy, with a JSON body.
/// </summary>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteJsonAsync,
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteJsonAsync,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteJsonAsync,
        });

        return app;
    }

    // Deliberately omits exception details so internal errors aren't exposed; the health check service logs them.
    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        var response = new HealthResponse(
            report.Status.ToString(),
            Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            report.Entries
                .Select(entry => new HealthCheckEntryResponse(
                    entry.Key,
                    entry.Value.Status.ToString(),
                    entry.Value.Description,
                    Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                    entry.Value.Data.Count > 0 ? entry.Value.Data : null))
                .ToList());

        return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }

    private sealed record HealthResponse(string Status, double TotalDurationMs, IReadOnlyList<HealthCheckEntryResponse> Checks);

    private sealed record HealthCheckEntryResponse(
        string Name,
        string Status,
        string? Description,
        double DurationMs,
        IReadOnlyDictionary<string, object>? Data);
}
