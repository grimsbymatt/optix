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

        _dbContext.Movies.AddRange(
            CreateMovie("Spider-Man: No Way Home", new DateOnly(2021, 12, 15), action),
            CreateMovie("Spider-Man", new DateOnly(2002, 5, 1), action),
            CreateMovie("Pokémon: The First Movie", new DateOnly(1998, 7, 18), animation),
            CreateMovie("100% Wolf", new DateOnly(2020, 5, 28), animation),
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
            new[] { "100% Wolf", "Pokémon: The First Movie", "Shiny_Flakes: The Teenage Drug Lord", "Spider-Man", "Spider-Man: No Way Home", "The Batman" },
            result.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task Paging_limits_results_and_reports_totals()
    {
        var page1 = await SearchAsync(null, page: 1, pageSize: 4);
        var page2 = await SearchAsync(null, page: 2, pageSize: 4);

        Assert.Equal(4, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.All(new[] { page1, page2 }, p => Assert.Equal(6, p.TotalCount));
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

        Assert.Equal(new[] { "Action" }, Assert.Single(result.Items).Genres);
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

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private Task<PagedResult<MovieSummary>> SearchAsync(string? search, int page, int pageSize) =>
        _catalog.SearchAsync(new MovieSearchCriteria(search, page, pageSize), TestContext.Current.CancellationToken);

    private static Movie CreateMovie(string title, DateOnly releaseDate, params Genre[] genres) => new()
    {
        Title = title,
        SearchTitle = TextNormalizer.Normalize(title),
        ReleaseDate = releaseDate,
        Genres = genres.ToList(),
    };
}
