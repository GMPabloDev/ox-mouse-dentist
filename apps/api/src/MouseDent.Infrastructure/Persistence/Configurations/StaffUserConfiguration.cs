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

        // El perfil es la fuente de acceso. Los roles de Identity se conservan por
        // compatibilidad con el esquema inicial, pero ninguna consulta los lee.
        builder.Property(user => user.Profile)
            .HasMaxLength(40)
            .IsRequired();

        builder.HasMany(user => user.Assignments)
            .WithOne(assignment => assignment.User)
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
