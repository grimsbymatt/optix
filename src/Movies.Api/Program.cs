using Movies.Api.Endpoints;
using Movies.Api.Errors;
using Movies.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<MoviesDataOptions>().BindConfiguration(MoviesDataOptions.SectionName);
builder.Services.AddMoviesInfrastructure();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
}

app.MapHealthChecks("/health");
app.MapMovieEndpoints();
app.MapGenreEndpoints();

// Build and seed the in-memory database before accepting requests.
await app.Services.InitializeMoviesDatabaseAsync();

await app.RunAsync();

// Exposes Program to WebApplicationFactory in the integration tests.
public partial class Program;
