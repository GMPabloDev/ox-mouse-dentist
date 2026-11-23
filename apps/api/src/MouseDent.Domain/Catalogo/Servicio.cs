namespace MouseDent.Domain.Catalogo;

public sealed class Servicio
{
    private Servicio() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid EspecialidadId { get; private set; }
    public bool IsActive { get; private set; }

    public static Servicio Create(string name, string? description, Especialidad especialidad)
    {
        EnsureActive(especialidad);
        var result = new Servicio { Id = Guid.NewGuid(), EspecialidadId = especialidad.Id, IsActive = true };
        result.Update(name, description);
        return result;
    }

    public void Update(string name, string? description)
    {
        var data = CatalogoDatos.Validate(name, description);
        Name = data.Name;
        NormalizedName = data.NormalizedName;
        Description = data.Description;
    }

    public void MoveTo(Especialidad especialidad)
    {
        EnsureActive(especialidad);
        EspecialidadId = especialidad.Id;
    }

    public void Activate(Especialidad especialidad)
    {
        ArgumentNullException.ThrowIfNull(especialidad);
        if (especialidad.Id != EspecialidadId)
        {
            throw new ArgumentException("La especialidad no corresponde al servicio.", nameof(especialidad));
        }

        EnsureActive(especialidad);
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    private static void EnsureActive(Especialidad especialidad)
    {
        ArgumentNullException.ThrowIfNull(especialidad);
        if (!especialidad.IsActive)
        {
            throw new InvalidOperationException("La especialidad debe estar activa.");
        }
    }
}
