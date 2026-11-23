using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Locales;
using MouseDent.Infrastructure.Profesionales;

namespace MouseDent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabla de relación entre fichas y locales de atención. Solo guarda el
/// identificador asignado: el estado del local vive en <see cref="Local"/>. La
/// clave compuesta impide repetir el mismo local en una ficha.
/// </summary>
public sealed class ProfesionalLocalConfiguration : IEntityTypeConfiguration<ProfesionalLocal>
{
    public void Configure(EntityTypeBuilder<ProfesionalLocal> builder)
    {
        builder.ToTable("ProfesionalLocales");

        builder.HasKey(asignado => new { asignado.ProfesionalId, asignado.LocalId });

        builder.HasOne<Local>()
            .WithMany()
            .HasForeignKey(asignado => asignado.LocalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
