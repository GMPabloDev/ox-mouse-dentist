namespace MouseDent.Domain.Locales;

/// <summary>
/// Horario semanal de atención de un local: los siete días de la semana, cada
/// uno cerrado o abierto, con al menos un día abierto. Se construye únicamente
/// con <see cref="Parse"/>, que devuelve los errores en lugar de lanzar, para
/// que el horario inválido no llegue a existir.
/// </summary>
public sealed class WeeklySchedule
{
    /// <summary>Días del horario en el orden en que se exponen: de lunes a domingo.</summary>
    public static readonly IReadOnlyList<DayOfWeek> WeekDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday,
    ];

    private readonly ScheduleDay[] _days;

    private WeeklySchedule(ScheduleDay[] days) => _days = days;

    public IReadOnlyList<ScheduleDay> Days => _days;

    public bool HasOpenDay => _days.Any(day => day.IsOpen);

    /// <summary>
    /// Valida los días recibidos y construye el horario. Un día repetido, un día
    /// ausente, una apertura que no precede al cierre o una semana sin días
    /// abiertos devuelven errores y ningún horario.
    /// </summary>
    public static ScheduleParseResult Parse(IEnumerable<ScheduleDayInput> days)
    {
        var errors = new List<ScheduleError>();
        var byDay = new Dictionary<DayOfWeek, ScheduleDayInput>();

        foreach (var input in days)
        {
            if (!byDay.TryAdd(input.Day, input))
            {
                errors.Add(new ScheduleError(ScheduleMember.Day, input.Day, "El día está repetido en el horario."));
            }
        }

        foreach (var day in WeekDays)
        {
            if (!byDay.ContainsKey(day))
            {
                errors.Add(new ScheduleError(ScheduleMember.Day, day, "Falta el día en el horario."));
            }
        }

        if (errors.Count > 0)
        {
            return ScheduleParseResult.Invalid(errors);
        }

        var week = new ScheduleDay[WeekDays.Count];
        for (var index = 0; index < WeekDays.Count; index++)
        {
            var day = WeekDays[index];
            var input = byDay[day];

            if (input.IsClosed)
            {
                week[index] = ScheduleDay.ClosedOn(day);
                continue;
            }

            if (input.OpensAt >= input.ClosesAt)
            {
                errors.Add(new ScheduleError(
                    ScheduleMember.ClosesAt,
                    day,
                    "La hora de cierre debe ser posterior a la hora de apertura."));
                continue;
            }

            week[index] = ScheduleDay.OpenOn(day, input.OpensAt, input.ClosesAt);
        }

        if (errors.Count > 0)
        {
            return ScheduleParseResult.Invalid(errors);
        }

        if (!week.Any(day => day.IsOpen))
        {
            return ScheduleParseResult.Invalid(
            [
                new ScheduleError(ScheduleMember.Schedule, null, "El horario debe tener al menos un día abierto."),
            ]);
        }

        return ScheduleParseResult.Valid(new WeeklySchedule(week));
    }
}

/// <summary>Día del horario tal como llega antes de validarse.</summary>
public readonly record struct ScheduleDayInput(DayOfWeek Day, bool IsClosed, TimeOnly OpensAt, TimeOnly ClosesAt);

public enum ScheduleMember
{
    /// <summary>Error del horario completo.</summary>
    Schedule,
    Day,
    OpensAt,
    ClosesAt,
}

/// <summary>Error de un horario: el miembro afectado, el día si aplica y el mensaje.</summary>
public sealed record ScheduleError(ScheduleMember Member, DayOfWeek? Day, string Message);

/// <summary>Resultado de <see cref="WeeklySchedule.Parse"/>: el horario válido o sus errores.</summary>
public sealed record ScheduleParseResult
{
    private ScheduleParseResult(WeeklySchedule? schedule, IReadOnlyList<ScheduleError> errors)
    {
        Schedule = schedule;
        Errors = errors;
    }

    public WeeklySchedule? Schedule { get; }

    public IReadOnlyList<ScheduleError> Errors { get; }

    public bool IsValid => Schedule is not null;

    public static ScheduleParseResult Valid(WeeklySchedule schedule) => new(schedule, []);

    public static ScheduleParseResult Invalid(IReadOnlyList<ScheduleError> errors) => new(null, errors);
}
