namespace MouseDent.Domain.Locales;

/// <summary>
/// Estado de un día del horario semanal. Un día está cerrado o abierto con una
/// hora de apertura anterior a la de cierre. No existen días abiertos sin horas
/// ni días cerrados con horas: los constructores son privados y solo se llega a
/// un día válido por <see cref="ClosedOn"/> u <see cref="OpenOn"/>.
/// </summary>
public abstract record ScheduleDay
{
    private ScheduleDay(DayOfWeek day) => Day = day;

    public DayOfWeek Day { get; }

    public abstract bool IsOpen { get; }

    /// <summary>Día cerrado, sin horas de atención.</summary>
    public static ScheduleDay ClosedOn(DayOfWeek day) => new ClosedDay(day);

    /// <summary>Día abierto con un único tramo dentro del mismo día.</summary>
    public static ScheduleDay OpenOn(DayOfWeek day, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (opensAt >= closesAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closesAt),
                "La hora de cierre debe ser posterior a la hora de apertura.");
        }

        return new OpenDay(day, opensAt, closesAt);
    }

    public sealed record ClosedDay : ScheduleDay
    {
        internal ClosedDay(DayOfWeek day) : base(day)
        {
        }

        public override bool IsOpen => false;
    }

    public sealed record OpenDay : ScheduleDay
    {
        internal OpenDay(DayOfWeek day, TimeOnly opensAt, TimeOnly closesAt) : base(day)
        {
            OpensAt = opensAt;
            ClosesAt = closesAt;
        }

        public TimeOnly OpensAt { get; }

        public TimeOnly ClosesAt { get; }

        public override bool IsOpen => true;
    }
}
