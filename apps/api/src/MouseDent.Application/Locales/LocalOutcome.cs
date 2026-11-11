using MouseDent.Domain.Locales;

namespace MouseDent.Application.Locales;

public enum LocalOutcomeStatus
{
    Saved,
    NotFound,
    DuplicateName,
}

/// <summary>Resultado de una operación de gestión de locales.</summary>
public sealed record LocalOutcome
{
    private LocalOutcome(Local? local, LocalOutcomeStatus status)
    {
        Local = local;
        Status = status;
    }

    public Local? Local { get; }

    public LocalOutcomeStatus Status { get; }

    public static LocalOutcome Saved(Local local) => new(local, LocalOutcomeStatus.Saved);

    public static LocalOutcome NotFound() => new(null, LocalOutcomeStatus.NotFound);

    public static LocalOutcome DuplicateName() => new(null, LocalOutcomeStatus.DuplicateName);
}
