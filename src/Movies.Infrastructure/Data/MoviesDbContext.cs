using Microsoft.EntityFrameworkCore;
using Movies.Core.Entities;

namespace Movies.Infrastructure.Data;

public sealed class MoviesDbContext(DbContextOptions<MoviesDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Genre> Genres => Set<Genre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>(movie =>
        {
            movie.Property(m => m.Title).HasMaxLength(300);
            movie.Property(m => m.SearchTitle).HasMaxLength(300);
            movie.Property(m => m.OriginalLanguage).HasMaxLength(10);
            movie.Property(m => m.PosterUrl).HasMaxLength(500);

            // Supports ordering; "contains" searches (LIKE '%x%') still scan, which is fine at this size.
            movie.HasIndex(m => m.SearchTitle);
            movie.HasIndex(m => m.ReleaseDate);

            movie.HasMany(m => m.Genres).WithMany(g => g.Movies);
        });

        modelBuilder.Entity<Genre>(genre =>
        {
            genre.Property(g => g.Name).HasMaxLength(50);
            genre.HasIndex(g => g.Name).IsUnique();
        });
    }
}
