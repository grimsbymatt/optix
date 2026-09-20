using Movies.Infrastructure.Seeding;

namespace Movies.Infrastructure.Tests;

public class MovieCsvReaderTests
{
    private const string Header =
        "Release_Date,Title,Overview,Popularity,Vote_Count,Vote_Average,Original_Language,Genre,Poster_Url\n";

    [Fact]
    public async Task Parses_a_valid_row_including_quoted_fields_and_multiple_genres()
    {
        var csv = Header +
            "2021-12-15,Spider-Man: No Way Home,\"Peter Parker, unmasked.\",5083.954,8940,8.3,en,\"Action, Adventure, Science Fiction\",https://image.tmdb.org/a.jpg\n";

        var result = await ReadAsync(csv);

        var row = Assert.Single(result.Rows);
        Assert.Empty(result.Errors);
        Assert.Equal(new DateOnly(2021, 12, 15), row.ReleaseDate);
        Assert.Equal("Spider-Man: No Way Home", row.Title);
        Assert.Equal("Peter Parker, unmasked.", row.Overview);
        Assert.Equal(5083.954, row.Popularity);
        Assert.Equal(8940, row.VoteCount);
        Assert.Equal(8.3, row.VoteAverage);
        Assert.Equal("en", row.OriginalLanguage);
        Assert.Equal(new[] { "Action", "Adventure", "Science Fiction" }, row.Genres);
        Assert.Equal("https://image.tmdb.org/a.jpg", row.PosterUrl);
    }

    [Fact]
    public async Task Treats_a_bare_carriage_return_inside_an_unquoted_field_as_data()
    {
        // Mirrors a real row in the dataset ("Pixie Hollow Bake Off").
        var csv = Header +
            "2013-10-20,Pixie Hollow Bake Off,Mini-Shorts:\r - Just Desserts\r - Dust Up,61.328,35,7.1,en,Animation,https://image.tmdb.org/b.jpg\n";

        var result = await ReadAsync(csv);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Mini-Shorts:\n - Just Desserts\n - Dust Up", row.Overview);
        Assert.Equal("https://image.tmdb.org/b.jpg", row.PosterUrl);
    }

    [Fact]
    public async Task Handles_crlf_line_endings()
    {
        var csv = Header.Replace("\n", "\r\n") +
            "2022-03-01,The Batman,Overview,3827.658,1151,8.1,en,\"Crime, Mystery\",https://image.tmdb.org/c.jpg\r\n";

        var result = await ReadAsync(csv);

        var row = Assert.Single(result.Rows);
        Assert.Equal("https://image.tmdb.org/c.jpg", row.PosterUrl);
        Assert.Equal(new[] { "Crime", "Mystery" }, row.Genres);
    }

    [Fact]
    public async Task Skips_and_reports_invalid_rows_without_failing_the_import()
    {
        var csv = Header +
            "not-a-date,Bad Date,Overview,1,1,1,en,Drama,https://x.test/a.jpg\n" +
            "2020-01-01,Bad Number,Overview,abc,1,1,en,Drama,https://x.test/b.jpg\n" +
            "2020-01-01,,Overview,1,1,1,en,Drama,https://x.test/c.jpg\n" +
            "2020-01-01,Good,Overview,1,1,1,en,Drama,https://x.test/d.jpg\n";

        var result = await ReadAsync(csv);

        Assert.Equal("Good", Assert.Single(result.Rows).Title);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public async Task Throws_when_a_required_column_is_missing()
    {
        var csv = "Release_Date,Title\n2020-01-01,Only Two Columns\n";

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(csv));
    }

    [Fact]
    public async Task Imports_every_row_of_the_real_dataset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "mymoviedb.csv");
        using var reader = new StreamReader(path);

        var result = await MovieCsvReader.ReadAsync(reader, TestContext.Current.CancellationToken);

        Assert.Empty(result.Errors);
        Assert.Equal(9827, result.Rows.Count);
    }

    private static Task<MovieCsvReadResult> ReadAsync(string csv) =>
        MovieCsvReader.ReadAsync(new StringReader(csv), TestContext.Current.CancellationToken);
}
