using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Users;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("movies", table =>
        {
            table.HasCheckConstraint("ck_movies_runtime_minutes_positive", "runtime_minutes > 0");
            // Either from TMDB (shared) or added manually by a user (private), never both or neither.
            table.HasCheckConstraint("ck_movies_tmdb_or_owner", "(tmdb_id IS NULL) <> (owner_id IS NULL)");
        });

        builder.HasKey(movie => movie.Id);
        builder.Property(movie => movie.Id).ValueGeneratedNever();

        builder.Property(movie => movie.Title).HasMaxLength(300);
        builder.Property(movie => movie.OriginalTitle).HasMaxLength(300);
        builder.Property(movie => movie.Overview).HasMaxLength(5000);
        builder.Property(movie => movie.PosterUrl).HasMaxLength(2000);

        // Every TMDB movie exists once. PostgreSQL allows multiple NULLs, so manual movies are fine.
        builder.HasIndex(movie => movie.TmdbId).IsUnique();

        // Cascade: deleting a user deletes the movies they added manually.
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(movie => movie.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(movie => movie.OwnerId);

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
