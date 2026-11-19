namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Asignación de un local a una cuenta de recepción. Se conserva cuando el local
/// se desactiva: el local inactivo simplemente no autoriza operaciones.
/// </summary>
public sealed class StaffLocalAssignment
{
    public Guid UserId { get; set; }

    public StaffUser User { get; set; } = null!;

    public Guid LocalId { get; set; }
}
