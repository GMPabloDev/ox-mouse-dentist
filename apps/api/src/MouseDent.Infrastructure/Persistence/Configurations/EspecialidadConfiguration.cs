using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Catalogo;

namespace MouseDent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Las reglas que cruzan especialidades y servicios se comprueban en Application
/// y, frente a escrituras simultáneas, en los triggers de la migración
/// <c>20261120054622_Catalogo</c>. El modelo de EF no conoce esos triggers, así
/// que un cambio de estas tablas debe revisar el SQL de la migración.
/// </summary>
public sealed class EspecialidadConfiguration : IEntityTypeConfiguration<Especialidad>
{
    public void Configure(EntityTypeBuilder<Especialidad> builder)
    {
        builder.ToTable("Especialidades");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}
