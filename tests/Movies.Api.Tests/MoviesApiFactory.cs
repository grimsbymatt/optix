using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Movies.Infrastructure;

namespace Movies.Api.Tests;

/// <summary>
/// Hosts the real API in memory with its own uniquely named SQLite database
/// (so test classes running in parallel don't share state) seeded from a small, known CSV.
/// </summary>
public sealed class MoviesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"movies-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
            services.PostConfigure<MoviesDataOptions>(options =>
            {
                options.ConnectionString = $"Data Source={_databaseName};Mode=Memory;Cache=Shared";
                options.SeedCsvPath = Path.Combine(AppContext.BaseDirectory, "TestData", "movies.csv");
            }));
    }
}
