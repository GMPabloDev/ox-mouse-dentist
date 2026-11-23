using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Catalogo;

namespace MouseDent.Infrastructure.Persistence.Configurations;

/// <summary>
/// El índice único repite la regla de nombre único dentro de la especialidad y
/// el trigger <c>Catalogo_Servicio_Crear</c> de la migración
/// <c>20261120054622_Catalogo</c> impide servicios activos bajo especialidades
/// inactivas. El modelo de EF no conoce ese trigger: un cambio de estas tablas
/// debe revisar el SQL de la migración.
/// </summary>
public sealed class ServicioConfiguration : IEntityTypeConfiguration<Servicio>
{
    public void Configure(EntityTypeBuilder<Servicio> builder)
    {
        builder.ToTable("Servicios");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => new { x.EspecialidadId, x.NormalizedName }).IsUnique();
        builder.HasOne<Especialidad>().WithMany().HasForeignKey(x => x.EspecialidadId).OnDelete(DeleteBehavior.Restrict);
    }
}
