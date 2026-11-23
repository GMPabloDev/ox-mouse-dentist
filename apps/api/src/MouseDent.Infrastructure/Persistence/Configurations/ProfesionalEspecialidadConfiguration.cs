using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Catalogo;
using MouseDent.Infrastructure.Profesionales;

namespace MouseDent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabla de relación entre fichas y especialidades. Solo guarda el identificador
/// asignado: el estado de la especialidad vive en el catálogo. La clave compuesta
/// impide repetir la misma especialidad en una ficha.
/// </summary>
public sealed class ProfesionalEspecialidadConfiguration
    : IEntityTypeConfiguration<ProfesionalEspecialidad>
{
    public void Configure(EntityTypeBuilder<ProfesionalEspecialidad> builder)
    {
        builder.ToTable("ProfesionalEspecialidades");

        builder.HasKey(asignada => new { asignada.ProfesionalId, asignada.EspecialidadId });

        builder.HasOne<Especialidad>()
            .WithMany()
            .HasForeignKey(asignada => asignada.EspecialidadId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
