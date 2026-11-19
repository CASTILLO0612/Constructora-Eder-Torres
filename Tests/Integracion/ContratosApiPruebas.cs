using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class ContratosApiPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task OpenApiDescribeRespuestasSesionCsrfYPaginacionSinExponerCredenciales()
    {
        await using var fabrica = new FabricaApi().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var cliente = fabrica.CreateClient();
        var texto = await cliente.GetStringAsync("/openapi/v1.json");
        using var documento = JsonDocument.Parse(texto);
        var raiz = documento.RootElement;
        var identificadores = new HashSet<string>();
        var total = 0;
        foreach (var ruta in raiz.GetProperty("paths").EnumerateObject().Where(p => p.Name.StartsWith("/api/", StringComparison.Ordinal)))
            foreach (var operacion in ruta.Value.EnumerateObject())
            {
                total++;
                Assert.True(identificadores.Add(operacion.Value.GetProperty("operationId").GetString()!), ruta.Name);
                var respuestas = operacion.Value.GetProperty("responses");
                var exitosas = respuestas.EnumerateObject().Where(r => r.Name.StartsWith('2')).ToArray();
                Assert.NotEmpty(exitosas);
                foreach (var respuesta in exitosas.Where(r => r.Name != "204"))
                    Assert.True(respuesta.Value.TryGetProperty("content", out var contenido) && contenido.EnumerateObject().Any(),
                        $"Falta el contrato de respuesta: {operacion.Name} {ruta.Name} {respuesta.Name}");
                Assert.True(respuestas.TryGetProperty("400", out _));
                if (operacion.Name is "post" or "put" or "patch" or "delete")
                    Assert.Contains(operacion.Value.GetProperty("parameters").EnumerateArray(), p =>
                        p.GetProperty("name").GetString() == "X-CET-CSRF" && p.GetProperty("required").GetBoolean());
            }
        Assert.True(total > 100);
        var esquemas = raiz.GetProperty("components").GetProperty("securitySchemes");
        Assert.Equal("cookie", esquemas.GetProperty("SesionCookie").GetProperty("in").GetString());
        var obras = BuscarRuta(raiz, "/api/obras").GetProperty("get");
        Assert.Contains(obras.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "tamanoPagina");
        Assert.NotEmpty(obras.GetProperty("security").EnumerateArray());
        Assert.Empty(BuscarRuta(raiz, "/api/sesion/antifalsificacion").GetProperty("get").GetProperty("security").EnumerateArray());
        Assert.DoesNotContain("password_hash", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("selloCredenciales", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FueConfirmacionBorrador", texto, StringComparison.OrdinalIgnoreCase);
        var asistencia = raiz.GetProperty("components").GetProperty("schemas").GetProperty("SolicitudRegistrarAsistencia");
        Assert.Contains(asistencia.GetProperty("required").EnumerateArray(), p => p.GetString() == "latitudEntrada");
        var crearObra = raiz.GetProperty("components").GetProperty("schemas").GetProperty("SolicitudCrearObra");
        foreach (var campo in new[] { "latitud", "longitud", "radioAsistenciaMetros" })
            Assert.Contains(crearObra.GetProperty("required").EnumerateArray(), p => p.GetString() == campo);
        Assert.True(raiz.GetProperty("paths").TryGetProperty("/api/asistencias/mis-asignaciones", out _));

        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "ConstructoraEderTorres.sln"))) carpeta = carpeta.Parent;
        var destino = Path.Combine(carpeta!.FullName, "artifacts", "ContratosApi");
        Directory.CreateDirectory(destino);
        await File.WriteAllTextAsync(Path.Combine(destino, "openapi.v1.json"), texto);
    }

    [Fact]
    public async Task CorsPermiteSoloElOrigenConfiguradoYConservaCabecerasEnErrores()
    {
        const string origen = "https://cliente-pruebas.example";
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string, string?>
        { ["Seguridad:Sesion:OrigenesCorsPermitidos:0"] = origen });
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/asistencias");
        preflight.Headers.Add("Origin", origen);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,x-cet-csrf");
        using var respuesta = await entorno.Cliente.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(origen, Assert.Single(respuesta.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(respuesta.Headers.GetValues("Access-Control-Allow-Credentials")));

        using var consulta = new HttpRequestMessage(HttpMethod.Get, "/api/obras?pagina=0");
        consulta.Headers.Add("Origin", origen);
        using var invalida = await entorno.Cliente.SendAsync(consulta);
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        Assert.Equal(origen, Assert.Single(invalida.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Location", string.Join(',', invalida.Headers.GetValues("Access-Control-Expose-Headers")));

        using var ajena = new HttpRequestMessage(HttpMethod.Options, "/api/asistencias");
        ajena.Headers.Add("Origin", "https://origen-ajeno.example");
        ajena.Headers.Add("Access-Control-Request-Method", "POST");
        using var denegada = await entorno.Cliente.SendAsync(ajena);
        Assert.False(denegada.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task GpsEsObligatorioEnEntradaSalidaYCuadrillaYLosReintentosConservanDatos()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var ahora = DateTime.UtcNow.AddHours(-8);
        var solicitud = new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion,
            CalendarioPruebas.Fecha(ahora), ahora, LatitudEntrada: 12.1363894m, LongitudEntrada: -86.2513894m, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m);
        var auditorias = await entorno.EnBaseAsync(c => c.Auditorias.CountAsync());
        foreach (var invalida in new[]
        {
            solicitud with { LatitudEntrada = null, LongitudEntrada = null },
            solicitud with { LongitudEntrada = null },
            solicitud with { LatitudEntrada = 90.0000001m },
            solicitud with { SalidaEn = ahora.AddHours(8) },
            solicitud with { LatitudSalida = 12m, LongitudSalida = -86m }
        })
        {
            using var respuesta = await entorno.Cliente.PostAsJsonAsync("/api/asistencias", invalida);
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
        using var cuadrilla = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/cuadrilla",
            new SolicitudRegistrarAsistenciaCuadrilla(escenario.Obra.IdObra, CalendarioPruebas.Fecha(ahora), null, null,
                [new(escenario.Asignacion.IdAsignacion, ahora)], PrecisionGpsMetros: 5m));
        Assert.Equal(HttpStatusCode.BadRequest, cuadrilla.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        Assert.Equal(auditorias, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync()));
        var creada = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias", solicitud);
        Assert.Equal(12.136389m, creada.LatitudEntrada);
        using var sinGps = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/{creada.IdAsistencia}/salida", new SolicitudRegistrarSalidaAsistencia(ahora.AddHours(8), PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.BadRequest, sinGps.StatusCode);
        using var salida = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/{creada.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(ahora.AddHours(8), 12.136389m, -86.251389m, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.OK, salida.StatusCode);
        using var alterada = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/{creada.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(ahora.AddHours(8), 13m, -86.251389m, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.Conflict, alterada.StatusCode);
        var repetida = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias", solicitud);
        Assert.True(repetida.FueIdempotente);
        Assert.Equal(EstadosAsistencia.Completa, repetida.Estado);
    }

    [Fact]
    public async Task SalidaYDevolucionOfflineSeConfirmanUnaSolaVezInclusoConReenviosConcurrentes()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var ahora = DateTime.UtcNow.AddHours(-8);
        var asistencia = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias",
            new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(ahora), ahora,
                LatitudEntrada: 12m, LongitudEntrada: -86m, PrecisionGpsEntradaMetros: 5m));
        var equipo = await entorno.CrearEppAsync();
        var entrega = await CrearAsync<RespuestaEntregaEpp>(entorno.Cliente, "/api/epp/entregas",
            new SolicitudRegistrarEntregaEpp(equipo.IdEpp, escenario.Asignacion.IdAsignacion, ahora));
        var uuidDevolucion = Guid.NewGuid();
        var lote = new SolicitudSincronizacionLote(
            SalidasAsistencia: [new(asistencia.IdAsistencia, ahora.AddHours(8), 12m, -86m, asistencia.UuidRegistro, PrecisionGpsSalidaMetros: 5m)],
            DevolucionesEpp: [new(entrega.IdEntregaEpp, ahora.AddHours(8), CondicionesEpp.Bueno, "Devuelto sin daños", uuidDevolucion)]);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote", lote)));
        Assert.Equal(2, respuestas.Sum(r => r.TotalConfirmadas));
        Assert.Equal(8, respuestas.Sum(r => r.TotalIdempotentes));
        Assert.All(respuestas, r => Assert.Equal(0, r.TotalRechazadas));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.MovimientosExistenciasEpp.CountAsync(m => m.UuidRegistro == uuidDevolucion)));
        Assert.Equal(EstadosEpp.Disponible, await entorno.EnBaseAsync(c => c.EquiposProteccionPersonal.Where(e => e.IdEpp == equipo.IdEpp).Select(e => e.Estado).SingleAsync()));
        var modificado = lote with { DevolucionesEpp = [new(entrega.IdEntregaEpp, ahora.AddHours(8), CondicionesEpp.Danado, "Otro contenido", uuidDevolucion)], SalidasAsistencia = null };
        var conflicto = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote", modificado);
        Assert.Equal(EstadosAcuseSincronizacion.RechazadoConflicto, Assert.Single(conflicto.Acuses).EstadoAcuse);
    }

    [Fact]
    public async Task BandejasDeCorreccionesReposicionesYEvidenciasRespetanObraYPaginacion()
    {
        await using var entorno = await CrearAsync(cluster);
        var propia = await entorno.CrearEscenarioAsync();
        var ajena = await entorno.CrearEscenarioAsync();
        var ahora = DateTime.UtcNow.AddHours(-8);
        long idAvanceAjeno = 0;
        foreach (var escenario in new[] { propia, ajena })
        {
            var asistencia = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias",
                new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(ahora), ahora,
                    LatitudEntrada: 12m, LongitudEntrada: -86m, PrecisionGpsEntradaMetros: 5m));
            await CrearAsync<RespuestaCorreccionAsistencia>(entorno.Cliente, "/api/asistencias/correcciones",
                new SolicitudCrearCorreccionAsistencia(asistencia.IdAsistencia, ahora.AddMinutes(-5), null, "Ajuste de hora"));
            var epp = await entorno.CrearEppAsync();
            var entrega = await CrearAsync<RespuestaEntregaEpp>(entorno.Cliente, "/api/epp/entregas",
                new SolicitudRegistrarEntregaEpp(epp.IdEpp, escenario.Asignacion.IdAsignacion, ahora));
            await CrearAsync<RespuestaReposicionEpp>(entorno.Cliente, "/api/epp/reposiciones",
                new SolicitudRegistrarReposicionEpp(entrega.IdEntregaEpp, null, MotivosReposicionEpp.Dano, "Por revisar", true));
            var actividad = await entorno.CrearActividadAsync(escenario);
            var avance = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
                new SolicitudRegistrarAvance(actividad.IdActividad, CalendarioPruebas.Fecha(ahora), 1m));
            await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias",
                new SolicitudRegistrarEvidencia(avance.IdAvance, TiposEvidencia.Fotografia, "prueba.png", FotoPng,
                    CapturadaEn: ahora, Latitud: 12m, Longitud: -86m));
            if (escenario == ajena) idAvanceAjeno = avance.IdAvance;
        }
        using var residente = await entorno.CrearUsuarioAsync(NombresRol.IngenieroResidente, propia.Trabajador.IdTrabajador);
        var correcciones = (await residente.GetFromJsonAsync<List<RespuestaCorreccionAsistencia>>("/api/asistencias/correcciones?estado=PENDIENTE"))!;
        Assert.Equal(propia.Obra.IdObra, Assert.Single(correcciones).IdObra);
        var reposiciones = (await residente.GetFromJsonAsync<List<RespuestaReposicionEpp>>("/api/epp/reposiciones?estado=PENDIENTE"))!;
        Assert.Equal(propia.Obra.IdObra, Assert.Single(reposiciones).IdObra);
        var entregas = (await residente.GetFromJsonAsync<List<RespuestaEntregaEpp>>("/api/epp/entregas"))!;
        Assert.Equal(propia.Obra.IdObra, Assert.Single(entregas).IdObra);
        foreach (var ruta in new[] { $"/api/asistencias/correcciones?idObra={ajena.Obra.IdObra}",
            $"/api/epp/reposiciones?idObra={ajena.Obra.IdObra}", $"/api/seguimiento/avances/{idAvanceAjeno}/evidencias" })
        {
            using var respuesta = await residente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
        var pagina = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaCorreccionAsistencia>>("/api/asistencias/correcciones?tamanoPagina=1&pagina=2"))!;
        Assert.Single(pagina);
        using var estadoInvalido = await entorno.Cliente.GetAsync("/api/epp/reposiciones?estado=INVENTADO");
        Assert.Equal(HttpStatusCode.BadRequest, estadoInvalido.StatusCode);
    }

    [Fact]
    public async Task CierreDeAsignacionFinalizaCuadrillaYLocationDelMiembroEsConsultable()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var cuadrilla = await CrearAsync<RespuestaCuadrilla>(entorno.Cliente, "/api/personal/cuadrillas", new SolicitudCuadrilla("CUAD-PR", "Cuadrilla", null));
        var hoy = CalendarioPruebas.Hoy;
        var grupo = await CrearAsync<RespuestaCuadrillaObra>(entorno.Cliente, "/api/personal/cuadrillas-obra", new SolicitudAsignarCuadrillaObra(cuadrilla.IdCuadrilla, escenario.Obra.IdObra, hoy.AddDays(-2)));
        var miembro = await CrearAsync<RespuestaTrabajadorCuadrilla>(entorno.Cliente, "/api/personal/trabajadores-cuadrilla",
            new SolicitudAsignarTrabajadorCuadrilla(escenario.Asignacion.IdAsignacion, grupo.IdCuadrillaObra, hoy.AddDays(-1), hoy.AddDays(10)));
        var lista = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaCuadrillaObra>>($"/api/personal/obras/{escenario.Obra.IdObra}/cuadrillas"))!;
        Assert.Equal(grupo.IdCuadrillaObra, Assert.Single(lista).IdCuadrillaObra);
        using var cierre = await entorno.Cliente.PatchAsJsonAsync($"/api/personal/asignaciones-obra/{escenario.Asignacion.IdAsignacion}/finalizacion", new SolicitudFinalizarAsignacionObra(hoy));
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        var actual = (await entorno.Cliente.GetFromJsonAsync<RespuestaTrabajadorCuadrilla>($"/api/personal/trabajadores-cuadrilla/{miembro.IdTrabajadorCuadrilla}"))!;
        Assert.Equal(hoy, actual.FechaFin);
        using var marcacion = await entorno.Cliente.PostAsJsonAsync("/api/asistencias", new SolicitudRegistrarAsistencia(
            escenario.Asignacion.IdAsignacion, hoy, DateTime.UtcNow, LatitudEntrada: 12m, LongitudEntrada: -86m, PrecisionGpsEntradaMetros: 5m));
        Assert.Equal(HttpStatusCode.Conflict, marcacion.StatusCode);
    }

    [Fact]
    public async Task ErroresDeRecursoConservanElDetalleYLosEventosDeHerramientaSeDescubren()
    {
        await using var entorno = await CrearAsync(cluster);
        using var inexistente = await entorno.Cliente.GetAsync("/api/obras/2147483647");
        using var error = JsonDocument.Parse(await inexistente.Content.ReadAsStringAsync());
        Assert.Equal("La obra solicitada no existe.", error.RootElement.GetProperty("detail").GetString());
        Assert.True(error.RootElement.TryGetProperty("identificador", out _));
        var escenario = await entorno.CrearEscenarioAsync();
        var herramienta = await entorno.CrearHerramientaAsync();
        var ahora = DateTime.UtcNow;
        var asignacion = await CrearAsync<RespuestaAsignacionHerramienta>(entorno.Cliente, "/api/herramientas/asignaciones",
            new SolicitudAsignarHerramienta(herramienta.IdHerramienta, escenario.Asignacion.IdAsignacion, ahora));
        var evento = await CrearAsync<RespuestaIncidenciaHerramienta>(entorno.Cliente, "/api/herramientas/eventos",
            new SolicitudRegistrarEventoHerramienta(asignacion.IdAsignacionHerramienta, TiposIncidenciaHerramienta.Devolucion, ahora.AddHours(2), "Regreso", CondicionesHerramienta.Buena));
        var historial = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaIncidenciaHerramienta>>($"/api/herramientas/eventos?idAsignacionHerramienta={asignacion.IdAsignacionHerramienta}"))!;
        Assert.Equal(evento.IdIncidenciaHerramienta, Assert.Single(historial).IdIncidenciaHerramienta);
    }

    [Fact]
    public async Task FotografiaConservaGpsYFechaOriginalYRechazaUnUuidConMetadatosDistintos()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var captura = DateTime.UtcNow.AddDays(-1);
        var avance = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(actividad.IdActividad, DateOnly.FromDateTime(captura), 1m));
        var solicitud = new SolicitudRegistrarEvidencia(avance.IdAvance, TiposEvidencia.Fotografia, "captura.png", FotoPng,
            CapturadaEn: captura, Latitud: 12.1234567m, Longitud: -86.1234567m, UuidRegistro: Guid.NewGuid(), EsSincronizacionDiferida: true);
        foreach (var incompleta in new[] { solicitud with { CapturadaEn = null }, solicitud with { Latitud = null, Longitud = null } })
        {
            using var respuesta = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias", incompleta);
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
        var primera = await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias", solicitud);
        Assert.Equal(captura.Date, primera.CapturadaEn.Date);
        Assert.Equal(12.123457m, primera.Latitud);
        var repetida = await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias", solicitud);
        Assert.True(repetida.FueIdempotente);
        using var diferente = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias", solicitud with { Latitud = 13m });
        Assert.Equal(HttpStatusCode.Conflict, diferente.StatusCode);
        Assert.Single(Directory.GetFiles(entorno.CarpetaEvidencias, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AuditoriaFiltraPorRegistroYPeriodoYRechazaInstantesSinZona()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var consulta = $"/api/auditorias?entidad=obras&idRegistro={escenario.Obra.IdObra}&desdeUtc=2000-01-01T00:00:00Z";
        var registros = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaAuditoria>>(consulta))!;
        Assert.Equal(escenario.Obra.IdObra.ToString(), Assert.Single(registros).IdRegistro);
        using var sinZona = await entorno.Cliente.GetAsync("/api/auditorias?desdeUtc=2026-01-01T00:00:00");
        Assert.Equal(HttpStatusCode.BadRequest, sinZona.StatusCode);
        using var periodoInverso = await entorno.Cliente.GetAsync("/api/auditorias?desdeUtc=2026-02-01T00:00:00Z&hastaUtc=2026-01-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, periodoInverso.StatusCode);
    }

    private static JsonElement BuscarRuta(JsonElement documento, string ruta) =>
        documento.GetProperty("paths").EnumerateObject().Single(p => p.Name.TrimEnd('/') == ruta).Value;
}
