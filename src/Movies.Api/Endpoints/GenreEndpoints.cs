using Microsoft.AspNetCore.Http.HttpResults;
using Movies.Core.Search;

namespace Movies.Api.Endpoints;

public static class GenreEndpoints
{
    public static IEndpointRouteBuilder MapGenreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/genres", GetGenresAsync)
            .WithTags("Genres")
            .WithName("GetGenres")
            .WithSummary("List all genres")
            .WithDescription("Returns every genre with the number of movies in it. Use a name as the 'genre' filter on GET /api/movies.");

        return app;
    }

    internal static async Task<Ok<IReadOnlyList<GenreSummary>>> GetGenresAsync(
        IMovieCatalog catalog,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await catalog.GetGenresAsync(cancellationToken));
}
