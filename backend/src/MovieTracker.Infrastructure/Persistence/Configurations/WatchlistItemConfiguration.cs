using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.ToTable("watchlist_items");

        // The movie ID is the primary key: a movie is either on the watchlist or not.
        builder.HasKey(item => item.MovieId);

        // Cascade: deleting a movie also removes it from the watchlist.
        builder
            .HasOne(item => item.Movie)
            .WithOne()
            .HasForeignKey<WatchlistItem>(item => item.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(item => item.Note).HasMaxLength(500);

        builder.HasIndex(item => item.AddedAt);
    }
}
