using Microsoft.AspNetCore.Http.HttpResults;
using Movies.Core.Search;

namespace Movies.Api.Endpoints;

public static class MovieEndpoints
{
    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/movies").WithTags("Movies");

        group.MapGet("/", SearchMoviesAsync)
            .WithName("SearchMovies")
            .WithSummary("Search, filter, sort and page through movies")
            .WithDescription(
                "Case- and accent-insensitive 'contains' search on the movie title, optionally filtered by genre. " +
                "Results are sorted by title (default) or release date and paged; omit 'search' to page through all movies.");

        group.MapGet("/{id:int}", GetMovieByIdAsync)
            .WithName("GetMovieById")
            .WithSummary("Get a single movie");

        return app;
    }

    internal static async Task<Results<Ok<PagedResult<MovieSummary>>, ValidationProblem>> SearchMoviesAsync(
        [AsParameters] SearchMoviesRequest request,
        IMovieCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (!request.TryGetCriteria(out var criteria, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await catalog.SearchAsync(criteria, cancellationToken);
        return TypedResults.Ok(result);
    }

    internal static async Task<Results<Ok<MovieDetails>, NotFound>> GetMovieByIdAsync(
        int id,
        IMovieCatalog catalog,
        CancellationToken cancellationToken)
    {
        var movie = await catalog.GetByIdAsync(id, cancellationToken);
        return movie is null ? TypedResults.NotFound() : TypedResults.Ok(movie);
    }
}
