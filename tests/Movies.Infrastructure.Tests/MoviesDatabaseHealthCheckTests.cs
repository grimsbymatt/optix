using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Movies.Core.Entities;
using Movies.Infrastructure.Data;
using Movies.Infrastructure.Health;

namespace Movies.Infrastructure.Tests;

public sealed class MoviesDatabaseHealthCheckTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MoviesDbContext _dbContext;

    public MoviesDatabaseHealthCheckTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _dbContext = new MoviesDbContext(
            new DbContextOptionsBuilder<MoviesDbContext>().UseSqlite(_connection).Options);
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task Healthy_when_movies_are_loaded()
    {
        _dbContext.Movies.Add(new Movie { Title = "The Batman", SearchTitle = "the batman" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(1, result.Data["movieCount"]);
    }

    [Fact]
    public async Task Unhealthy_when_the_database_is_empty()
    {
        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(0, result.Data["movieCount"]);
    }

    [Fact]
    public async Task Unhealthy_when_the_database_cannot_be_queried()
    {
        // A fresh in-memory database has no schema: this is what the app would see if the
        // keep-alive connection were lost and the original in-memory database destroyed.
        await using var brokenContext = new MoviesDbContext(
            new DbContextOptionsBuilder<MoviesDbContext>().UseSqlite("Data Source=:memory:").Options);

        var result = await new MoviesDatabaseHealthCheck(brokenContext)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private Task<HealthCheckResult> CheckAsync() =>
        new MoviesDatabaseHealthCheck(_dbContext)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
}
