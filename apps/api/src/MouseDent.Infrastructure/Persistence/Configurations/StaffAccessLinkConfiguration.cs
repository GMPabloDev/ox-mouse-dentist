using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Auth;

namespace MouseDent.Infrastructure.Persistence.Configurations;

public sealed class StaffAccessLinkConfiguration : IEntityTypeConfiguration<StaffAccessLink>
{
    public void Configure(EntityTypeBuilder<StaffAccessLink> builder)
    {
        builder.ToTable("StaffAccessLinks");

        builder.HasKey(link => link.Id);

        // Solo se persiste el hash del token. La unicidad permite localizarlo sin
        // recorrer la tabla y evita dos enlaces con el mismo token.
        builder.Property(link => link.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(link => link.TokenHash)
            .IsUnique();

        builder.HasIndex(link => link.AccountId);

        builder.HasOne<StaffUser>()
            .WithMany()
            .HasForeignKey(link => link.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
