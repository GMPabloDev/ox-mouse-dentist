using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MouseDent.Domain.Locales;

namespace MouseDent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Guarda el horario semanal como un único documento JSON. El horario no se
/// compara ni se ordena en SQL, y al leerlo se reconstruye con
/// <see cref="WeeklySchedule.Parse"/>, así que la base nunca devuelve un
/// horario que el dominio considere inválido.
/// </summary>
internal sealed class WeeklyScheduleConverter : ValueConverter<WeeklySchedule, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public WeeklyScheduleConverter()
        : base(
            schedule => JsonSerializer.Serialize(schedule.Days.Select(StoredDay.From), Options),
            json => Restore(json))
    {
    }

    private static WeeklySchedule Restore(string json)
    {
        var stored = JsonSerializer.Deserialize<StoredDay[]>(json, Options)
            ?? throw new InvalidOperationException("El horario almacenado está vacío.");

        var parsed = WeeklySchedule.Parse(stored.Select(day => day.ToInput()));

        return parsed.Schedule
            ?? throw new InvalidOperationException("El horario almacenado no es válido.");
    }

    private sealed record StoredDay(string Day, bool IsClosed, string? OpensAt, string? ClosesAt)
    {
        public static StoredDay From(ScheduleDay day) => day switch
        {
            ScheduleDay.OpenDay open => new(
                open.Day.ToString(),
                false,
                open.OpensAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                open.ClosesAt.ToString("HH:mm", CultureInfo.InvariantCulture)),
            _ => new(day.Day.ToString(), true, null, null),
        };

        public ScheduleDayInput ToInput() => new(
            Enum.Parse<DayOfWeek>(Day),
            IsClosed,
            OpensAt is null ? TimeOnly.MinValue : TimeOnly.ParseExact(OpensAt, "HH:mm", CultureInfo.InvariantCulture),
            ClosesAt is null ? TimeOnly.MinValue : TimeOnly.ParseExact(ClosesAt, "HH:mm", CultureInfo.InvariantCulture));
    }
}
