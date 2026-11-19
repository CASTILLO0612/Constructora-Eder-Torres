using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class CapturaAsistenciaApiPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private static readonly DateTime Instante = new(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

    private static SolicitudRegistrarAsistencia Captura(EscenarioObra escenario, DateTime entrada, decimal? precision = 5m) =>
        new(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(entrada), entrada,
            LatitudEntrada: 12m, LongitudEntrada: -86m, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: precision);

    [Fact]
    public async Task FechaDeManaguaYLimiteDeRelojSeCompruebanAntesDeEscribir()
    {
        await using var entorno = await CrearAsync(cluster, new TiempoControlado(Instante));
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var solicitud = Captura(escenario, Instante);
        using var fechaUtcIncorrecta = await trabajador.PostAsJsonAsync("/api/asistencias", solicitud with { FechaJornada = DateOnly.FromDateTime(Instante) });
        await ErrorAsync(fechaUtcIncorrecta, "fecha_jornada_inconsistente");
        using var adelantada = await trabajador.PostAsJsonAsync("/api/asistencias", Captura(escenario, Instante.AddSeconds(121)));
        await ErrorAsync(adelantada, "captura_futura");
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        var aceptada = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", Captura(escenario, Instante.AddSeconds(120)));
        Assert.Equal(new DateOnly(2026, 10, 29), aceptada.FechaJornada);
        Assert.Equal(Instante.AddSeconds(120), aceptada.EntradaEn);
        Assert.Equal(Instante, aceptada.CreadoEn);
    }

    [Fact]
    public async Task PrecisionEsObligatoriaYElCirculoDeErrorDebeCaberEnLaObra()
    {
        await using var entorno = await CrearAsync(cluster, new TiempoControlado(Instante));
        var escenario = await entorno.CrearEscenarioAsync(0m, 0m, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var solicitud = Captura(escenario, Instante) with { LatitudEntrada = 0m, LongitudEntrada = .0008m, PrecisionGpsEntradaMetros = null };
        using var sinPrecision = await trabajador.PostAsJsonAsync("/api/asistencias", solicitud);
        await ErrorAsync(sinPrecision, "precision_gps_obligatoria");
        using var imprecisa = await trabajador.PostAsJsonAsync("/api/asistencias", solicitud with { PrecisionGpsEntradaMetros = 12m });
        await ErrorAsync(imprecisa, "gps_impreciso");
        var consulta = await CrearAsync<RespuestaValidacionUbicacionAsistencia>(trabajador, "/api/asistencias/validar-ubicacion",
            new SolicitudValidarUbicacionAsistencia(escenario.Asignacion.IdAsignacion, 0m, .0008m, 12m));
        Assert.False(consulta.MarcacionHabilitada);
        Assert.True(consulta.DistanciaMetros < 100 && consulta.DistanciaMaximaEstimadaMetros > 100);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        var valida = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", solicitud with { PrecisionGpsEntradaMetros = 10.000001m });
        Assert.Equal(10.01m, valida.PrecisionGpsEntradaMetros);
    }

    [Fact]
    public async Task SalidaSinPrecisionNoCierraLaJornadaYSuReintentoNoCambiaLaLecturaOriginal()
    {
        await using var entorno = await CrearAsync(cluster, new TiempoControlado(Instante));
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var entrada = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", Captura(escenario, Instante.AddHours(-8)));
        using var sinPrecision = await trabajador.PatchAsJsonAsync($"/api/asistencias/{entrada.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(Instante, 12m, -86m, entrada.UuidRegistro));
        await ErrorAsync(sinPrecision, "precision_gps_obligatoria");
        Assert.Null(await entorno.EnBaseAsync(c => c.Asistencias.Select(a => a.SalidaEn).SingleAsync()));
        var solicitud = new SolicitudRegistrarSalidaAsistencia(Instante, 12m, -86m, entrada.UuidRegistro, PrecisionGpsSalidaMetros: 6.00001m);
        using var salida = await trabajador.PatchAsJsonAsync($"/api/asistencias/{entrada.IdAsistencia}/salida", solicitud);
        Assert.Equal(HttpStatusCode.OK, salida.StatusCode);
        Assert.Equal(6.01m, (await salida.Content.ReadFromJsonAsync<RespuestaAsistencia>())!.PrecisionGpsSalidaMetros);
        using var repetida = await trabajador.PatchAsJsonAsync($"/api/asistencias/{entrada.IdAsistencia}/salida", solicitud);
        Assert.True((await repetida.Content.ReadFromJsonAsync<RespuestaAsistencia>())!.FueIdempotente);
        using var alterada = await trabajador.PatchAsJsonAsync($"/api/asistencias/{entrada.IdAsistencia}/salida", solicitud with { PrecisionGpsSalidaMetros = 7m });
        Assert.Equal(HttpStatusCode.Conflict, alterada.StatusCode);
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias")));
    }

    [Fact]
    public async Task LoteConservaCapturaAntiguaYRechazaRelojFuturoYGpsImprecisoPorElemento()
    {
        await using var entorno = await CrearAsync(cluster, new TiempoControlado(Instante));
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var historica = Captura(escenario, Instante.AddDays(-3)) with
        {
            SalidaEn = Instante.AddDays(-3).AddHours(8), LatitudSalida = 12m, LongitudSalida = -86m,
            PrecisionGpsSalidaMetros = 5m
        };
        var lote = new SolicitudSincronizacionLote(Asistencias:
            [historica, Captura(escenario, Instante.AddSeconds(121)), Captura(escenario, Instante.AddDays(-2), 101m)]);
        var respuesta = await CrearAsync<RespuestaSincronizacionLote>(trabajador, "/api/sincronizacion/lote", lote);
        Assert.Equal(1, respuesta.TotalConfirmadas);
        Assert.Equal(2, respuesta.TotalRechazadas);
        Assert.Contains(respuesta.Acuses, a => a.CodigoError == "captura_futura");
        Assert.Contains(respuesta.Acuses, a => a.CodigoError == "gps_impreciso");
        var guardada = await entorno.EnBaseAsync(c => c.Asistencias.SingleAsync());
        Assert.Equal(historica.EntradaEn, guardada.EntradaEn);
        Assert.Equal(Instante, guardada.SincronizadoEn);
    }

    [Fact]
    public async Task ReenvioDeUnaConfirmacionHistoricaSinPrecisionNoGeneraUnaMarcacionNueva()
    {
        await using var entorno = await CrearAsync(cluster, new TiempoControlado(Instante));
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var perfil = (await trabajador.GetFromJsonAsync<RespuestaSesionActiva>("/api/sesion/actual"))!;
        var uuid = Guid.NewGuid();
        await entorno.EjecutarSqlAsync($"""
            INSERT INTO asistencias(id_asignacion, fecha_jornada, entrada_at, latitud_entrada, longitud_entrada,
                estado, id_usuario_registro, uuid_registro)
            VALUES({escenario.Asignacion.IdAsignacion}, DATE '2026-10-29', TIMESTAMPTZ '2026-10-30 03:00:00+00',
                12, -86, 'ABIERTA', {perfil.IdUsuario}, '{uuid}');
            """);
        var auditorias = await entorno.EnBaseAsync(c => c.Auditorias.CountAsync());
        var respuesta = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", Captura(escenario, Instante, null) with { UuidRegistro = uuid });
        Assert.True(respuesta.FueIdempotente);
        Assert.Null(respuesta.PrecisionGpsEntradaMetros);
        Assert.Equal(auditorias, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync()));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
    }

    [Fact]
    public async Task CorreccionQueCruzaLaFechaLocalActualizaLaJornadaConHistorialDeHoras()
    {
        var ahora = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        await using var entorno = await CrearAsync(cluster, new TiempoControlado(ahora));
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        var entrada = new DateTime(2026, 10, 30, 5, 59, 0, DateTimeKind.Utc);
        var asistencia = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias", Captura(escenario, entrada) with
        {
            SalidaEn = entrada.AddMinutes(31), LatitudSalida = 12m, LongitudSalida = -86m, PrecisionGpsSalidaMetros = 5m
        });
        var corregida = entrada.AddMinutes(2);
        var correccion = await CrearAsync<RespuestaCorreccionAsistencia>(entorno.Cliente, "/api/asistencias/correcciones",
            new SolicitudCrearCorreccionAsistencia(asistencia.IdAsistencia, corregida, null, "Corrección autorizada de fecha y hora de entrada"));
        using var decision = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/correcciones/{correccion.IdCorreccion}/resolucion",
            new SolicitudResolverCorreccionAsistencia(EstadosCorreccionAsistencia.Aprobada));
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        Assert.Equal(new DateOnly(2026, 10, 30), await entorno.EnBaseAsync(c => c.Asistencias.Select(a => a.FechaJornada).SingleAsync()));
        Assert.Equal(entrada, correccion.EntradaAnterior);
        Assert.Equal(corregida, correccion.EntradaNueva);
    }

    [Fact]
    public async Task UnaAsignacionNoVenceSeisHorasAntesPorConfundirUtcConManagua()
    {
        var reloj = new TiempoControlado(Instante);
        await using var entorno = await CrearAsync(cluster, reloj);
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        // La fecha final está incluida; se mantiene ACTIVA hasta finalizar el día de Managua.
        await entorno.EjecutarSqlAsync($"UPDATE asignaciones_trabajador_obra SET fecha_fin=DATE '2026-10-29' WHERE id_asignacion={escenario.Asignacion.IdAsignacion}");
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var perfil = (await trabajador.GetFromJsonAsync<RespuestaSesionActiva>("/api/sesion/actual"))!;
        Assert.Single((await trabajador.GetFromJsonAsync<List<RespuestaAsignacionAsistencia>>("/api/asistencias/mis-asignaciones"))!);
        reloj.Avanzar(TimeSpan.FromHours(2));
        await AutenticarAsync(trabajador, perfil.NombreUsuario, Contrasena);
        Assert.Single((await trabajador.GetFromJsonAsync<List<RespuestaAsignacionAsistencia>>("/api/asistencias/mis-asignaciones"))!);
        reloj.Avanzar(TimeSpan.FromHours(1));
        await AutenticarAsync(trabajador, perfil.NombreUsuario, Contrasena);
        Assert.Empty((await trabajador.GetFromJsonAsync<List<RespuestaAsignacionAsistencia>>("/api/asistencias/mis-asignaciones"))!);
    }

    [Fact]
    public async Task RedondearLasCoordenadasParaGuardarNoHabilitaUnaLecturaFueraDelRadio()
    {
        await using var entorno = await CrearAsync(cluster, new TiempoControlado(Instante));
        var escenario = await entorno.CrearEscenarioAsync(0m, 0m, 100m);
        var consulta = await CrearAsync<RespuestaValidacionUbicacionAsistencia>(entorno.Cliente, "/api/asistencias/validar-ubicacion",
            new SolicitudValidarUbicacionAsistencia(escenario.Asignacion.IdAsignacion, 0m, .0008994m, 0m));
        Assert.False(consulta.MarcacionHabilitada);
        using var captura = await entorno.Cliente.PostAsJsonAsync("/api/asistencias", Captura(escenario, Instante) with
        { LatitudEntrada = 0m, LongitudEntrada = .0008994m, PrecisionGpsEntradaMetros = 0m });
        await ErrorAsync(captura, "ubicacion_fuera_de_obra");
    }

    private static async Task ErrorAsync(HttpResponseMessage respuesta, string codigo)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var json = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync());
        Assert.Equal(codigo, json.RootElement.GetProperty("codigo").GetString());
    }
}
