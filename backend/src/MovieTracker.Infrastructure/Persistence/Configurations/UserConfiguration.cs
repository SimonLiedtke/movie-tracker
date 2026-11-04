using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieTracker.Domain.Users;

namespace MovieTracker.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.GoogleSubject).HasMaxLength(255);
        builder.HasIndex(user => user.GoogleSubject).IsUnique();

        builder.Property(user => user.Email).HasMaxLength(320);
        builder.Property(user => user.Name).HasMaxLength(300);
        builder.Property(user => user.PictureUrl).HasMaxLength(2000);
    }
}
