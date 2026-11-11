using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MouseDent.Application.Locales;
using MouseDent.Domain.Locales;
using MouseDent.Infrastructure.Persistence;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Comprueba la persistencia de locales contra la base real: la unicidad del
/// nombre la garantiza el índice, y el horario vuelve del almacén con el mismo
/// estado y las mismas horas.
/// </summary>
public sealed class LocalePersistenceTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    [Fact]
    public async Task The_unique_index_rejects_two_locals_with_the_same_name_ignoring_case()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();

        database.Locales.Add(ExistingLocal("Sede Índice Único"));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        database.Locales.Add(ExistingLocal(" sede índice único "));

        await Should.ThrowAsync<DbUpdateException>(
            async () => await database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_store_reports_a_duplicate_name_instead_of_failing()
    {
        await using var createScope = factory.Services.CreateAsyncScope();
        var store = createScope.ServiceProvider.GetRequiredService<ILocales>();

        var first = await store.SaveAsync(ExistingLocal("Sede Duplicada"), TestContext.Current.CancellationToken);

        await using var secondScope = factory.Services.CreateAsyncScope();
        var otherStore = secondScope.ServiceProvider.GetRequiredService<ILocales>();
        var duplicate = await otherStore.SaveAsync(
            ExistingLocal("  SEDE DUPLICADA "),
            TestContext.Current.CancellationToken);

        first.ShouldBe(LocalSaveResult.Saved);
        duplicate.ShouldBe(LocalSaveResult.DuplicateName);

        await using var readScope = factory.Services.CreateAsyncScope();
        var readStore = readScope.ServiceProvider.GetRequiredService<ILocales>();
        var stored = await readStore.ListAsync(TestContext.Current.CancellationToken);

        stored.Count(local => local.NormalizedName == "SEDE DUPLICADA").ShouldBe(1);
    }

    [Fact]
    public async Task The_schedule_is_read_back_with_its_closed_days_and_hours()
    {
        var schedule = WeeklySchedule.Parse(
        [
            new ScheduleDayInput(DayOfWeek.Monday, false, new TimeOnly(9, 15), new TimeOnly(17, 45)),
            new ScheduleDayInput(DayOfWeek.Tuesday, true, default, default),
            new ScheduleDayInput(DayOfWeek.Wednesday, true, default, default),
            new ScheduleDayInput(DayOfWeek.Thursday, true, default, default),
            new ScheduleDayInput(DayOfWeek.Friday, false, new TimeOnly(10, 0), new TimeOnly(14, 30)),
            new ScheduleDayInput(DayOfWeek.Saturday, true, default, default),
            new ScheduleDayInput(DayOfWeek.Sunday, true, default, default),
        ]).Schedule!;

        Guid id;
        await using (var createScope = factory.Services.CreateAsyncScope())
        {
            var store = createScope.ServiceProvider.GetRequiredService<ILocales>();
            var local = Local.Create("Sede Horario", "Av. Siempre Viva 742", "+51 999 888 777", schedule);
            await store.SaveAsync(local, TestContext.Current.CancellationToken);
            id = local.Id;
        }

        await using var readScope = factory.Services.CreateAsyncScope();
        var readStore = readScope.ServiceProvider.GetRequiredService<ILocales>();
        var stored = await readStore.FindAsync(id, TestContext.Current.CancellationToken);

        stored.ShouldNotBeNull();
        stored.Schedule.Days.Count.ShouldBe(7);
        stored.Schedule.Days[0].ShouldBe(ScheduleDay.OpenOn(DayOfWeek.Monday, new TimeOnly(9, 15), new TimeOnly(17, 45)));
        stored.Schedule.Days[1].IsOpen.ShouldBeFalse();
        stored.Schedule.Days[4].ShouldBe(ScheduleDay.OpenOn(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(14, 30)));
        stored.Schedule.Days[6].IsOpen.ShouldBeFalse();
    }

    private static Local ExistingLocal(string name) =>
        Local.Create(name, "Av. Siempre Viva 742", "+51 999 888 777", WeekFromNineToEighteen());

    private static WeeklySchedule WeekFromNineToEighteen() =>
        WeeklySchedule.Parse(
            WeeklySchedule.WeekDays.Select(day =>
                new ScheduleDayInput(day, false, new TimeOnly(9, 0), new TimeOnly(18, 0)))).Schedule!;
}
