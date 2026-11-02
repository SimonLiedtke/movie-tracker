using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("movies", table =>
            table.HasCheckConstraint("ck_movies_runtime_minutes_positive", "runtime_minutes > 0"));

        builder.HasKey(movie => movie.Id);
        builder.Property(movie => movie.Id).ValueGeneratedNever();

        builder.Property(movie => movie.Title).HasMaxLength(300);
        builder.Property(movie => movie.OriginalTitle).HasMaxLength(300);
        builder.Property(movie => movie.Overview).HasMaxLength(5000);
        builder.Property(movie => movie.PosterUrl).HasMaxLength(2000);

        // PostgreSQL allows multiple NULLs in a unique index, so movies without a TMDB ID are fine.
        builder.HasIndex(movie => movie.TmdbId).IsUnique();

        builder
            .HasMany(movie => movie.Genres)
            .WithMany()
            .UsingEntity(
                "movie_genres",
                right => right.HasOne(typeof(Genre)).WithMany().HasForeignKey("GenreId"),
                left => left.HasOne(typeof(Movie)).WithMany().HasForeignKey("MovieId"));

        builder.Navigation(movie => movie.Genres).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
