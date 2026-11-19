using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Presentacion.Diagnostico;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class ObservabilidadApiPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private sealed record Traza(string Nombre, Dictionary<string, object?> Etiquetas,
        KeyValuePair<string,string?>[] Equipaje, string? Estado, string? Descripcion);
    private sealed class CapturadorTrazas : BaseExporter<Activity>
    {
        public ConcurrentQueue<Traza> Trazas { get; } = new();
        public override ExportResult Export(in Batch<Activity> elementos)
        {
            foreach (var actividad in elementos)
                Trazas.Enqueue(new(actividad.DisplayName, actividad.TagObjects.ToDictionary(e => e.Key, e => e.Value),
                    actividad.Baggage.ToArray(), actividad.TraceStateString, actividad.StatusDescription));
            return ExportResult.Success;
        }
    }

    [Fact]
    public void ProcesadorEliminaDatosDeIdentidadSqlCookiesGpsYEquipaje()
    {
        using var exportador = new CapturadorTrazas();
        using var proveedor = Sdk.CreateTracerProviderBuilder().AddSource("Pruebas.Privacidad")
            .AddProcessor(new ProcesadorPrivacidadTrazas())
            .AddProcessor(new SimpleActivityExportProcessor(exportador)).Build();
        using var fuente = new ActivitySource("Pruebas.Privacidad");
        using (var actividad = fuente.StartActivity("/api/asistencias/88?token=SECRETO_TRAZA", ActivityKind.Server))
        {
            Assert.NotNull(actividad);
            actividad.SetTag("http.request.method", "POST");
            actividad.SetTag("http.route", "/api/asistencias/{idAsistencia}");
            actividad.SetTag("http.request.header.cookie", "SECRETO_TRAZA");
            actividad.SetTag("url.query", "SECRETO_TRAZA");
            actividad.SetTag("db.statement", "SELECT 'SECRETO_TRAZA'");
            actividad.SetTag("latitud", "SECRETO_TRAZA");
            actividad.SetTag("id_usuario", "SECRETO_TRAZA");
            actividad.SetBaggage("usuario", "SECRETO_TRAZA");
            actividad.TraceStateString = "prueba=SECRETO_TRAZA";
            actividad.SetStatus(ActivityStatusCode.Error, "SECRETO_TRAZA");
        }
        var traza = Assert.Single(exportador.Trazas);
        Assert.Equal("POST /api/asistencias/{idAsistencia}", traza.Nombre);
        Assert.Empty(traza.Equipaje);
        Assert.DoesNotContain("SECRETO_TRAZA", JsonSerializer.Serialize(traza));
    }

    [Fact]
    public void TrazaConEventoNoEditableSeDescartaAntesDeExportar()
    {
        using var exportador = new CapturadorTrazas();
        using var proveedor = Sdk.CreateTracerProviderBuilder().AddSource("Pruebas.EventosPrivados")
            .AddProcessor(new ProcesadorPrivacidadTrazas())
            .AddProcessor(new SimpleActivityExportProcessor(exportador)).Build();
        using var fuente = new ActivitySource("Pruebas.EventosPrivados");
        using (var actividad = fuente.StartActivity("evento"))
            actividad!.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            { ["exception.message"] = "SECRETO_TRAZA" }));
        Assert.Empty(exportador.Trazas);
    }

    [Fact]
    public async Task ApiExportaRutaPlantillaYMetricasSinContenidoDeSolicitud()
    {
        using var exportador = new CapturadorTrazas();
        var medidas = new ConcurrentQueue<(string Nombre, Dictionary<string,object?> Etiquetas)>();
        using var oyente = new MeterListener();
        oyente.InstrumentPublished = (instrumento, suscriptor) =>
        { if (instrumento.Meter.Name == DiagnosticoAplicacion.Nombre) suscriptor.EnableMeasurementEvents(instrumento); };
        oyente.SetMeasurementEventCallback<long>((i, _, etiquetas, _) => medidas.Enqueue((i.Name, etiquetas.ToArray().ToDictionary(e => e.Key, e => e.Value))));
        oyente.SetMeasurementEventCallback<double>((i, _, etiquetas, _) => medidas.Enqueue((i.Name, etiquetas.ToArray().ToDictionary(e => e.Key, e => e.Value))));
        oyente.Start();
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string,string?>
            { ["Observabilidad:FraccionMuestreo"] = "1" }, configurarServicios: servicios =>
            servicios.AddOpenTelemetry().WithTracing(t => t.AddProcessor(new SimpleActivityExportProcessor(exportador))));
        var escenario = await entorno.CrearEscenarioAsync();
        exportador.Trazas.Clear();
        entorno.Cliente.DefaultRequestHeaders.Add("baggage", "gps=SECRETO_TRAZA");
        using var respuesta = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/validar-ubicacion?token=SECRETO_TRAZA",
            new SolicitudValidarUbicacionAsistencia(escenario.Asignacion.IdAsignacion, 12m, -86m, 5m));
        respuesta.EnsureSuccessStatusCode();
        entorno.Fabrica.Services.GetRequiredService<TracerProvider>().ForceFlush();
        Assert.Contains(exportador.Trazas, t => t.Nombre.Contains("/api/asistencias/validar-ubicacion", StringComparison.Ordinal));
        var contenido = JsonSerializer.Serialize(exportador.Trazas.ToArray());
        Assert.DoesNotContain("SECRETO_TRAZA", contenido);
        Assert.DoesNotContain(Contrasena, contenido);
        Assert.Contains(medidas, m => m.Nombre == "cet.api.respuestas"
            && m.Etiquetas.GetValueOrDefault("http.route") as string == "/api/asistencias/validar-ubicacion");
        Assert.All(medidas, m => Assert.All(m.Etiquetas.Keys, k => Assert.DoesNotContain(k,
            new[] { "id_usuario", "uuid", "latitud", "longitud", "cookie", "url.query" })));
    }

    [Theory]
    [InlineData("http://colector.remoto")]
    [InlineData("https://usuario:clave@colector.remoto")]
    [InlineData("https://colector.remoto?token=privado")]
    public void DestinoOtlpInseguroSeRechaza(string destino) =>
        Assert.Throws<InvalidOperationException>(() => new OpcionesObservabilidad { DestinoOtlp = destino }.Validar());

    [Fact]
    public async Task ColectorOtlpRecibeExportacionHttpRealSinConsultaPrivada()
    {
        var recibidas = new ConcurrentQueue<byte[]>();
        var constructor = WebApplication.CreateBuilder();
        constructor.WebHost.UseUrls("http://127.0.0.1:0");
        constructor.Logging.ClearProviders();
        await using var colector = constructor.Build();
        colector.MapPost("/v1/traces", async (HttpContext contexto) =>
        {
            using var flujo = new MemoryStream();
            await contexto.Request.Body.CopyToAsync(flujo);
            recibidas.Enqueue(flujo.ToArray());
            contexto.Response.ContentType = "application/x-protobuf";
        });
        colector.MapPost("/v1/metrics", () => Results.Ok());
        await colector.StartAsync();
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string,string?>
        {
            ["Observabilidad:FraccionMuestreo"] = "1",
            ["Observabilidad:DestinoOtlp"] = colector.Urls.Single()
        });
        using var respuesta = await entorno.Cliente.GetAsync("/api/obras?token=SECRETO_OTLP");
        respuesta.EnsureSuccessStatusCode();
        Assert.True(entorno.Fabrica.Services.GetRequiredService<TracerProvider>().ForceFlush(10_000));
        Assert.NotEmpty(recibidas);
        var texto = string.Join("", recibidas.Select(System.Text.Encoding.UTF8.GetString));
        Assert.Contains("constructora-eder-torres-api", texto);
        Assert.DoesNotContain("SECRETO_OTLP", texto);
        Assert.DoesNotContain(Contrasena, texto);
    }
}
