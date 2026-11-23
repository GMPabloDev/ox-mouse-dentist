namespace MouseDent.Domain.Catalogo;

public sealed class Especialidad
{
    private Especialidad() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public static Especialidad Create(string name, string? description)
    {
        var result = new Especialidad { Id = Guid.NewGuid(), IsActive = true };
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

    public void Activate() => IsActive = true;

    /// <summary>
    /// Retira la especialidad del catálogo. Application comprueba antes que no
    /// tenga servicios activos; los triggers de SQLite repiten la comprobación
    /// frente a escrituras simultáneas.
    /// </summary>
    public void Deactivate() => IsActive = false;
}
