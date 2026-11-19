using ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.EPP;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Herramientas;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Obras;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Personal;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Seguridad;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Sincronizacion;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Infraestructura.Diagnostico;
using ConstructoraEderTorres.Infraestructura.Evidencias;
using ConstructoraEderTorres.Infraestructura.Identidad;
using ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;
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
        IConfiguration configuracion,
        string raizContenido)
    {
        servicios.AddSingleton<IRelojSistema, RelojSistemaUtc>();
        servicios.AddSingleton(proveedor =>
        {
            var seccion = proveedor.GetRequiredService<IConfiguration>().GetSection(OpcionesCapturaAsistencia.Seccion);
            var predeterminadas = new OpcionesCapturaAsistencia();
            var tolerancia = predeterminadas.ToleranciaRelojFuturo;
            if (seccion["ToleranciaRelojFuturo"] is { } valor
                && !TimeSpan.TryParse(valor, System.Globalization.CultureInfo.InvariantCulture, out tolerancia))
                throw new InvalidOperationException("Asistencia:ToleranciaRelojFuturo debe ser una duración válida.");
            var opciones = new OpcionesCapturaAsistencia
            {
                ZonaHorariaJornada = seccion["ZonaHorariaJornada"] ?? predeterminadas.ZonaHorariaJornada,
                ToleranciaRelojFuturo = tolerancia
            };
            opciones.Validar();
            return opciones;
        });
        servicios.AddSingleton<ICalendarioLaboral, CalendarioLaboral>();
        servicios.AddScoped<PoliticaCapturaAsistencia>();
        servicios.AddScoped<PoliticaOperacionesObra>();
        servicios.AddSingleton<IServicioHashContrasena, ServicioHashContrasenaPbkdf2>();
        servicios.AddSingleton<IAlmacenamientoEvidencias>(proveedor =>
            new AlmacenamientoEvidenciasLocalConfigurable(proveedor.GetRequiredService<IConfiguration>(), raizContenido,
                proveedor.GetService<Microsoft.Extensions.Logging.ILogger<AlmacenamientoEvidenciasLocalConfigurable>>()));
        servicios.AddScoped<IUnidadTrabajo, UnidadTrabajoPostgreSql>();
        servicios.AddSingleton(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<IConfiguration>().GetSection(OpcionesTransacciones.Seccion)
                .Get<OpcionesTransacciones>() ?? new();
            opciones.Validar();
            return opciones;
        });
        servicios.AddScoped<IInicializadorSeguridad, InicializadorSeguridadPostgreSql>();
        servicios.AddSingleton(proveedor =>
        {
            var valor = proveedor.GetRequiredService<IConfiguration>()["Sincronizacion:MaximoOperaciones"];
            if (valor is not null && (!int.TryParse(valor, out var numero) || numero < 1))
                throw new InvalidOperationException("Sincronizacion:MaximoOperaciones debe ser positivo.");
            return new OpcionesSincronizacion { MaximoOperaciones = valor is null ? 100 : int.Parse(valor, System.Globalization.CultureInfo.InvariantCulture) };
        });

        servicios.AddScoped<IServicioAuditoria, ServicioAuditoriaPostgreSql>();
        servicios.AddScoped<IPuertoSeguridad, AdaptadorSeguridadPostgreSql>();
        servicios.AddScoped<IPuertoSesiones, AlmacenSesionesPostgreSql>();
        servicios.AddScoped<IPuertoObras, AdaptadorObrasPostgreSql>();
        servicios.AddScoped<IPuertoPersonal, AdaptadorPersonalPostgreSql>();
        servicios.AddScoped<IPuertoAsistencia, AdaptadorAsistenciaPostgreSql>();
        servicios.AddScoped<IPuertoSeguimientoObra, AdaptadorSeguimientoObraPostgreSql>();
        servicios.AddScoped<IPuertoEpp, AdaptadorEppPostgreSql>();
        servicios.AddScoped<IPuertoHerramientas, AdaptadorHerramientasPostgreSql>();

        servicios.AddScoped<ManejadorSeguridad>();
        servicios.AddScoped<ManejadorObras>();
        servicios.AddScoped<ManejadorPersonal>();
        servicios.AddScoped<ManejadorAsistencia>();
        servicios.AddScoped<ManejadorSeguimientoObra>();
        servicios.AddScoped<ManejadorEpp>();
        servicios.AddScoped<ManejadorHerramientas>();
        servicios.AddScoped<ManejadorSincronizacion>();

        servicios.AddSingleton<NpgsqlDataSource>(proveedor =>
        {
            var configActual = proveedor.GetService<IConfiguration>() ?? configuracion;
            var cadenaConexion = configActual.GetConnectionString("Constructora");
            if (string.IsNullOrWhiteSpace(cadenaConexion))
            {
                throw new ExcepcionPersistencia(new ErrorNegocio("conexion_no_configurada", "Configure ConnectionStrings:Constructora fuera del repositorio.", TipoError.NoDisponible));
            }

            var configuracionConexion = new NpgsqlConnectionStringBuilder(cadenaConexion)
            {
                IncludeErrorDetail = false
            };

            return new NpgsqlDataSourceBuilder(configuracionConexion.ConnectionString).Build();
        });

        servicios.AddDbContext<ContextoConstructora>((proveedor, opciones) =>
            opciones.UseNpgsql(proveedor.GetRequiredService<NpgsqlDataSource>()));

        servicios.AddHealthChecks()
            .AddCheck<ComprobacionPostgreSql>(
                "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["disponibilidad"],
                timeout: TimeSpan.FromSeconds(3));

        return servicios;
    }
}
