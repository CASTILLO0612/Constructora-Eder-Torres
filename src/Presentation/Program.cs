using ConstructoraEderTorres.Infraestructura.Configuracion;
using ConstructoraEderTorres.Presentacion.Configuracion;
using ConstructoraEderTorres.Presentacion.Comandos;
using ConstructoraEderTorres.Presentacion.Intermediarios;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Asistencia;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.EPP;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Herramientas;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Obras;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Personal;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Salud;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.SeguimientoObra;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Seguridad;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Sincronizacion;
using Scalar.AspNetCore;
using ConstructoraEderTorres.Presentacion.Diagnostico;

var constructor = WebApplication.CreateBuilder(args);

constructor.Services.AgregarPresentacion();
constructor.Services.AgregarObservabilidad();
constructor.Services.AgregarConfiguracionDespliegue();
constructor.Services.AgregarInfraestructura(constructor.Configuration, constructor.Environment.ContentRootPath);

var aplicacion = constructor.Build();
_ = aplicacion.Services.GetRequiredService<OpcionesDespliegue>();

if (args.Contains("--inicializar-administrador", StringComparer.Ordinal))
{
    Environment.ExitCode = await ComandoInicializarAdministrador.EjecutarAsync(aplicacion.Services, aplicacion.Configuration);
    await aplicacion.DisposeAsync();
    return;
}

aplicacion.UseForwardedHeaders();
aplicacion.UseMiddleware<IntermediarioCabecerasSeguridad>();
aplicacion.UseMiddleware<IntermediarioDiagnosticoApi>();
aplicacion.UseExceptionHandler();
aplicacion.UseStatusCodePages();
aplicacion.UseRouting();

_ = aplicacion.Services.GetRequiredService<OpcionesSesion>();
_ = aplicacion.Services.GetRequiredService<ConstructoraEderTorres.Aplicacion.Contratos.Asistencia.OpcionesCapturaAsistencia>();
aplicacion.UseCors(ConstructoraEderTorres.Presentacion.Configuracion.InyeccionDependencias.PoliticaCorsConstructora);
aplicacion.UseMiddleware<IntermediarioLimitesApi>();

if (aplicacion.Environment.IsDevelopment())
{
    aplicacion.MapOpenApi();
    aplicacion.MapScalarApiReference("/scalar", opciones =>
        opciones.WithTitle("Constructora Eder Torres — API"));
}
else
{
    aplicacion.UseHsts();
    aplicacion.UseHttpsRedirection();
}

aplicacion.UseAuthentication();
aplicacion.UseRateLimiter();
aplicacion.UseAuthorization();
aplicacion.UseMiddleware<IntermediarioAntifalsificacion>();

aplicacion.MapearPuntosAccesoSalud();
var api = aplicacion.MapGroup("").RequireAuthorization()
    .AddEndpointFilter<FiltroContratoSolicitud>()
    .AddEndpointFilter<FiltroTransaccionApi>();
api.MapearPuntosAccesoSeguridad();
api.MapearPuntosAccesoObras();
api.MapearPuntosAccesoPersonal();
api.MapearPuntosAccesoAsistencia();
api.MapearPuntosAccesoSeguimientoObra();
api.MapearPuntosAccesoEpp();
api.MapearPuntosAccesoHerramientas();
api.MapearPuntosAccesoSincronizacion();

aplicacion.Run();

public partial class Program
{
}
