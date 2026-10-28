using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ConstructoraEderTorres.Infraestructura.Diagnostico;

internal sealed class ComprobacionPostgreSql(NpgsqlDataSource origenDatos) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contexto,
        CancellationToken cancelacion = default)
    {
        try
        {
            await using var conexion = await origenDatos.OpenConnectionAsync(cancelacion);
            await using var comando = new NpgsqlCommand("SELECT 1", conexion);
            await comando.ExecuteScalarAsync(cancelacion);

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("La comprobación de PostgreSQL no finalizó a tiempo.");
        }
        catch (NpgsqlException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL no está disponible.");
        }
        catch (TimeoutException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL no respondió a tiempo.");
        }
    }
}
