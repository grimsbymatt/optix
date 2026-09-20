using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Movies.Core.Search;
using Movies.Infrastructure.Data;
using Movies.Infrastructure.Health;
using Movies.Infrastructure.Search;
using Movies.Infrastructure.Seeding;

namespace Movies.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the SQLite database, keep-alive connection, seeding and query services.
    /// Expects <see cref="MoviesDataOptions"/> to be configured by the host.
    /// </summary>
    public static IServiceCollection AddMoviesInfrastructure(this IServiceCollection services)
    {
        // Options are resolved lazily so hosts (and tests) can override them after registration.
        services.AddSingleton(sp =>
            new SqliteKeepAlive(sp.GetRequiredService<IOptions<MoviesDataOptions>>().Value.ConnectionString));

        services.AddDbContext<MoviesDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<IOptions<MoviesDataOptions>>().Value.ConnectionString));

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IMovieCatalog, MovieCatalog>();

        return services;
    }

    /// <summary>
    /// Adds a health check that confirms the movie database is reachable and populated.
    /// </summary>
    public static IHealthChecksBuilder AddMoviesDatabaseHealthCheck(this IHealthChecksBuilder builder, params string[] tags) =>
        builder.AddCheck<MoviesDatabaseHealthCheck>(MoviesDatabaseHealthCheck.Name, tags: tags);

    /// <summary>
    /// Creates the schema and loads the CSV. Call once at start-up, before the app starts serving requests.
    /// </summary>
    public static async Task InitializeMoviesDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
