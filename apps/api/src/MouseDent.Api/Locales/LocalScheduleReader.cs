using System.Globalization;
using System.Text.Json;
using MouseDent.Domain.Locales;

namespace MouseDent.Api.Locales;

/// <summary>
/// Lee el horario que llega como texto JSON en el formulario multipart y lo
/// convierte en el horario del dominio. Los errores se devuelven ligados a los
/// campos del formulario, con la misma forma que usa la validación de la API.
/// </summary>
public static class LocalScheduleReader
{
    public const string TimeFormat = "HH:mm";

    private const string ScheduleField = "Schedule";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LocalScheduleReadResult Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LocalScheduleReadResult.Invalid(Errors(ScheduleField, "El horario es obligatorio."));
        }

        ScheduleDayRequest?[]? days;
        try
        {
            days = JsonSerializer.Deserialize<ScheduleDayRequest?[]>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return LocalScheduleReadResult.Invalid(
                Errors(ScheduleField, "El horario no tiene el formato esperado."));
        }

        if (days is null)
        {
            return LocalScheduleReadResult.Invalid(
                Errors(ScheduleField, "El horario no tiene el formato esperado."));
        }

        var problems = new Dictionary<string, string[]>();
        var inputs = new List<ScheduleDayInput>(days.Length);

        for (var position = 0; position < days.Length; position++)
        {
            var day = days[position];
            if (day is null)
            {
                problems[ScheduleField] = ["El horario no tiene el formato esperado."];
                continue;
            }

            // Schedule[n] siempre identifica el día semanal, no la posición del arreglo.
            // Sin un día válido no existe un índice semanal: el error es general.
            if (day.Day is not { } weekDay || !Enum.IsDefined(weekDay))
            {
                problems[ScheduleField] = ["Indica un día válido."];
                continue;
            }

            var index = WeekDayIndex(weekDay);

            if (day.IsClosed)
            {
                inputs.Add(new ScheduleDayInput(WeekDayMap.ToDomain(weekDay), true, TimeOnly.MinValue, TimeOnly.MinValue));
                continue;
            }

            var opensAt = ParseTime(day.OpensAt);
            var closesAt = ParseTime(day.ClosesAt);

            if (opensAt is null)
            {
                problems[$"Schedule[{index}].OpensAt"] = ["Indica la hora de apertura en formato HH:mm."];
            }

            if (closesAt is null)
            {
                problems[$"Schedule[{index}].ClosesAt"] = ["Indica la hora de cierre en formato HH:mm."];
            }

            if (opensAt is null || closesAt is null)
            {
                continue;
            }

            inputs.Add(new ScheduleDayInput(WeekDayMap.ToDomain(weekDay), false, opensAt.Value, closesAt.Value));
        }

        if (problems.Count > 0)
        {
            return LocalScheduleReadResult.Invalid(problems);
        }

        var parsed = WeeklySchedule.Parse(inputs);
        if (parsed.Schedule is { } schedule)
        {
            return LocalScheduleReadResult.Valid(schedule);
        }

        foreach (var error in parsed.Errors)
        {
            problems[FieldOf(error)] = [error.Message];
        }

        return LocalScheduleReadResult.Invalid(problems);
    }

    private sealed record ScheduleDayRequest(LocalWeekDay? Day, bool IsClosed, string? OpensAt, string? ClosesAt);

    public static LocalScheduleDay ToContract(ScheduleDay day) => day switch
    {
        ScheduleDay.OpenDay open => new(
            WeekDayMap.ToContract(open.Day),
            false,
            open.OpensAt.ToString(TimeFormat, CultureInfo.InvariantCulture),
            open.ClosesAt.ToString(TimeFormat, CultureInfo.InvariantCulture)),
        _ => new(WeekDayMap.ToContract(day.Day), true, null, null),
    };

    private static string FieldOf(ScheduleError error) => error switch
    {
        { Member: ScheduleMember.Day, Day: { } day } => $"Schedule[{WeekDayIndex(WeekDayMap.ToContract(day))}].Day",
        { Member: ScheduleMember.OpensAt, Day: { } day } => $"Schedule[{WeekDayIndex(WeekDayMap.ToContract(day))}].OpensAt",
        { Member: ScheduleMember.ClosesAt, Day: { } day } => $"Schedule[{WeekDayIndex(WeekDayMap.ToContract(day))}].ClosesAt",
        _ => ScheduleField,
    };

    private static int WeekDayIndex(LocalWeekDay day) => (int)day;

    private static TimeOnly? ParseTime(string? value) =>
        TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private static Dictionary<string, string[]> Errors(string field, string message) =>
        new() { [field] = [message] };
}

/// <summary>Resultado de leer el horario del formulario.</summary>
public sealed record LocalScheduleReadResult
{
    private LocalScheduleReadResult(WeeklySchedule? schedule, IReadOnlyDictionary<string, string[]> errors)
    {
        Schedule = schedule;
        Errors = errors;
    }

    public WeeklySchedule? Schedule { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public bool IsValid => Schedule is not null;

    public static LocalScheduleReadResult Valid(WeeklySchedule schedule) =>
        new(schedule, new Dictionary<string, string[]>());

    public static LocalScheduleReadResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, errors);
}

/// <summary>Traduce los días del contrato a los del dominio y viceversa.</summary>
public static class WeekDayMap
{
    public static DayOfWeek ToDomain(LocalWeekDay day) => day switch
    {
        LocalWeekDay.Monday => DayOfWeek.Monday,
        LocalWeekDay.Tuesday => DayOfWeek.Tuesday,
        LocalWeekDay.Wednesday => DayOfWeek.Wednesday,
        LocalWeekDay.Thursday => DayOfWeek.Thursday,
        LocalWeekDay.Friday => DayOfWeek.Friday,
        LocalWeekDay.Saturday => DayOfWeek.Saturday,
        LocalWeekDay.Sunday => DayOfWeek.Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(day)),
    };

    public static LocalWeekDay ToContract(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => LocalWeekDay.Monday,
        DayOfWeek.Tuesday => LocalWeekDay.Tuesday,
        DayOfWeek.Wednesday => LocalWeekDay.Wednesday,
        DayOfWeek.Thursday => LocalWeekDay.Thursday,
        DayOfWeek.Friday => LocalWeekDay.Friday,
        DayOfWeek.Saturday => LocalWeekDay.Saturday,
        _ => LocalWeekDay.Sunday,
    };
}
