using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Movies.Core.Search;

namespace Movies.Api.Tests;

public sealed class MovieEndpointsTests(MoviesApiFactory factory) : IClassFixture<MoviesApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_by_title_returns_matching_movies()
    {
        var result = await GetPageAsync("/api/movies?search=spider");

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, m => Assert.Contains("Spider", m.Title));
    }

    [Fact]
    public async Task Search_is_case_and_accent_insensitive()
    {
        var result = await GetPageAsync("/api/movies?search=POKEMON");

        Assert.Equal("Pokémon: The First Movie", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task PageSize_limits_the_number_of_results()
    {
        var result = await GetPageAsync("/api/movies?pageSize=2");

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task Paging_walks_through_every_movie_exactly_once()
    {
        var titles = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await GetPageAsync($"/api/movies?page={page}&pageSize=2");
            titles.AddRange(result.Items.Select(m => m.Title));
        }

        Assert.Equal(
            new[] { "100% Wolf", "Pokémon: The First Movie", "Spider-Man", "Spider-Man: No Way Home", "The Batman" },
            titles);
    }

    [Fact]
    public async Task Defaults_are_applied_when_paging_is_not_specified()
    {
        var result = await GetPageAsync("/api/movies");

        Assert.Equal(MovieSearchCriteria.DefaultPage, result.Page);
        Assert.Equal(MovieSearchCriteria.DefaultPageSize, result.PageSize);
        Assert.Equal(5, result.Items.Count);
    }

    [Theory]
    [InlineData("/api/movies?pageSize=0", "PageSize")]
    [InlineData("/api/movies?pageSize=101", "PageSize")]
    [InlineData("/api/movies?page=0", "Page")]
    public async Task Invalid_paging_returns_a_validation_problem(string url, string expectedErrorKey)
    {
        var response = await _client.GetAsync(url, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(CancellationToken);
        Assert.NotNull(problem);
        Assert.Contains(expectedErrorKey, problem.Errors.Keys);
    }

    [Fact]
    public async Task Get_by_id_returns_the_movie()
    {
        var summary = (await GetPageAsync("/api/movies?search=batman")).Items[0];

        var movie = await _client.GetFromJsonAsync<MovieDetails>($"/api/movies/{summary.Id}", CancellationToken);

        Assert.NotNull(movie);
        Assert.Equal("The Batman", movie.Title);
        Assert.Equal(new[] { "Crime", "Mystery", "Thriller" }, movie.Genres);
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_404()
    {
        var response = await _client.GetAsync("/api/movies/999999", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<PagedResult<MovieSummary>> GetPageAsync(string url)
    {
        var response = await _client.GetAsync(url, CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PagedResult<MovieSummary>>(CancellationToken);
        Assert.NotNull(result);
        return result;
    }
}
