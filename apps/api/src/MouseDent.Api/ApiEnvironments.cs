namespace MouseDent.Api;

public static class ApiEnvironments
{
    /// <summary>
    /// Entorno usado por la generación de OpenAPI en build. No arranca la base
    /// de datos ni aplica el control de clave de despliegue.
    /// </summary>
    public const string OpenApiBuild = "OpenApiBuild";
}
