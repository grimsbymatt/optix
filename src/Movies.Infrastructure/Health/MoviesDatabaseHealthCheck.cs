using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Movies.Infrastructure.Data;

namespace Movies.Infrastructure.Health;

/// <summary>
/// Reports whether the movie database can be queried and contains data. An empty database means the
/// seed failed or the in-memory database was lost (e.g. the keep-alive connection closed), so the
/// service can't answer queries even though the process is running.
/// </summary>
internal sealed class MoviesDatabaseHealthCheck(MoviesDbContext dbContext) : IHealthCheck
{
    public const string Name = "movies-database";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var movieCount = await dbContext.Movies.CountAsync(cancellationToken);
            var data = new Dictionary<string, object> { ["movieCount"] = movieCount };

            return movieCount > 0
                ? HealthCheckResult.Healthy($"{movieCount} movies loaded.", data)
                : HealthCheckResult.Unhealthy("The movie database is empty.", data: data);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("The movie database could not be queried.", exception);
        }
    }
}
