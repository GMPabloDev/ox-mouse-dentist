using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MouseDent.Application.Catalogo;
using MouseDent.Domain.Catalogo;
using MouseDent.Infrastructure.Catalogo;
using MouseDent.Infrastructure.Persistence;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class CatalogoPersistenceTests
{
    [Fact]
    public async Task Names_are_unique_including_inactive_records_and_moves_are_atomic()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var db = CreateContext(path);
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var store = new EfCatalogo(db);
            var first = Especialidad.Create("Dental", null);
            var second = Especialidad.Create("Otra", null);
            (await store.SaveAsync(first, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            (await store.SaveAsync(second, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            var firstService = Servicio.Create("Limpieza", null, first);
            firstService.Deactivate();
            (await store.SaveAsync(firstService, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            var secondService = Servicio.Create(" limpieza ", null, second);
            (await store.SaveAsync(secondService, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            secondService.MoveTo(first);
            (await store.SaveAsync(secondService, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.DuplicateName);
            await using var verify = CreateContext(path);
            (await verify.Servicios.SingleAsync(x => x.Id == secondService.Id, TestContext.Current.CancellationToken)).EspecialidadId.ShouldBe(second.Id);
            var duplicate = Especialidad.Create(" dental ", null);
            (await new EfCatalogo(verify).SaveAsync(duplicate, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.DuplicateName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Stale_writers_cannot_create_active_services_under_inactive_specialties()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var specialty = Especialidad.Create("Dental", null);
            (await new EfCatalogo(setup).SaveAsync(specialty, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            await using var writer1 = CreateContext(path);
            await using var writer2 = CreateContext(path);
            var old1 = (await writer1.Especialidades.SingleAsync(TestContext.Current.CancellationToken));
            var old2 = (await writer2.Especialidades.SingleAsync(TestContext.Current.CancellationToken));
            old1.Deactivate();
            (await new EfCatalogo(writer1).SaveAsync(old1, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            var service = Servicio.Create("Limpieza", null, old2);
            (await new EfCatalogo(writer2).SaveAsync(service, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.DestinationInactive);
            await using var verify = CreateContext(path);
            (await verify.Servicios.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
            (await verify.Especialidades.SingleAsync(TestContext.Current.CancellationToken)).IsActive.ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Stale_deactivation_is_rejected_after_service_creation()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var specialty = Especialidad.Create("Dental", null);
            (await new EfCatalogo(setup).SaveAsync(specialty, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            await using var writer1 = CreateContext(path);
            await using var writer2 = CreateContext(path);
            var old = await writer1.Especialidades.SingleAsync(TestContext.Current.CancellationToken);
            var active = await writer2.Especialidades.SingleAsync(TestContext.Current.CancellationToken);
            (await new EfCatalogo(writer2).SaveAsync(Servicio.Create("Limpieza", null, active), TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.Saved);
            old.Deactivate();
            (await new EfCatalogo(writer1).SaveAsync(old, TestContext.Current.CancellationToken)).ShouldBe(CatalogoSaveResult.EspecialidadHasActiveServices);
            await using var verify = CreateContext(path);
            (await verify.Especialidades.SingleAsync(TestContext.Current.CancellationToken)).IsActive.ShouldBeTrue();
            (await verify.Servicios.SingleAsync(TestContext.Current.CancellationToken)).IsActive.ShouldBeTrue();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Concurrent_duplicate_creations_leave_only_one_record()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var cancellationToken = TestContext.Current.CancellationToken;
            using var gate = new ManualResetEventSlim();
            async Task<CatalogoSaveResult> Create(string name)
            {
                await Task.Yield();
                gate.Wait(cancellationToken);
                await using var db = CreateContext(path);
                return await new EfCatalogo(db).SaveAsync(Especialidad.Create(name, null), cancellationToken);
            }
            var first = Task.Run(() => Create("Dental"), cancellationToken);
            var second = Task.Run(() => Create(" dental "), cancellationToken);
            gate.Set();
            var results = await Task.WhenAll(first, second);
            results.Count(x => x == CatalogoSaveResult.Saved).ShouldBe(1);
            results.Count(x => x == CatalogoSaveResult.DuplicateName).ShouldBe(1);
            (await setup.Especialidades.CountAsync(cancellationToken)).ShouldBe(1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Migration_creates_the_write_guard_triggers()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var db = CreateContext(path);
            var token = TestContext.Current.CancellationToken;
            await db.Database.MigrateAsync(token);

            var triggers = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'Catalogo%'")
                .ToListAsync(token);

            triggers.ShouldBe(
                ["Catalogo_Especialidad_Desactivar", "Catalogo_Servicio_Actualizar", "Catalogo_Servicio_Crear"],
                ignoreOrder: true);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Migration_preserves_existing_identity_data()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var db = CreateContext(path);
            var token = TestContext.Current.CancellationToken;
            await db.GetService<IMigrator>().MigrateAsync("20261112013036_AddStaffAccessLinks", token);
            await db.Database.ExecuteSqlRawAsync("INSERT INTO AspNetRoles (Id, Name, NormalizedName) VALUES ('11111111-1111-1111-1111-111111111111', 'Existente', 'EXISTENTE')", token);
            await db.Database.MigrateAsync(token);
            (await db.Roles.CountAsync(x => x.NormalizedName == "EXISTENTE", token)).ShouldBe(1);
            (await db.Especialidades.CountAsync(token)).ShouldBe(0);
        }
        finally { File.Delete(path); }
    }

    private static MouseDentDbContext CreateContext(string path) => new(
        new DbContextOptionsBuilder<MouseDentDbContext>().UseSqlite($"Data Source={path}").Options);
}
