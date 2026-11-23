using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class MoviePosterConfiguration : IEntityTypeConfiguration<MoviePoster>
{
    public void Configure(EntityTypeBuilder<MoviePoster> builder)
    {
        builder.ToTable("movie_posters", table =>
            table.HasCheckConstraint("ck_movie_posters_data_size", $"octet_length(data) BETWEEN 1 AND {MoviePoster.MaxBytes}"));

        // One poster per movie: the movie's ID is the key.
        builder.HasKey(poster => poster.MovieId);
        builder.Property(poster => poster.MovieId).ValueGeneratedNever();

        builder.Property(poster => poster.ContentType).HasMaxLength(50);
        builder.Property(poster => poster.Data);

        // Cascade: deleting a movie deletes its poster.
        builder
            .HasOne<Movie>()
            .WithOne()
            .HasForeignKey<MoviePoster>(poster => poster.MovieId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
