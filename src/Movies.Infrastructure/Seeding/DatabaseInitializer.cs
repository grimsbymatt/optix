using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Movies.Core.Entities;
using Movies.Core.Text;
using Movies.Infrastructure.Data;

namespace Movies.Infrastructure.Seeding;

/// <summary>
/// Creates the schema and loads the CSV into the database if it is empty.
/// EnsureCreated (rather than migrations) is deliberate: the database is rebuilt on every start.
/// </summary>
internal sealed class DatabaseInitializer(
    MoviesDbContext dbContext,
    SqliteKeepAlive keepAlive,
    IOptions<MoviesDataOptions> options,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Must happen before anything else, or the in-memory database disappears between operations.
        keepAlive.EnsureOpen();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (await dbContext.Movies.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Movie database already contains data; skipping seed.");
            return;
        }

        var csvPath = ResolvePath(options.Value.SeedCsvPath);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Seed file not found at '{csvPath}'.", csvPath);
        }

        MovieCsvReadResult result;
        using (var reader = new StreamReader(csvPath))
        {
            result = await MovieCsvReader.ReadAsync(reader, cancellationToken);
        }

        foreach (var error in result.Errors)
        {
            logger.LogWarning("Skipped CSV row. {Error}", error);
        }

        var genres = new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);

        Genre GetOrAddGenre(string name)
        {
            if (!genres.TryGetValue(name, out var genre))
            {
                genre = new Genre { Name = name };
                genres.Add(name, genre);
            }

            return genre;
        }

        var movies = result.Rows.Select(row => new Movie
        {
            Title = row.Title,
            SearchTitle = TextNormalizer.Normalize(row.Title),
            Overview = row.Overview,
            ReleaseDate = row.ReleaseDate,
            Popularity = row.Popularity,
            VoteCount = row.VoteCount,
            VoteAverage = row.VoteAverage,
            OriginalLanguage = row.OriginalLanguage,
            PosterUrl = row.PosterUrl,
            Genres = row.Genres.Select(GetOrAddGenre).ToList(),
        });

        dbContext.Movies.AddRange(movies);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {MovieCount} movies and {GenreCount} genres from {CsvPath} ({SkippedCount} rows skipped).",
            result.Rows.Count, genres.Count, csvPath, result.Errors.Count);
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
