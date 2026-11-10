using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Infrastructure.Auth;

namespace MouseDent.Infrastructure.Persistence.Configurations;

public sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.Property(user => user.DisplayName)
            .HasMaxLength(120)
            .IsRequired();
    }
}
