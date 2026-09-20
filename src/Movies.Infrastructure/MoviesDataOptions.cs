namespace Movies.Infrastructure;

public sealed class MoviesDataOptions
{
    public const string SectionName = "MoviesData";

    /// <summary>
    /// A named, shared-cache in-memory database. It exists only while at least one
    /// connection is open, which is why <see cref="Data.SqliteKeepAlive"/> holds one open.
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=movies;Mode=Memory;Cache=Shared";

    /// <summary>
    /// Path to the source CSV. Relative paths are resolved against the application's base directory.
    /// </summary>
    public string SeedCsvPath { get; set; } = "Data/mymoviedb.csv";
}
