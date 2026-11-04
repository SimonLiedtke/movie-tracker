using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Users;
using MovieTracker.Infrastructure.Persistence.Converters;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class DiaryEntryConfiguration : IEntityTypeConfiguration<DiaryEntry>
{
    public void Configure(EntityTypeBuilder<DiaryEntry> builder)
    {
        builder.ToTable("diary_entries", table =>
            table.HasCheckConstraint(
                "ck_diary_entries_rating_range",
                $"rating BETWEEN {Rating.MinHalfStars} AND {Rating.MaxHalfStars}"));

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.Rating).HasConversion(new RatingConverter());
        builder.Property(entry => entry.Review).HasMaxLength(20000);

        // Restrict: a movie with diary entries cannot be deleted (API answers with 409).
        builder
            .HasOne(entry => entry.Movie)
            .WithMany()
            .HasForeignKey(entry => entry.MovieId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade: deleting a user deletes their diary.
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Every query is scoped to one user, the diary is ordered by date.
        builder.HasIndex(entry => new { entry.UserId, entry.WatchedOn });
    }
}
