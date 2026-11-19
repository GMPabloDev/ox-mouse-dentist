using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Locales;
using MouseDent.Infrastructure.Auth;

namespace MouseDent.Infrastructure.Persistence.Configurations;

public sealed class StaffLocalAssignmentConfiguration : IEntityTypeConfiguration<StaffLocalAssignment>
{
    public void Configure(EntityTypeBuilder<StaffLocalAssignment> builder)
    {
        builder.ToTable("StaffLocalAssignments");

        builder.HasKey(assignment => new { assignment.UserId, assignment.LocalId });

        builder.HasOne<Local>()
            .WithMany()
            .HasForeignKey(assignment => assignment.LocalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
