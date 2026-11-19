using System.Net;
using System.Net.Http.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class RecorridosDiagramasPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task ActaConservaIdentidadDecisionesYContenidoDuranteCorreccionYRevision()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var otra = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var avance = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 10m));
        var borrador = await CrearAsync<RespuestaActa>(entorno.Cliente, "/api/seguimiento/actas",
            new SolicitudElaborarActa(escenario.Obra.IdObra, "ACTA-CICLO", entorno.HoyLaboral, entorno.HoyLaboral));
        var ruta = $"/api/seguimiento/actas/{borrador.IdActa}";
        using (var omitirRevision = await entorno.Cliente.PatchAsJsonAsync(ruta + "/resolucion", new SolicitudResolverActa("APROBADA")))
            Assert.Equal(HttpStatusCode.Conflict, omitirRevision.StatusCode);
        using var residenteAjeno = await entorno.CrearUsuarioAsync(NombresRol.IngenieroResidente, otra.Trabajador.IdTrabajador);
        using (var ajena = await residenteAjeno.PostAsJsonAsync(ruta + "/enviar-revision", new SolicitudEnviarActa()))
            Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
        var enviada = await CrearAsync<RespuestaActa>(entorno.Cliente, ruta + "/enviar-revision", new SolicitudEnviarActa());
        Assert.Equal(EstadosActa.Pendiente, enviada.Estado);
        using (var editarPendiente = await entorno.Cliente.PutAsJsonAsync(ruta, new SolicitudActualizarActa(entorno.HoyLaboral, entorno.HoyLaboral)))
            Assert.Equal(HttpStatusCode.Conflict, editarPendiente.StatusCode);
        using (var rechazar = await entorno.Cliente.PatchAsJsonAsync(ruta + "/resolucion", new SolicitudResolverActa("RECHAZADA", "Completar observaciones")))
            Assert.Equal(HttpStatusCode.OK, rechazar.StatusCode);
        using (var actualizar = await entorno.Cliente.PutAsJsonAsync(ruta,
            new SolicitudActualizarActa(entorno.HoyLaboral, entorno.HoyLaboral, "Corregida", IdsAvances: [avance.IdAvance])))
        {
            Assert.Equal(HttpStatusCode.OK, actualizar.StatusCode);
            var corregida = (await actualizar.Content.ReadFromJsonAsync<RespuestaActa>())!;
            Assert.Equal(borrador.UuidRegistro, corregida.UuidRegistro);
            Assert.Equal(EstadosActa.Borrador, corregida.Estado);
            Assert.Single(corregida.Aprobaciones);
            Assert.Single(corregida.AvancesConsolidados);
        }
        await CrearAsync<RespuestaActa>(entorno.Cliente, ruta + "/enviar-revision", new SolicitudEnviarActa());
        using (var aprobar = await entorno.Cliente.PatchAsJsonAsync(ruta + "/resolucion", new SolicitudResolverActa("APROBADA")))
            Assert.Equal(HttpStatusCode.OK, aprobar.StatusCode);
        using (var editarAprobada = await entorno.Cliente.PutAsJsonAsync(ruta, new SolicitudActualizarActa(entorno.HoyLaboral, entorno.HoyLaboral)))
            Assert.Equal(HttpStatusCode.Conflict, editarAprobada.StatusCode);
        var final = (await entorno.Cliente.GetFromJsonAsync<RespuestaActa>(ruta))!;
        Assert.Equal(2, final.Aprobaciones.Count);
        Assert.Equal(EstadosActa.Aprobada, final.Estado);
        Assert.True(await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "actas")) >= 6);
    }

    [Fact]
    public async Task HerramientasFiltranHistorialYExigenResolverIncidenciaAntesDeLiberarRevision()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var herramienta = await entorno.CrearHerramientaAsync();
        var otra = await entorno.CrearHerramientaAsync();
        var asignacion = await CrearAsync<RespuestaAsignacionHerramienta>(entorno.Cliente, "/api/herramientas/asignaciones",
            new SolicitudAsignarHerramienta(herramienta.IdHerramienta, escenario.Asignacion.IdAsignacion, entorno.AhoraUtc.AddHours(-1)));
        var evento = await CrearAsync<RespuestaIncidenciaHerramienta>(entorno.Cliente, "/api/herramientas/eventos",
            new SolicitudRegistrarEventoHerramienta(asignacion.IdAsignacionHerramienta, "DANO",
                entorno.AhoraUtc, "Carcasa dañada", CondicionDevolucion: "DANADA"));
        var revision = new SolicitudRevisarHerramienta("BUENA", false, "Reparada y comprobada");
        using (var prematura = await entorno.Cliente.PostAsJsonAsync($"/api/herramientas/{herramienta.IdHerramienta}/revision", revision))
            Assert.Equal(HttpStatusCode.Conflict, prematura.StatusCode);
        foreach (var estado in new[] { "EN_PROCESO", "RESUELTA" })
        {
            using var atender = await entorno.Cliente.PatchAsJsonAsync($"/api/herramientas/eventos/{evento.IdIncidenciaHerramienta}/atencion",
                new SolicitudAtenderIncidenciaHerramienta(estado, "Cambio de carcasa y verificación", 20m));
            Assert.Equal(HttpStatusCode.OK, atender.StatusCode);
        }
        var revisada = await CrearAsync<RespuestaHerramienta>(entorno.Cliente, $"/api/herramientas/{herramienta.IdHerramienta}/revision", revision);
        Assert.Equal(EstadosHerramienta.Disponible, revisada.Estado);
        var devueltas = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaAsignacionHerramienta>>("/api/herramientas/asignaciones?estado=DEVUELTA"))!;
        Assert.Single(devueltas);
        Assert.All(devueltas, a => Assert.Equal(EstadosAsignacionHerramienta.Devuelta, a.Estado));
        Assert.Empty((await entorno.Cliente.GetFromJsonAsync<List<RespuestaAsignacionHerramienta>>("/api/herramientas/asignaciones?estado=ACTIVA"))!);
        Assert.Single((await entorno.Cliente.GetFromJsonAsync<List<RespuestaIncidenciaHerramienta>>($"/api/herramientas/eventos?idHerramienta={herramienta.IdHerramienta}"))!);
        Assert.Empty((await entorno.Cliente.GetFromJsonAsync<List<RespuestaIncidenciaHerramienta>>($"/api/herramientas/eventos?idHerramienta={otra.IdHerramienta}"))!);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        using (var sinPermiso = await trabajador.PostAsJsonAsync($"/api/herramientas/{herramienta.IdHerramienta}/revision", revision))
            Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);
        using var editar = await entorno.Cliente.PutAsJsonAsync($"/api/herramientas/{herramienta.IdHerramienta}",
            new SolicitudActualizarHerramienta("SERIE-REVISADA", "Marca", "Modelo", entorno.HoyLaboral));
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        Assert.Equal(EstadosHerramienta.Disponible, (await editar.Content.ReadFromJsonAsync<RespuestaHerramienta>())!.Estado);
    }

    [Fact]
    public async Task SalidaPorListaEsAtomicaRevalidaGpsYConservaIdempotencia()
    {
        await using var entorno = await CrearAsync(cluster);
        var a = await entorno.CrearEscenarioAsync();
        var b = await entorno.CrearEscenarioAsync();
        async Task<RespuestaAsistencia> Entrada(EscenarioObra e) => await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias",
            new SolicitudRegistrarAsistencia(e.Asignacion.IdAsignacion, entorno.HoyLaboral, entorno.AhoraUtc.AddMinutes(-10),
                LatitudEntrada: 12m, LongitudEntrada: -86m, PrecisionGpsEntradaMetros: 5m));
        var entrada = await Entrada(a);
        var ajena = await Entrada(b);
        var salida = new SolicitudRegistrarSalidasCuadrilla(a.Obra.IdObra, entorno.AhoraUtc, 12m, -86m, 5m, [entrada.IdAsistencia, ajena.IdAsistencia]);
        using (var mixta = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/cuadrilla/salidas", salida))
            Assert.Equal(HttpStatusCode.Conflict, mixta.StatusCode);
        Assert.Equal(EstadosAsistencia.Abierta, (await entorno.Cliente.GetFromJsonAsync<RespuestaAsistencia>($"/api/asistencias/{entrada.IdAsistencia}"))!.Estado);
        salida = salida with { IdsAsistencias = [entrada.IdAsistencia] };
        using (var fueraRadio = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/cuadrilla/salidas", salida with { LatitudLectura = 0m }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, fueraRadio.StatusCode);
            Assert.Contains("fuera", await fueraRadio.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
        var confirmada = await CrearAsync<List<RespuestaAsistencia>>(entorno.Cliente, "/api/asistencias/cuadrilla/salidas", salida);
        var reintento = await CrearAsync<List<RespuestaAsistencia>>(entorno.Cliente, "/api/asistencias/cuadrilla/salidas", salida);
        Assert.Equal(EstadosAsistencia.Completa, confirmada.Single().Estado);
        Assert.True(reintento.Single().FueIdempotente);
        Assert.Equal(entrada.UuidRegistro, reintento.Single().UuidRegistro);
        var abiertas = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaAsistencia>>($"/api/asistencias?idObra={a.Obra.IdObra}&estado=ABIERTA"))!;
        Assert.Empty(abiertas);
    }

    [Fact]
    public async Task EppDeterioradoNoSeLiberaSinDictamenValidoYGeneraMovimiento()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var epp = await entorno.CrearEppAsync();
        var entrega = await CrearAsync<RespuestaEntregaEpp>(entorno.Cliente, "/api/epp/entregas",
            new SolicitudRegistrarEntregaEpp(epp.IdEpp, escenario.Asignacion.IdAsignacion, entorno.AhoraUtc.AddHours(-1)));
        using (var devolver = await entorno.Cliente.PatchAsJsonAsync($"/api/epp/entregas/{entrega.IdEntregaEpp}/devolucion",
            new SolicitudDevolverEpp(entorno.AhoraUtc, "DANADO")))
            Assert.Equal(HttpStatusCode.OK, devolver.StatusCode);
        using (var invalida = await entorno.Cliente.PostAsJsonAsync($"/api/epp/existencias/{epp.IdEpp}/revision",
            new SolicitudRevisarEpp("DANADO", false, "Inspección")))
            Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        var retirado = await CrearAsync<RespuestaEpp>(entorno.Cliente, $"/api/epp/existencias/{epp.IdEpp}/revision",
            new SolicitudRevisarEpp("DANADO", true, "Equipo no recuperable"));
        Assert.Equal(EstadosEpp.Retirado, retirado.Estado);
        Assert.True(await entorno.EnBaseAsync(c => c.MovimientosExistenciasEpp.AnyAsync(m => m.IdEpp == epp.IdEpp && m.TipoMovimiento == TiposMovimientoEpp.Retiro)));
    }
}
