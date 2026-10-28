using ConstructoraEderTorres.Infraestructura.Diagnostico;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ConstructoraEderTorres.Infraestructura.Configuracion;

public static class InyeccionDependencias
{
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var cadenaConexion = configuracion.GetConnectionString("Constructora");
        var comprobaciones = servicios.AddHealthChecks();

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            comprobaciones.AddCheck(
                "postgresql",
                () => HealthCheckResult.Unhealthy("La conexión de PostgreSQL no está configurada."),
                tags: ["disponibilidad"]);

            return servicios;
        }

        var configuracionConexion = new NpgsqlConnectionStringBuilder(cadenaConexion)
        {
            IncludeErrorDetail = false
        };

        servicios.AddSingleton<NpgsqlDataSource>(_ =>
            new NpgsqlDataSourceBuilder(configuracionConexion.ConnectionString).Build());

        servicios.AddDbContext<ContextoConstructora>((proveedor, opciones) =>
            opciones.UseNpgsql(proveedor.GetRequiredService<NpgsqlDataSource>()));

        comprobaciones.AddCheck<ComprobacionPostgreSql>(
            "postgresql",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["disponibilidad"],
            timeout: TimeSpan.FromSeconds(3));

        return servicios;
    }
}
