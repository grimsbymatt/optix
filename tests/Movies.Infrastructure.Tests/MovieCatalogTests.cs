using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Movies.Core.Entities;
using Movies.Core.Search;
using Movies.Core.Text;
using Movies.Infrastructure.Data;
using Movies.Infrastructure.Search;

namespace Movies.Infrastructure.Tests;

/// <summary>
/// Runs the real queries against a private in-memory SQLite database
/// (not the EF InMemory provider, which would not exercise SQL translation).
/// </summary>
public sealed class MovieCatalogTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MoviesDbContext _dbContext;
    private readonly MovieCatalog _catalog;

    public MovieCatalogTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _dbContext = new MoviesDbContext(
            new DbContextOptionsBuilder<MoviesDbContext>().UseSqlite(_connection).Options);
        _dbContext.Database.EnsureCreated();

        var action = new Genre { Name = "Action" };
        var animation = new Genre { Name = "Animation" };
        var comedy = new Genre { Name = "Comedy" };
        var scienceFiction = new Genre { Name = "Science Fiction" };

        _dbContext.Movies.AddRange(
            CreateMovie("Spider-Man: No Way Home", new DateOnly(2021, 12, 15), action, scienceFiction),
            CreateMovie("Spider-Man", new DateOnly(2002, 5, 1), action, scienceFiction),
            CreateMovie("Pokémon: The First Movie", new DateOnly(1998, 7, 18), animation),
            CreateMovie("100% Wolf", new DateOnly(2020, 5, 28), animation, comedy),
            CreateMovie("Shiny_Flakes: The Teenage Drug Lord", new DateOnly(2021, 8, 3)),
            CreateMovie("The Batman", new DateOnly(2022, 3, 1), action));
        _dbContext.SaveChanges();
        _dbContext.ChangeTracker.Clear();

        _catalog = new MovieCatalog(_dbContext);
    }

    [Theory]
    [InlineData("spider", 2)]
    [InlineData("SPIDER-MAN", 2)]
    [InlineData("pokemon", 1)] // accent-insensitive
    [InlineData("POKÉMON", 1)]
    [InlineData("%", 1)]       // wildcard is treated literally
    [InlineData("_", 1)]
    [InlineData("no match", 0)]
    public async Task Search_matches_titles_case_and_accent_insensitively(string search, int expectedCount)
    {
        var result = await SearchAsync(search, page: 1, pageSize: 10);

        Assert.Equal(expectedCount, result.TotalCount);
        Assert.Equal(expectedCount, result.Items.Count);
    }

    [Fact]
    public async Task Search_without_a_term_returns_all_movies_ordered_by_title()
    {
        var result = await SearchAsync(null, page: 1, pageSize: 10);

        Assert.Equal(
            ["100% Wolf", "Pokémon: The First Movie", "Shiny_Flakes: The Teenage Drug Lord", "Spider-Man", "Spider-Man: No Way Home", "The Batman"],
            result.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task Paging_limits_results_and_reports_totals()
    {
        var page1 = await SearchAsync(null, page: 1, pageSize: 4);
        var page2 = await SearchAsync(null, page: 2, pageSize: 4);

        Assert.Equal(4, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.All([page1, page2], p => Assert.Equal(6, p.TotalCount));
        Assert.Equal(2, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);
        Assert.False(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);
        Assert.Empty(page1.Items.Select(m => m.Id).Intersect(page2.Items.Select(m => m.Id)));
    }

    [Fact]
    public async Task Page_beyond_the_end_returns_no_items_but_keeps_totals()
    {
        var result = await SearchAsync(null, page: 1_000_000, pageSize: 100);

        Assert.Empty(result.Items);
        Assert.Equal(6, result.TotalCount);
    }

    [Fact]
    public async Task Results_include_genres()
    {
        var result = await SearchAsync("batman", page: 1, pageSize: 10);

        Assert.Equal(["Action"], Assert.Single(result.Items).Genres);
    }

    [Fact]
    public async Task GetById_returns_details_or_null()
    {
        var id = (await SearchAsync("batman", 1, 1)).Items[0].Id;

        var found = await _catalog.GetByIdAsync(id, TestContext.Current.CancellationToken);
        var missing = await _catalog.GetByIdAsync(-1, TestContext.Current.CancellationToken);

        Assert.Equal("The Batman", found?.Title);
        Assert.Null(missing);
    }

    [Theory]
    [InlineData("Action", 3)]
    [InlineData("action", 3)]            // case-insensitive
    [InlineData("SCIENCE FICTION", 2)]   // multi-word genre
    [InlineData(" Animation ", 2)]       // surrounding whitespace ignored
    [InlineData("Western", 0)]           // unknown genre is simply no results
    public async Task Genre_filter_returns_only_movies_in_that_genre(string genre, int expectedCount)
    {
        var result = await SearchAsync(new MovieSearchCriteria(null, 1, 10) { Genre = genre });

        Assert.Equal(expectedCount, result.TotalCount);
        Assert.All(result.Items, m => Assert.Contains(genre.Trim(), m.Genres, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("spider", "Action", 2)]
    [InlineData("spider", "Animation", 0)]
    [InlineData("man", "Action", 3)]     // both Spider-Man films and "The Batman"
    [InlineData("the", "Animation", 1)]  // "Pokémon: The First Movie" only
    public async Task Genre_filter_combines_with_title_search(string search, string genre, int expectedCount)
    {
        var result = await SearchAsync(new MovieSearchCriteria(search, 1, 10) { Genre = genre });

        Assert.Equal(expectedCount, result.TotalCount);
    }

    [Fact]
    public async Task Genre_filter_is_applied_before_paging()
    {
        var result = await SearchAsync(new MovieSearchCriteria(null, 1, 2) { Genre = "Action" });

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task Sorts_by_title_descending()
    {
        var result = await SearchAsync(new MovieSearchCriteria(null, 1, 10) { SortDirection = SortDirection.Descending });

        Assert.Equal(
            ["The Batman", "Spider-Man: No Way Home", "Spider-Man", "Shiny_Flakes: The Teenage Drug Lord", "Pokémon: The First Movie", "100% Wolf"],
            result.Items.Select(m => m.Title));
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task Sorts_by_release_date(SortDirection direction)
    {
        var result = await SearchAsync(new MovieSearchCriteria(null, 1, 10)
        {
            SortBy = MovieSortField.ReleaseDate,
            SortDirection = direction,
        });

        var expected = new[]
        {
            "Pokémon: The First Movie",            // 1998-07-18
            "Spider-Man",                          // 2002-05-01
            "100% Wolf",                           // 2020-05-28
            "Shiny_Flakes: The Teenage Drug Lord", // 2021-08-03
            "Spider-Man: No Way Home",             // 2021-12-15
            "The Batman",                          // 2022-03-01
        };
        if (direction == SortDirection.Descending)
        {
            Array.Reverse(expected);
        }

        Assert.Equal(expected, result.Items.Select(m => m.Title));
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task Movies_released_on_the_same_date_are_ordered_by_title(SortDirection direction)
    {
        _dbContext.Movies.Add(CreateMovie("Another Film", new DateOnly(2022, 3, 1)));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await SearchAsync(new MovieSearchCriteria(null, 1, 10)
        {
            SortBy = MovieSortField.ReleaseDate,
            SortDirection = direction,
        });

        var titles = result.Items.Select(m => m.Title).ToList();
        Assert.Equal(titles.IndexOf("Another Film") + 1, titles.IndexOf("The Batman"));
    }

    [Fact]
    public async Task Sorting_is_applied_before_paging()
    {
        var criteria = new MovieSearchCriteria(null, 1, 2) { SortBy = MovieSortField.ReleaseDate };

        var page1 = await SearchAsync(criteria);
        var page2 = await SearchAsync(criteria with { Page = 2 });

        Assert.Equal(["Pokémon: The First Movie", "Spider-Man"], page1.Items.Select(m => m.Title));
        Assert.Equal(["100% Wolf", "Shiny_Flakes: The Teenage Drug Lord"], page2.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task GetGenres_returns_every_genre_with_its_movie_count_ordered_by_name()
    {
        var genres = await _catalog.GetGenresAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new GenreSummary("Action", 3),
                new GenreSummary("Animation", 2),
                new GenreSummary("Comedy", 1),
                new GenreSummary("Science Fiction", 2),
            ],
            genres);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private Task<PagedResult<MovieSummary>> SearchAsync(string? search, int page, int pageSize) =>
        SearchAsync(new MovieSearchCriteria(search, page, pageSize));

    private Task<PagedResult<MovieSummary>> SearchAsync(MovieSearchCriteria criteria) =>
        _catalog.SearchAsync(criteria, TestContext.Current.CancellationToken);

    private static Movie CreateMovie(string title, DateOnly releaseDate, params Genre[] genres) => new()
    {
        Title = title,
        SearchTitle = TextNormalizer.Normalize(title),
        ReleaseDate = releaseDate,
        Genres = [.. genres],
    };
}
