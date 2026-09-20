using System.Net.Http.Json;
using Movies.Core.Search;

namespace Movies.Api.Tests;

public sealed class GenreEndpointsTests(MoviesApiFactory factory) : IClassFixture<MoviesApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Returns_every_genre_with_its_movie_count_ordered_by_name()
    {
        var genres = await _client.GetFromJsonAsync<GenreSummary[]>("/api/genres", TestContext.Current.CancellationToken);

        Assert.NotNull(genres);
        Assert.Equal(
            new[]
            {
                new GenreSummary("Action", 2),
                new GenreSummary("Adventure", 1),
                new GenreSummary("Animation", 2),
                new GenreSummary("Comedy", 1),
                new GenreSummary("Crime", 1),
                new GenreSummary("Family", 1),
                new GenreSummary("Mystery", 1),
                new GenreSummary("Science Fiction", 2),
                new GenreSummary("Thriller", 1),
            },
            genres);
    }
}
