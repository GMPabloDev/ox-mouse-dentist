using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;
using MouseDent.Domain.Locales;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Profesionales;

namespace MouseDent.Infrastructure.Persistence;

/// <summary>
/// Contexto único del MVP. Identity y el modelo de dominio comparten base SQLite.
/// El modelo de dominio se agrega aquí en las specs de H-01 en adelante.
/// </summary>
public sealed class MouseDentDbContext(DbContextOptions<MouseDentDbContext> options)
    : IdentityDbContext<StaffUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Especialidad> Especialidades => Set<Especialidad>();

    public DbSet<Servicio> Servicios => Set<Servicio>();

    public DbSet<Local> Locales => Set<Local>();

    public DbSet<StaffAccessLink> StaffAccessLinks => Set<StaffAccessLink>();

    public DbSet<ProfesionalGuardado> Profesionales => Set<ProfesionalGuardado>();

    public DbSet<ProfesionalEspecialidad> ProfesionalEspecialidades => Set<ProfesionalEspecialidad>();

    public DbSet<ProfesionalLocal> ProfesionalLocales => Set<ProfesionalLocal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(MouseDentDbContext).Assembly);
    }
}
