namespace ConstructoraEderTorres.Presentacion.Configuracion;

public enum PerfilLimiteApi { Consulta, Escritura, Asistencia, Ubicacion, Sincronizacion, Evidencia }
public sealed record LimiteOperacionApi(PerfilLimiteApi Perfil);

public sealed class LimiteSolicitudes
{
    public int Permisos { get; init; } = 120;
    public TimeSpan Ventana { get; init; } = TimeSpan.FromMinutes(1);
    public int MaximoSimultaneo { get; init; }
}

public sealed class OpcionesLimitesSolicitudes
{
    public const string Seccion = "Api:LimitesSolicitudes";
    public LimiteSolicitudes Consulta { get; init; } = new() { Permisos = 300 };
    public LimiteSolicitudes Escritura { get; init; } = new();
    public LimiteSolicitudes Asistencia { get; init; } = new() { Permisos = 60 };
    public LimiteSolicitudes Ubicacion { get; init; } = new();
    public LimiteSolicitudes Sincronizacion { get; init; } = new() { Permisos = 20, MaximoSimultaneo = 4 };
    public LimiteSolicitudes Evidencia { get; init; } = new() { Permisos = 10, MaximoSimultaneo = 4 };

    public LimiteSolicitudes Obtener(PerfilLimiteApi perfil) => perfil switch
    {
        PerfilLimiteApi.Consulta => Consulta,
        PerfilLimiteApi.Escritura => Escritura,
        PerfilLimiteApi.Asistencia => Asistencia,
        PerfilLimiteApi.Ubicacion => Ubicacion,
        PerfilLimiteApi.Sincronizacion => Sincronizacion,
        PerfilLimiteApi.Evidencia => Evidencia,
        _ => throw new ArgumentOutOfRangeException(nameof(perfil))
    };

    public void Validar()
    {
        foreach (var perfil in Enum.GetValues<PerfilLimiteApi>())
        {
            var limite = Obtener(perfil);
            if (limite.Permisos is < 1 or > 100_000 || limite.Ventana <= TimeSpan.Zero
                || limite.Ventana > TimeSpan.FromDays(1) || limite.MaximoSimultaneo is < 0 or > 10_000)
                throw new InvalidOperationException($"El límite de solicitudes {perfil} no es válido.");
        }
    }
}
