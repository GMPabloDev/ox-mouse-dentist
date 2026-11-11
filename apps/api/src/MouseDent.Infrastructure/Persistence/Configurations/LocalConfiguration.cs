using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Locales;

namespace MouseDent.Infrastructure.Persistence.Configurations;

public sealed class LocalConfiguration : IEntityTypeConfiguration<Local>
{
    public void Configure(EntityTypeBuilder<Local> builder)
    {
        builder.ToTable("Locales");

        builder.HasKey(local => local.Id);

        builder.Property(local => local.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(local => local.NormalizedName)
            .HasMaxLength(120)
            .IsRequired();

        // La unicidad incluye los locales inactivos y sobrevive a escrituras concurrentes.
        builder.HasIndex(local => local.NormalizedName)
            .IsUnique();

        builder.Property(local => local.Address)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(local => local.Phone)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(local => local.Schedule)
            .HasConversion(new WeeklyScheduleConverter())
            .IsRequired();

        builder.Property(local => local.PhotoFileName)
            .HasMaxLength(128);

        builder.Property(local => local.PhotoContentType)
            .HasMaxLength(64);

        builder.Ignore(local => local.Photo);
    }
}
