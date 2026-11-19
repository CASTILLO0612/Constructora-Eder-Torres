using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Reglas;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class CicloVidaApiPruebas(ClusterPostgreSqlDesechableFixture cluster) : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private static Task<RespuestaActividad> ActividadAsync(EntornoApiPruebas entorno, EscenarioObra escenario) =>
        CrearAsync<RespuestaActividad>(entorno.Cliente, "/api/seguimiento/actividades", new SolicitudCrearActividad(
            escenario.Obra.IdObra, escenario.Asignacion.IdAsignacion, "ACT-" + Guid.NewGuid().ToString("N")[..8],
            "Actividad de validación", null, "m2", 100m, entorno.HoyLaboral.AddDays(-10), entorno.HoyLaboral.AddDays(10)));

    private static SolicitudActualizarActividad Cambio(RespuestaActividad actividad, string estado) =>
        new(actividad.IdAsignacionResponsable, actividad.Nombre, actividad.Descripcion, actividad.UnidadMedida,
            actividad.CantidadPlanificada, actividad.FechaInicioPlan, actividad.FechaFinPlan, estado);

    private static async Task ErrorAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo)
    {
        using (respuesta)
        {
            Assert.Equal(estado, respuesta.StatusCode);
            using var cuerpo = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync());
            Assert.Equal(codigo, cuerpo.RootElement.GetProperty("codigo").GetString());
        }
    }

    [Fact]
    public async Task ObraPermiteAltaActivaYPlanificacionPeroNoReabrirNiSaltarEstados()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var solicitud = new SolicitudCrearObra("P-" + Guid.NewGuid().ToString("N")[..8], "Obra planificada", null, "Dirección",
            entorno.HoyLaboral, entorno.HoyLaboral.AddDays(20), EstadosObra.Planificada, 12m, -86m, 100m);
        var obra = await CrearAsync<RespuestaObra>(entorno.Cliente, "/api/obras", solicitud);
        await CrearAsync<RespuestaAsignacionTrabajadorObra>(entorno.Cliente, "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(escenario.Trabajador.IdTrabajador, obra.IdObra, null, TiposContratacion.Fijo, entorno.HoyLaboral));
        await ErrorAsync(await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{obra.IdObra}/estado", new SolicitudCambiarEstadoObra(EstadosObra.Finalizada)), HttpStatusCode.Conflict, "transicion_obra_invalida");
        foreach (var estado in new[] { EstadosObra.Activa, EstadosObra.Suspendida, EstadosObra.Activa, EstadosObra.Finalizada })
        {
            using var respuesta = await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{obra.IdObra}/estado", new SolicitudCambiarEstadoObra(estado));
            respuesta.EnsureSuccessStatusCode();
        }
        await ErrorAsync(await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{obra.IdObra}/estado", new SolicitudCambiarEstadoObra(EstadosObra.Activa)), HttpStatusCode.Conflict, "transicion_obra_invalida");
        foreach (var estado in new[] { EstadosObra.Suspendida, EstadosObra.Finalizada })
            await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/obras", solicitud with { Codigo = "N-" + Guid.NewGuid().ToString("N")[..8], EstadoInicial = estado }), HttpStatusCode.BadRequest, "estado_inicial_obra_invalido");
    }

    [Theory]
    [InlineData(EstadosObra.Suspendida)]
    [InlineData(EstadosObra.Finalizada)]
    public async Task ObraInactivaBloqueaNuevosRegistrosYConservaReenviosYDevoluciones(string estadoObra)
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await ActividadAsync(entorno, escenario);
        var equipo = await entorno.CrearEppAsync();
        var herramienta = await entorno.CrearHerramientaAsync();
        var entrega = new SolicitudRegistrarEntregaEpp(equipo.IdEpp, escenario.Asignacion.IdAsignacion, entorno.AhoraUtc, UuidRegistro: Guid.NewGuid());
        var registrada = await CrearAsync<RespuestaEntregaEpp>(entorno.Cliente, "/api/epp/entregas", entrega);
        var sustituto = await entorno.CrearEppAsync(equipo.IdTipoEpp);
        var reposicion = await CrearAsync<RespuestaReposicionEpp>(entorno.Cliente, "/api/epp/reposiciones",
            new SolicitudRegistrarReposicionEpp(registrada.IdEntregaEpp, null, MotivosReposicionEpp.Dano,
                "Casco dañado", true, entorno.AhoraUtc, Guid.NewGuid()));
        using var suspension = await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{escenario.Obra.IdObra}/estado", new SolicitudCambiarEstadoObra(estadoObra));
        suspension.EnsureSuccessStatusCode();
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 20)), HttpStatusCode.Conflict, "obra_no_activa");
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/incidencias", new SolicitudRegistrarIncidenciaObra(escenario.Obra.IdObra, TiposIncidenciaObra.Clima, entorno.AhoraUtc, null, 1m, "Lluvia", null)), HttpStatusCode.Conflict, "obra_no_activa");
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/epp/entregas", entrega with { UuidRegistro = Guid.NewGuid() }), HttpStatusCode.Conflict, "obra_no_activa");
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/herramientas/asignaciones", new SolicitudAsignarHerramienta(herramienta.IdHerramienta, escenario.Asignacion.IdAsignacion, entorno.AhoraUtc)), HttpStatusCode.Conflict, "obra_no_activa");
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync($"/api/epp/reposiciones/{reposicion.IdReposicionEpp}/ejecutar",
            new SolicitudEjecutarReposicionEpp(sustituto.IdEpp, true, entorno.AhoraUtc)), HttpStatusCode.Conflict, "obra_no_activa");
        Assert.Equal(EstadosEpp.Disponible, await entorno.EnBaseAsync(c => c.EquiposProteccionPersonal
            .Where(e => e.IdEpp == sustituto.IdEpp).Select(e => e.Estado).SingleAsync()));
        var repetida = await CrearAsync<RespuestaEntregaEpp>(entorno.Cliente, "/api/epp/entregas", entrega);
        Assert.True(repetida.FueIdempotente);
        using var devolucion = await entorno.Cliente.PatchAsJsonAsync($"/api/epp/entregas/{registrada.IdEntregaEpp}/devolucion",
            new SolicitudDevolverEpp(entorno.AhoraUtc, CondicionesEpp.Bueno, "Cierre de custodia", Guid.NewGuid()));
        devolucion.EnsureSuccessStatusCode();
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Avances.CountAsync()));
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Incidencias.CountAsync()));
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.AsignacionesHerramienta.CountAsync()));
    }

    [Fact]
    public async Task AvancesRegistradosInicianYCompletanLaActividadLosBorradoresNoComputan()
    {
        await using var entorno = await CrearAsync(cluster);
        var actividad = await ActividadAsync(entorno, await entorno.CrearEscenarioAsync());
        await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral.AddDays(-2), 250m, Estado: EstadosAvance.Borrador));
        var borrador = await entorno.Cliente.GetFromJsonAsync<RespuestaActividad>($"/api/seguimiento/actividades/{actividad.IdActividad}");
        Assert.Equal(0, borrador!.CantidadEjecutadaAcumulada);
        Assert.Equal(EstadosActividad.Planificada, borrador.Estado);
        await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances", new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral.AddDays(-1), 35m));
        await ErrorAsync(await entorno.Cliente.PutAsJsonAsync($"/api/seguimiento/actividades/{actividad.IdActividad}", Cambio(actividad, EstadosActividad.Completa)), HttpStatusCode.Conflict, "meta_actividad_no_alcanzada");
        var captura = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 80m, UuidRegistro: Guid.NewGuid());
        await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances", captura);
        var completa = await entorno.Cliente.GetFromJsonAsync<RespuestaActividad>($"/api/seguimiento/actividades/{actividad.IdActividad}");
        Assert.Equal(115m, completa!.CantidadEjecutadaAcumulada);
        Assert.Equal(EstadosActividad.Completa, completa.Estado);
        Assert.True((await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances", captura)).FueIdempotente);
        await ErrorAsync(await entorno.Cliente.PutAsJsonAsync($"/api/seguimiento/actividades/{actividad.IdActividad}", Cambio(actividad, EstadosActividad.EnProceso)), HttpStatusCode.Conflict, "transicion_actividad_invalida");
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "actividades" && a.Campo == "estado" && a.Accion == AccionesAuditoria.Modificar)));
    }

    [Fact]
    public async Task NuevosAvancesNoPuedenSerFuturosOAnuladosNiCrearseEnActividadCancelada()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await ActividadAsync(entorno, escenario);
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral.AddDays(1), 10m)), HttpStatusCode.BadRequest, "fecha_avance_futura");
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 10m, Estado: EstadosAvance.Anulado)), HttpStatusCode.BadRequest, "estado_inicial_avance_invalido");
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/incidencias", new SolicitudRegistrarIncidenciaObra(escenario.Obra.IdObra, TiposIncidenciaObra.Clima, entorno.AhoraUtc, null, 1m, "Prueba", null, EstadosIncidencia.Anulada)), HttpStatusCode.BadRequest, "estado_inicial_incidencia_invalido");
        using var cancelada = await entorno.Cliente.PutAsJsonAsync($"/api/seguimiento/actividades/{actividad.IdActividad}", Cambio(actividad, EstadosActividad.Cancelada));
        cancelada.EnsureSuccessStatusCode();
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 10m)), HttpStatusCode.Conflict, "actividad_cerrada");
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Avances.CountAsync()));
    }

    [Fact]
    public async Task AvancesConcurrentesConservanElAcumuladoYLaFinalizacionSinDuplicarAuditoria()
    {
        await using var entorno = await CrearAsync(cluster);
        var actividad = await ActividadAsync(entorno, await entorno.CrearEscenarioAsync());
        using var clienteA = ClienteApiPruebas.Crear(entorno.Fabrica);
        using var clienteB = ClienteApiPruebas.Crear(entorno.Fabrica);
        await AutenticarAsync(clienteA, NombreAdministrador, Contrasena);
        await AutenticarAsync(clienteB, NombreAdministrador, Contrasena);
        var capturaA = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral.AddDays(-1),
            60m, UuidRegistro: Guid.NewGuid());
        var capturaB = capturaA with { FechaAvance = entorno.HoyLaboral, UuidRegistro = Guid.NewGuid() };

        await Task.WhenAll(
            CrearAsync<RespuestaAvance>(clienteA, "/api/seguimiento/avances", capturaA),
            CrearAsync<RespuestaAvance>(clienteB, "/api/seguimiento/avances", capturaB));

        var resultado = await entorno.Cliente.GetFromJsonAsync<RespuestaActividad>(
            $"/api/seguimiento/actividades/{actividad.IdActividad}");
        Assert.Equal(120m, resultado!.CantidadEjecutadaAcumulada);
        Assert.Equal(EstadosActividad.Completa, resultado.Estado);
        Assert.True((await CrearAsync<RespuestaAvance>(clienteA, "/api/seguimiento/avances", capturaA)).FueIdempotente);
        Assert.True((await CrearAsync<RespuestaAvance>(clienteB, "/api/seguimiento/avances", capturaB)).FueIdempotente);
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Avances.CountAsync()));
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(
            a => a.Entidad == "actividades" && a.Campo == "estado" && a.Accion == AccionesAuditoria.Modificar)));
    }

    [Fact]
    public async Task FalloAlAuditarRevierteAvanceEstadoYPermiteReintentarElMismoUuid()
    {
        await using var entorno = await CrearAsync(cluster);
        var actividad = await ActividadAsync(entorno, await entorno.CrearEscenarioAsync());
        await entorno.EjecutarSqlAsync("""
            CREATE FUNCTION rechazar_auditoria_avance() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.entidad = 'avances' THEN
                    RAISE EXCEPTION 'Fallo controlado de auditoría';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fallo_auditoria_avance BEFORE INSERT ON auditorias
                FOR EACH ROW EXECUTE FUNCTION rechazar_auditoria_avance();
            """);
        var captura = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 100m,
            UuidRegistro: Guid.NewGuid());
        using (var fallo = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", captura))
            Assert.Equal(HttpStatusCode.Conflict, fallo.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Avances.CountAsync()));
        Assert.Equal(EstadosActividad.Planificada, await entorno.EnBaseAsync(c => c.Actividades
            .Where(a => a.IdActividad == actividad.IdActividad).Select(a => a.Estado).SingleAsync()));
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(
            a => a.Entidad == "actividades" && a.Campo == "estado" && a.Accion == AccionesAuditoria.Modificar)));

        await entorno.EjecutarSqlAsync("DROP TRIGGER fallo_auditoria_avance ON auditorias; DROP FUNCTION rechazar_auditoria_avance();");
        var confirmada = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances", captura);
        Assert.False(confirmada.FueIdempotente);
        Assert.Equal(EstadosActividad.Completa, await entorno.EnBaseAsync(c => c.Actividades
            .Where(a => a.IdActividad == actividad.IdActividad).Select(a => a.Estado).SingleAsync()));
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(
            a => a.Entidad == "actividades" && a.Campo == "estado" && a.Accion == AccionesAuditoria.Modificar)));
    }

    [Fact]
    public async Task SincronizacionRevalidaObraPeroReconoceElUuidYaConfirmado()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await ActividadAsync(entorno, escenario);
        var captura = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 10m, UuidRegistro: Guid.NewGuid());
        await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances", captura);
        using var suspension = await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{escenario.Obra.IdObra}/estado", new SolicitudCambiarEstadoObra(EstadosObra.Suspendida));
        suspension.EnsureSuccessStatusCode();
        var resultado = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote", new SolicitudSincronizacionLote(
            Avances: [captura, captura with { FechaAvance = entorno.HoyLaboral.AddDays(-1), UuidRegistro = Guid.NewGuid() }]));
        Assert.Equal(1, resultado.TotalIdempotentes);
        Assert.Equal(1, resultado.TotalRechazadas);
        Assert.Contains(resultado.Acuses, a => a.CodigoError == "obra_no_activa");
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Avances.CountAsync()));
    }
}
