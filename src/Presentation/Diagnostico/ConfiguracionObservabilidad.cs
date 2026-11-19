using ConstructoraEderTorres.Aplicacion.Comun;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ConstructoraEderTorres.Presentacion.Diagnostico;

public static class ConfiguracionObservabilidad
{
    public static IServiceCollection AgregarObservabilidad(this IServiceCollection servicios)
    {
        servicios.AddSingleton(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<IConfiguration>().GetSection(OpcionesObservabilidad.Seccion)
                .Get<OpcionesObservabilidad>() ?? new();
            opciones.Validar();
            return opciones;
        });
        servicios.AddOpenTelemetry().WithTracing(trazas => trazas
            .AddSource(DiagnosticoAplicacion.Nombre)
            .AddAspNetCoreInstrumentation(instrumentacion =>
            {
                instrumentacion.RecordException = false;
                instrumentacion.Filter = contexto => contexto.Request.Path.StartsWithSegments("/api")
                    && contexto.RequestServices.GetRequiredService<OpcionesObservabilidad>().Habilitada;
            })
            .AddProcessor(new ProcesadorPrivacidadTrazas())).WithMetrics();
        servicios.ConfigureOpenTelemetryTracerProvider((proveedor, trazas) =>
        {
            var opciones = proveedor.GetRequiredService<OpcionesObservabilidad>();
            if (!opciones.Habilitada) { trazas.SetSampler(new AlwaysOffSampler()); return; }
            trazas.SetResourceBuilder(CrearRecurso(proveedor, opciones))
                .SetSampler(new TraceIdRatioBasedSampler(opciones.FraccionMuestreo));
            if (!string.IsNullOrWhiteSpace(opciones.DestinoOtlp))
                trazas.AddProcessor(new BatchActivityExportProcessor(new OtlpTraceExporter(CrearDestino(opciones, "v1/traces"))));
        });
        servicios.ConfigureOpenTelemetryMeterProvider((proveedor, metricas) =>
        {
            var opciones = proveedor.GetRequiredService<OpcionesObservabilidad>();
            if (!opciones.Habilitada) return;
            metricas.SetResourceBuilder(CrearRecurso(proveedor, opciones))
                .AddMeter(DiagnosticoAplicacion.Nombre, "System.Runtime");
            if (!string.IsNullOrWhiteSpace(opciones.DestinoOtlp))
                metricas.AddReader(new PeriodicExportingMetricReader(new OtlpMetricExporter(CrearDestino(opciones, "v1/metrics"))));
        });
        return servicios;
    }

    private static ResourceBuilder CrearRecurso(IServiceProvider proveedor, OpcionesObservabilidad opciones) =>
        ResourceBuilder.CreateEmpty().AddService(opciones.NombreServicio,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString())
            .AddAttributes([new("deployment.environment.name", proveedor.GetRequiredService<IHostEnvironment>().EnvironmentName)]);

    private static OtlpExporterOptions CrearDestino(OpcionesObservabilidad opciones, string ruta) => new()
    {
        Endpoint = new Uri(new Uri(opciones.DestinoOtlp!.TrimEnd('/') + "/"), ruta),
        Protocol = OtlpExportProtocol.HttpProtobuf,
        Headers = opciones.CabecerasOtlp
    };
}
