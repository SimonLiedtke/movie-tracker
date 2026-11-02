using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Persistence.Seed;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("genres");

        builder.HasKey(genre => genre.Id);
        builder.Property(genre => genre.Id).ValueGeneratedNever();

        builder.Property(genre => genre.Name).HasMaxLength(100);
        builder.HasIndex(genre => genre.Name).IsUnique();

        builder.HasData(GenreSeed.All);
    }
}
