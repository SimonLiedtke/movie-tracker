using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Activity;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Users;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
{
    public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
    {
        builder.ToTable("activity_log_entries");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        // As text, so the order of the enum can change and the table stays readable.
        builder.Property(entry => entry.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(entry => entry.MovieTitle).HasMaxLength(ActivityLogEntry.MaxMovieTitleLength);
        builder.Property(entry => entry.Detail).HasMaxLength(ActivityLogEntry.MaxDetailLength);

        // Set null: the log outlives a deleted movie, which it still names with MovieTitle.
        builder
            .HasOne<Movie>()
            .WithMany()
            .HasForeignKey(entry => entry.MovieId)
            .OnDelete(DeleteBehavior.SetNull);

        // Cascade: deleting a user deletes their log.
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The log is listed per user, most recent first (the ID, a UUIDv7, breaks ties in the order of creation).
        builder.HasIndex(entry => new { entry.UserId, entry.OccurredAt, entry.Id }).IsDescending(false, true, true);
        // For the movie side of the foreign key.
        builder.HasIndex(entry => entry.MovieId);
    }
}
