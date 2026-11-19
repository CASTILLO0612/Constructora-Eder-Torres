namespace ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

public interface IPuertoSesiones
{
    Task<Guid> CrearAsync(int idUsuario, DateTime emitidaUtc, DateTime expiraUtc, CancellationToken cancelacion = default);
    Task RevocarAsync(Guid idSesion, int idUsuario, DateTime ahoraUtc, CancellationToken cancelacion = default);
}
