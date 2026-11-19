namespace ConstructoraEderTorres.Aplicacion.Comun;

public interface ISolicitudSincronizable
{
    Guid? UuidRegistro { get; }
}

public interface IRespuestaIdempotente
{
    bool FueIdempotente { get; }
}
