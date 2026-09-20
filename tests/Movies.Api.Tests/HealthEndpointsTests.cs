using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Movies.Api.Tests;

public sealed class HealthEndpointsTests(MoviesApiFactory factory) : IClassFixture<MoviesApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_reports_healthy_with_the_database_check_and_movie_count()
    {
        var (statusCode, health) = await GetHealthAsync(_client, "/health");

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Equal("Healthy", health.Status);

        var check = Assert.Single(health.Checks);
        Assert.Equal("movies-database", check.Name);
        Assert.Equal("Healthy", check.Status);
        Assert.Equal(5, check.Data!["movieCount"].GetInt32());
    }

    [Fact]
    public async Task Ready_runs_the_database_check()
    {
        var (statusCode, health) = await GetHealthAsync(_client, "/health/ready");

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Equal("movies-database", Assert.Single(health.Checks).Name);
    }

    [Fact]
    public async Task Live_runs_no_dependency_checks()
    {
        var (statusCode, health) = await GetHealthAsync(_client, "/health/live");

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Equal("Healthy", health.Status);
        Assert.Empty(health.Checks);
    }

    internal static async Task<(HttpStatusCode StatusCode, HealthResponse Health)> GetHealthAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(health);
        return (response.StatusCode, health);
    }

    internal sealed record HealthResponse(string Status, double TotalDurationMs, List<HealthCheckEntry> Checks);

    internal sealed record HealthCheckEntry(
        string Name,
        string Status,
        string? Description,
        double DurationMs,
        Dictionary<string, JsonElement>? Data);
}

/// <summary>
/// When the database is empty the process is alive but can't serve queries,
/// so readiness must fail (503) while liveness still passes.
/// </summary>
public sealed class HealthEndpointsEmptyDatabaseTests(EmptyDatabaseApiFactory factory) : IClassFixture<EmptyDatabaseApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Reports_unhealthy_when_no_movies_are_loaded(string url)
    {
        var (statusCode, health) = await HealthEndpointsTests.GetHealthAsync(_client, url);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, statusCode);
        Assert.Equal("Unhealthy", health.Status);
        Assert.Equal(0, Assert.Single(health.Checks).Data!["movieCount"].GetInt32());
    }

    [Fact]
    public async Task Live_still_reports_healthy()
    {
        var (statusCode, _) = await HealthEndpointsTests.GetHealthAsync(_client, "/health/live");

        Assert.Equal(HttpStatusCode.OK, statusCode);
    }
}
