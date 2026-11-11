using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Users;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.ToTable("watchlist_items");

        // A movie is either on a user's watchlist or not.
        builder.HasKey(item => new { item.UserId, item.MovieId });

        // Cascade: deleting a movie also removes it from all watchlists.
        builder
            .HasOne(item => item.Movie)
            .WithMany()
            .HasForeignKey(item => item.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade: deleting a user deletes their watchlist.
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(item => item.Note).HasMaxLength(500);
        // A text[] column; the tags are normalized (see TagList), so "tag = ANY(tags)" finds a tag.
        builder.Property(item => item.Tags).HasColumnType("text[]").HasDefaultValueSql("'{}'");

        builder.HasIndex(item => new { item.UserId, item.AddedAt });
        // For the movie side of the foreign key (the primary key starts with user_id).
        builder.HasIndex(item => item.MovieId);
    }
}
