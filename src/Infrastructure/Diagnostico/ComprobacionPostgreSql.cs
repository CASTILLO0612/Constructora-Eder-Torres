using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ConstructoraEderTorres.Infraestructura.Diagnostico;

internal sealed class ComprobacionPostgreSql(
    IConfiguration configuracion,
    IServiceProvider servicios) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contexto,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(configuracion.GetConnectionString("Constructora")))
        {
            return HealthCheckResult.Unhealthy("La conexión de PostgreSQL no está configurada.");
        }

        try
        {
            var origenDatos = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<NpgsqlDataSource>(servicios);
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
