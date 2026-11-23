using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MouseDent.Domain.Profesionales;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Profesionales;

namespace MouseDent.Infrastructure.Persistence.Configurations;

/// <summary>
/// La ficha se vincula a una cuenta mediante una relación uno a uno obligatoria,
/// de modo que el índice único de <c>StaffAccountId</c> impide dos fichas para la
/// misma cuenta. La colegiatura normalizada tiene un índice único parcial: es
/// único entre todas las fichas, también las inactivas, y varias fichas pueden
/// quedarse sin número.
/// </summary>
public sealed class ProfesionalGuardadoConfiguration : IEntityTypeConfiguration<ProfesionalGuardado>
{
    public void Configure(EntityTypeBuilder<ProfesionalGuardado> builder)
    {
        builder.ToTable("Profesionales");

        builder.HasKey(ficha => ficha.Id);

        builder.Property(ficha => ficha.Colegiatura)
            .HasMaxLength(DatosDeColegiatura.LongitudMaxima);

        builder.Property(ficha => ficha.ColegiaturaNormalizada)
            .HasMaxLength(DatosDeColegiatura.LongitudMaxima);

        // La ficha no existe sin cuenta y la cuenta tiene como máximo una ficha.
        builder.HasOne<StaffUser>()
            .WithOne()
            .HasForeignKey<ProfesionalGuardado>(ficha => ficha.StaffAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ficha => ficha.ColegiaturaNormalizada)
            .IsUnique()
            .HasFilter("\"ColegiaturaNormalizada\" IS NOT NULL");

        builder.HasMany(ficha => ficha.Especialidades)
            .WithOne(asignada => asignada.Profesional)
            .HasForeignKey(asignada => asignada.ProfesionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(ficha => ficha.Locales)
            .WithOne(asignado => asignado.Profesional)
            .HasForeignKey(asignado => asignado.ProfesionalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
