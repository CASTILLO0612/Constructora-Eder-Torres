using Npgsql;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

public static class ErroresConexionPostgreSql
{
    public static bool EsNoDisponible(Exception excepcion) => excepcion switch
    {
        NpgsqlException { IsTransient: true } => true,
        PostgresException { SqlState: PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown
            or PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.TooManyConnections } => true,
        AggregateException agregada => agregada.InnerExceptions.Any(EsNoDisponible),
        { InnerException: { } interna } => EsNoDisponible(interna),
        _ => false
    };
}
