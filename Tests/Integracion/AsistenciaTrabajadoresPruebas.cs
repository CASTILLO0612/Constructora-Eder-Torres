using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Abstractions;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class AsistenciaTrabajadoresPruebas(ClusterPostgreSqlDesechableFixture cluster, ITestOutputHelper salidaPruebas)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private const decimal Latitud = 12m;
    private const decimal Longitud = -86m;
    private static DateTime Ahora => new(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private static SolicitudRegistrarAsistencia Entrada(EscenarioObra escenario, DateTime? hora = null)
    {
        var instante = hora ?? CalendarioPruebas.InstanteUtc(CalendarioPruebas.Hoy.AddDays(-1), new TimeOnly(8, 0));
        return new(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(instante), instante,
            LatitudEntrada: Latitud, LongitudEntrada: Longitud, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m);
    }

    [Fact]
    public async Task AlbanilIniciaSesionMarcaSuJornadaYConsultaSoloSuContexto()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m, "ALBAÑIL");
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var sesion = (await trabajador.GetFromJsonAsync<RespuestaSesionActiva>("/api/sesion/actual"))!;
        Assert.False(sesion.TieneAlcanceGlobal);
        Assert.Contains(CodigosPermiso.RegistrarAsistenciaPropia, sesion.Permisos);
        Assert.DoesNotContain(CodigosPermiso.RegistrarAsistencia, sesion.Permisos);
        var asignaciones = (await trabajador.GetFromJsonAsync<List<RespuestaAsignacionAsistencia>>("/api/asistencias/mis-asignaciones"))!;
        Assert.Equal(escenario.Asignacion.IdAsignacion, Assert.Single(asignaciones).IdAsignacion);
        Assert.Equal(100m, asignaciones[0].RadioAsistenciaMetros);
        var validacion = await CrearAsync<RespuestaValidacionUbicacionAsistencia>(trabajador, "/api/asistencias/validar-ubicacion",
            new SolicitudValidarUbicacionAsistencia(escenario.Asignacion.IdAsignacion, Latitud, Longitud, PrecisionGpsMetros: 5m));
        Assert.True(validacion.MarcacionHabilitada);
        Assert.Equal(0, validacion.DistanciaMetros);
        var solicitud = Entrada(escenario);
        var asistencia = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", solicitud);
        Assert.Equal(sesion.IdUsuario, asistencia.IdUsuarioRegistro);
        var abierta = (await trabajador.GetFromJsonAsync<List<RespuestaAsignacionAsistencia>>("/api/asistencias/mis-asignaciones"))!;
        Assert.Equal(asistencia.IdAsistencia, Assert.Single(abierta).IdAsistenciaAbierta);
        using var salida = await trabajador.PatchAsJsonAsync($"/api/asistencias/{asistencia.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(solicitud.EntradaEn.AddHours(8), Latitud, Longitud, solicitud.UuidRegistro, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.OK, salida.StatusCode);
        var completada = (await salida.Content.ReadFromJsonAsync<RespuestaAsistencia>())!;
        Assert.Equal(EstadosAsistencia.Completa, completada.Estado);
        var historia = (await trabajador.GetFromJsonAsync<List<RespuestaAsistencia>>("/api/asistencias"))!;
        Assert.Equal(asistencia.IdAsistencia, Assert.Single(historia).IdAsistencia);
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias" && a.IdUsuario == sesion.IdUsuario)));
    }

    [Fact]
    public async Task TrabajadorNoMarcaConsultaNiCorrigeLaAsistenciaDeUnColegaEnLaMismaObra()
    {
        await using var entorno = await CrearAsync(cluster);
        var propio = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var colega = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var asignacionColega = await CrearAsync<RespuestaAsignacionTrabajadorObra>(entorno.Cliente, "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(colega.Trabajador.IdTrabajador, propio.Obra.IdObra, null, TiposContratacion.Fijo, CalendarioPruebas.Fecha(Ahora).AddDays(-1)));
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, propio.Trabajador.IdTrabajador);
        var asistenciaColega = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias",
            Entrada(colega) with { IdAsignacion = asignacionColega.IdAsignacion });
        foreach (var ruta in new[] { $"/api/asistencias/{asistenciaColega.IdAsistencia}",
            $"/api/asistencias?idTrabajador={colega.Trabajador.IdTrabajador}", "/api/asistencias/consolidado",
            "/api/asistencias/correcciones", "/api/usuarios", "/api/personal/trabajadores", "/api/obras" })
        {
            using var consulta = await trabajador.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Forbidden, consulta.StatusCode);
        }
        using var entradaAjena = await trabajador.PostAsJsonAsync("/api/asistencias", Entrada(colega) with { IdAsignacion = asignacionColega.IdAsignacion });
        await ComprobarErrorAsync(entradaAjena, HttpStatusCode.Forbidden, "asistencia_ajena");
        using var reenvioAjeno = await trabajador.PostAsJsonAsync("/api/asistencias", Entrada(colega) with
            { IdAsignacion = asignacionColega.IdAsignacion, UuidRegistro = asistenciaColega.UuidRegistro });
        await ComprobarErrorAsync(reenvioAjeno, HttpStatusCode.Forbidden, "asistencia_ajena");
        using var salidaAjena = await trabajador.PatchAsJsonAsync($"/api/asistencias/{asistenciaColega.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(Ahora.AddHours(8), Latitud, Longitud, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.Forbidden, salidaAjena.StatusCode);
        using var correccion = await trabajador.PostAsJsonAsync("/api/asistencias/correcciones",
            new SolicitudCrearCorreccionAsistencia(asistenciaColega.IdAsistencia, Ahora.AddMinutes(-5), null, "Intento ajeno"));
        Assert.Equal(HttpStatusCode.Forbidden, correccion.StatusCode);
        using var cuadrilla = await trabajador.PostAsJsonAsync("/api/asistencias/cuadrilla",
            new SolicitudRegistrarAsistenciaCuadrilla(propio.Obra.IdObra, CalendarioPruebas.Fecha(Ahora), Latitud, Longitud,
                [new(propio.Asignacion.IdAsignacion, Ahora)], PrecisionGpsMetros: 5m));
        Assert.Equal(HttpStatusCode.Forbidden, cuadrilla.StatusCode);
        Assert.Empty((await trabajador.GetFromJsonAsync<List<RespuestaAsistencia>>("/api/asistencias"))!);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
    }

    [Fact]
    public async Task ResidentePuedeMarcarComoApoyoYElTrabajadorCerrarSuPropiaAsistencia()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var residente = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        await CrearAsync<RespuestaAsignacionTrabajadorObra>(entorno.Cliente, "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(residente.Trabajador.IdTrabajador, escenario.Obra.IdObra, null,
                TiposContratacion.Fijo, CalendarioPruebas.Fecha(Ahora).AddDays(-1)));
        using var clienteResidente = await entorno.CrearUsuarioAsync(NombresRol.IngenieroResidente, residente.Trabajador.IdTrabajador);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var asistencia = await CrearAsync<RespuestaAsistencia>(clienteResidente, "/api/asistencias", Entrada(escenario));
        using var salida = await trabajador.PatchAsJsonAsync($"/api/asistencias/{asistencia.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(asistencia.EntradaEn.AddHours(8), Latitud, Longitud, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.OK, salida.StatusCode);
        var actorSalida = await entorno.EnBaseAsync(c => c.Auditorias.Where(a => a.Entidad == "asistencias" && a.Campo == "salida_at").Select(a => a.IdUsuario).SingleAsync());
        Assert.NotEqual(asistencia.IdUsuarioRegistro, actorSalida);
        using var otraObra = await clienteResidente.PostAsJsonAsync("/api/asistencias", Entrada(residente) with { IdAsignacion = int.MaxValue });
        Assert.Equal(HttpStatusCode.NotFound, otraObra.StatusCode);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(12.0, null)]
    [InlineData(null, -86.0)]
    [InlineData(91.0, -86.0)]
    [InlineData(12.0, -181.0)]
    [InlineData(12.002, -86.0)]
    public async Task EntradaSinGpsValidoODentroDelRadioNoEsPersistida(double? latitud, double? longitud)
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var auditorias = await entorno.EnBaseAsync(c => c.Auditorias.CountAsync());
        using var respuesta = await trabajador.PostAsJsonAsync("/api/asistencias", Entrada(escenario) with
            { LatitudEntrada = (decimal?)latitud, LongitudEntrada = (decimal?)longitud });
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        Assert.Equal(auditorias, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync()));
    }

    [Fact]
    public async Task PrevalidacionNoReservaLaUbicacionYEntradaSalidaYLoteSiempreVuelvenAValidarla()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var fuera = await CrearAsync<RespuestaValidacionUbicacionAsistencia>(trabajador, "/api/asistencias/validar-ubicacion",
            new SolicitudValidarUbicacionAsistencia(escenario.Asignacion.IdAsignacion, Latitud + .002m, Longitud, PrecisionGpsMetros: 5m));
        Assert.False(fuera.MarcacionHabilitada);
        Assert.InRange(fuera.DistanciaMetros, 222m, 223m);
        var solicitud = Entrada(escenario);
        var asistencia = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", solicitud);
        using var salidaFuera = await trabajador.PatchAsJsonAsync($"/api/asistencias/{asistencia.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(solicitud.EntradaEn.AddHours(8), Latitud + .002m, Longitud, PrecisionGpsSalidaMetros: 5m));
        await ComprobarErrorAsync(salidaFuera, HttpStatusCode.BadRequest, "ubicacion_fuera_de_obra");
        Assert.Null((await trabajador.GetFromJsonAsync<RespuestaAsistencia>($"/api/asistencias/{asistencia.IdAsistencia}"))!.SalidaEn);
        await entorno.EjecutarSqlAsync($"UPDATE obras SET latitud=13 WHERE id_obra={escenario.Obra.IdObra}");
        using var salidaDespuesDeCambio = await trabajador.PatchAsJsonAsync($"/api/asistencias/{asistencia.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(solicitud.EntradaEn.AddHours(8), Latitud, Longitud, PrecisionGpsSalidaMetros: 5m));
        await ComprobarErrorAsync(salidaDespuesDeCambio, HttpStatusCode.BadRequest, "ubicacion_fuera_de_obra");
        var replay = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", solicitud);
        Assert.True(replay.FueIdempotente);
        var lote = await CrearAsync<RespuestaSincronizacionLote>(trabajador, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(SalidasAsistencia: [new(null, solicitud.EntradaEn.AddHours(8), Latitud, Longitud, solicitud.UuidRegistro, PrecisionGpsSalidaMetros: 5m)]));
        Assert.Equal("ubicacion_fuera_de_obra", Assert.Single(lote.Acuses).CodigoError);
        Assert.Equal(0, lote.TotalConfirmadas);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias")));
    }

    [Fact]
    public async Task EntradaYSalidaOfflineSeRelacionanPorUuidSinNecesitarUnIdDelServidor()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var entrada = Entrada(escenario, Ahora.AddDays(-1));
        var lote = new SolicitudSincronizacionLote(Asistencias: [entrada],
            SalidasAsistencia: [new(null, entrada.EntradaEn.AddHours(8), Latitud, Longitud, entrada.UuidRegistro, PrecisionGpsSalidaMetros: 5m)]);
        var primera = await CrearAsync<RespuestaSincronizacionLote>(trabajador, "/api/sincronizacion/lote", lote);
        Assert.Equal(2, primera.TotalConfirmadas);
        var segunda = await CrearAsync<RespuestaSincronizacionLote>(trabajador, "/api/sincronizacion/lote", lote);
        Assert.Equal(2, segunda.TotalIdempotentes);
        var asistencia = Assert.Single((await trabajador.GetFromJsonAsync<List<RespuestaAsistencia>>("/api/asistencias"))!);
        Assert.Equal(entrada.EntradaEn, asistencia.EntradaEn);
        Assert.Equal(entrada.FechaJornada, asistencia.FechaJornada);
        Assert.Equal(entrada.EntradaEn.AddHours(8), asistencia.SalidaEn);
        Assert.NotNull(asistencia.SincronizadoEn);
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias")));
    }

    [Fact]
    public async Task LoteDelTrabajadorConfirmaSoloSuAsistenciaValidaYRechazaCadaOperacionAjena()
    {
        await using var entorno = await CrearAsync(cluster);
        var propio = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var otro = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, propio.Trabajador.IdTrabajador);
        var valida = Entrada(propio);
        var respuesta = await CrearAsync<RespuestaSincronizacionLote>(trabajador, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Asistencias: [Entrada(propio) with { LatitudEntrada = 13m }, Entrada(otro), valida],
                Avances: [new(1, CalendarioPruebas.Fecha(Ahora), 1m, UuidRegistro: Guid.NewGuid())]));
        Assert.Equal(1, respuesta.TotalConfirmadas);
        Assert.Equal(3, respuesta.TotalRechazadas);
        Assert.Contains(respuesta.Acuses, a => a.CodigoError == "ubicacion_fuera_de_obra");
        Assert.Equal(2, respuesta.Acuses.Count(a => a.EstadoAcuse == EstadosAcuseSincronizacion.RechazadoAutorizacion));
        Assert.Equal(valida.UuidRegistro, await entorno.EnBaseAsync(c => c.Asistencias.Select(a => a.UuidRegistro).SingleAsync()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarcacionesConcurrentesSoloCreanUnaJornadaAbiertaInclusoEntreObras(bool mismoUuid)
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var otraObra = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var segundaAsignacion = await CrearAsync<RespuestaAsignacionTrabajadorObra>(entorno.Cliente, "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(escenario.Trabajador.IdTrabajador, otraObra.Obra.IdObra, null,
                TiposContratacion.Fijo, CalendarioPruebas.Fecha(Ahora).AddDays(-1)));
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var solicitud = Entrada(escenario);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => trabajador.PostAsJsonAsync("/api/asistencias",
            mismoUuid ? solicitud : solicitud with { IdAsignacion = i % 2 == 0 ? escenario.Asignacion.IdAsignacion : segundaAsignacion.IdAsignacion,
                EntradaEn = solicitud.EntradaEn.AddMinutes(i), UuidRegistro = Guid.NewGuid() })));
        try
        {
            Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.All(respuestas, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict));
            if (mismoUuid) Assert.All(respuestas, r => Assert.True(r.IsSuccessStatusCode));
            else Assert.Equal(7, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(1, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
            Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias")));
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Fact]
    public async Task CuentaIndividualRequiereVinculoUnicoYRevocaAccesoAlDesactivarAlTrabajador()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var rol = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaRol>>("/api/roles"))!.Single(r => r.Nombre == NombresRol.Trabajador);
        using var sinVinculo = await entorno.Cliente.PostAsJsonAsync("/api/usuarios", new SolicitudCrearUsuario("sin.vinculo", Contrasena, null, [rol.IdRol]));
        await ComprobarErrorAsync(sinVinculo, HttpStatusCode.BadRequest, "trabajador_requerido");
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var perfil = (await trabajador.GetFromJsonAsync<RespuestaSesionActiva>("/api/sesion/actual"))!;
        using var quitarVinculo = await entorno.Cliente.PutAsJsonAsync($"/api/usuarios/{perfil.IdUsuario}", new SolicitudActualizarUsuario(perfil.NombreUsuario, null));
        await ComprobarErrorAsync(quitarVinculo, HttpStatusCode.BadRequest, "trabajador_requerido");
        using var duplicada = await entorno.Cliente.PostAsJsonAsync("/api/usuarios", new SolicitudCrearUsuario("duplicada", Contrasena, escenario.Trabajador.IdTrabajador, [rol.IdRol]));
        await ComprobarErrorAsync(duplicada, HttpStatusCode.Conflict, "trabajador_ya_vinculado");
        using var desactivar = await entorno.Cliente.PatchAsJsonAsync($"/api/personal/trabajadores/{escenario.Trabajador.IdTrabajador}/estado",
            new SolicitudCambiarEstadoTrabajador(EstadosTrabajador.Inactivo));
        Assert.Equal(HttpStatusCode.OK, desactivar.StatusCode);
        using var acceso = await trabajador.GetAsync("/api/asistencias/mis-asignaciones");
        Assert.Equal(HttpStatusCode.Unauthorized, acceso.StatusCode);
    }

    [Fact]
    public async Task AsignacionFinalizadaImpideNuevasMarcacionesPeroConservaElHistorialPropio()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var solicitud = Entrada(escenario) with { SalidaEn = Ahora, LatitudSalida = Latitud, LongitudSalida = Longitud, PrecisionGpsSalidaMetros = 5m };
        var creada = await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", solicitud);
        using var cierre = await entorno.Cliente.PatchAsJsonAsync($"/api/personal/asignaciones-obra/{escenario.Asignacion.IdAsignacion}/finalizacion",
            new SolicitudFinalizarAsignacionObra(CalendarioPruebas.Fecha(Ahora)));
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        Assert.Empty((await trabajador.GetFromJsonAsync<List<RespuestaAsignacionAsistencia>>("/api/asistencias/mis-asignaciones"))!);
        Assert.Equal(creada.IdAsistencia, Assert.Single((await trabajador.GetFromJsonAsync<List<RespuestaAsistencia>>("/api/asistencias"))!).IdAsistencia);
        using var detalle = await trabajador.GetAsync($"/api/asistencias/{creada.IdAsistencia}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        using var nueva = await trabajador.PostAsJsonAsync("/api/asistencias", Entrada(escenario));
        Assert.Equal(HttpStatusCode.Forbidden, nueva.StatusCode);
    }

    [Theory]
    [InlineData(null, -86.0, 100.0)]
    [InlineData(12.0, null, 100.0)]
    [InlineData(12.0, -86.0, null)]
    [InlineData(12.0, -86.0, 0.0)]
    [InlineData(12.0, -86.0, -10.0)]
    [InlineData(91.0, -86.0, 100.0)]
    [InlineData(12.0, -181.0, 100.0)]
    public async Task AltaDeObraExigeGeocercaCompletaYRadioPositivo(double? latitud, double? longitud, double? radio)
    {
        await using var entorno = await CrearAsync(cluster);
        var hoy = CalendarioPruebas.Fecha(Ahora);
        using var respuesta = await entorno.Cliente.PostAsJsonAsync("/api/obras", new SolicitudCrearObra("GEO", "Obra", null,
            "Dirección", hoy, hoy, Latitud: (decimal?)latitud, Longitud: (decimal?)longitud, RadioAsistenciaMetros: (decimal?)radio));
        await ComprobarErrorAsync(respuesta, HttpStatusCode.BadRequest, "geocerca_invalida");
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Obras.CountAsync()));
    }

    [Fact]
    public async Task ObraSinUbicacionPreexistenteBloqueaLaMarcacionYPostgreSqlRechazaGeocercasIncompletas()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        await entorno.EjecutarSqlAsync($"UPDATE obras SET latitud=NULL, longitud=NULL, radio_asistencia_metros=NULL WHERE id_obra={escenario.Obra.IdObra}");
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        using var marcacion = await trabajador.PostAsJsonAsync("/api/asistencias", Entrada(escenario));
        await ComprobarErrorAsync(marcacion, HttpStatusCode.Conflict, "geocerca_no_configurada");
        var error = await Assert.ThrowsAsync<PostgresException>(() => entorno.EjecutarSqlAsync($"UPDATE obras SET latitud=12 WHERE id_obra={escenario.Obra.IdObra}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        using var actualizar = await entorno.Cliente.PutAsJsonAsync($"/api/obras/{escenario.Obra.IdObra}",
            new SolicitudActualizarObra(escenario.Obra.Nombre, null, escenario.Obra.Direccion,
                escenario.Obra.FechaInicioPlan, escenario.Obra.FechaFinPlan, null, null, Latitud, Longitud, 100m));
        Assert.Equal(HttpStatusCode.OK, actualizar.StatusCode);
        await CrearAsync<RespuestaAsistencia>(trabajador, "/api/asistencias", Entrada(escenario));
    }

    private static async Task ComprobarErrorAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        using var cuerpo = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync());
        Assert.Equal(codigo, cuerpo.RootElement.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task CorreccionAutorizadaNoPuedeSuperponerDosJornadasDelTrabajador()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var hora = Ahora.AddHours(-4);
        var primera = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias", Entrada(escenario, hora) with
            { SalidaEn = hora.AddHours(1), LatitudSalida = Latitud, LongitudSalida = Longitud, PrecisionGpsSalidaMetros = 5m });
        await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias", Entrada(escenario, hora.AddHours(2)) with
            { SalidaEn = hora.AddHours(3), LatitudSalida = Latitud, LongitudSalida = Longitud, PrecisionGpsSalidaMetros = 5m });
        var correccion = await CrearAsync<RespuestaCorreccionAsistencia>(entorno.Cliente, "/api/asistencias/correcciones",
            new SolicitudCrearCorreccionAsistencia(primera.IdAsistencia, null, hora.AddHours(2.5), "Revisión de salida"));
        using var decision = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/correcciones/{correccion.IdCorreccion}/resolucion",
            new SolicitudResolverCorreccionAsistencia(EstadosCorreccionAsistencia.Aprobada));
        await ComprobarErrorAsync(decision, HttpStatusCode.Conflict, "asistencia_solapada");
        Assert.Equal(primera.SalidaEn, await entorno.EnBaseAsync(c => c.Asistencias.Where(a => a.IdAsistencia == primera.IdAsistencia).Select(a => a.SalidaEn).SingleAsync()));
        Assert.Equal(EstadosCorreccionAsistencia.Pendiente, await entorno.EnBaseAsync(c => c.CorreccionesAsistencia.Select(a => a.Estado).SingleAsync()));
    }

    [Fact]
    public async Task ReaplicarElScriptNoRecreaObjetosNiRestablecePermisosRevocados()
    {
        await using var entorno = await CrearAsync(cluster);
        await entorno.EjecutarSqlAsync("""
            UPDATE permisos SET activo=false WHERE codigo='asistencia.propia.registrar';
            DELETE FROM roles_permiso WHERE id_rol=(SELECT id_rol FROM roles WHERE nombre='TRABAJADOR')
                AND id_permiso=(SELECT id_permiso FROM permisos WHERE codigo='asistencia.propia.consultar');
            """);
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "ConstructoraEderTorres.sln"))) carpeta = carpeta.Parent;
        var sql = await File.ReadAllTextAsync(Path.Combine(carpeta!.FullName, "database", "ActualizarAsistenciaTrabajadores.sql"));
        await entorno.EjecutarSqlAsync(sql);
        await entorno.EjecutarSqlAsync(sql);
        Assert.False(await entorno.EnBaseAsync(c => c.Permisos.Where(p => p.Codigo == CodigosPermiso.RegistrarAsistenciaPropia).Select(p => p.Activo).SingleAsync()));
        Assert.False(await entorno.EnBaseAsync(c => (from rp in c.RolesPermiso join p in c.Permisos on rp.IdPermiso equals p.IdPermiso
            join r in c.Roles on rp.IdRol equals r.IdRol where r.Nombre == NombresRol.Trabajador
                && p.Codigo == CodigosPermiso.ConsultarAsistenciaPropia select rp).AnyAsync()));
        Assert.True(await entorno.EnBaseAsync(async c => c.Database.GetMigrations().SequenceEqual(await c.Database.GetAppliedMigrationsAsync())));
    }

    [Fact]
    public async Task ListaAsistidaComparteLaLecturaDelResidenteYSeReenviaSinDuplicaciones()
    {
        await using var entorno = await CrearAsync(cluster);
        var residente = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var otro = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        var asignacion = await CrearAsync<RespuestaAsignacionTrabajadorObra>(entorno.Cliente, "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(otro.Trabajador.IdTrabajador, residente.Obra.IdObra, null,
                TiposContratacion.Fijo, CalendarioPruebas.Fecha(Ahora).AddDays(-1)));
        using var cliente = await entorno.CrearUsuarioAsync(NombresRol.IngenieroResidente, residente.Trabajador.IdTrabajador);
        var solicitud = new SolicitudRegistrarAsistenciaCuadrilla(residente.Obra.IdObra, CalendarioPruebas.Fecha(Ahora), 13m, Longitud,
            [new(residente.Asignacion.IdAsignacion, Ahora, UuidRegistro: Guid.NewGuid()),
             new(asignacion.IdAsignacion, Ahora, UuidRegistro: Guid.NewGuid())], PrecisionGpsMetros: 5m);
        using var fuera = await cliente.PostAsJsonAsync("/api/asistencias/cuadrilla", solicitud);
        await ComprobarErrorAsync(fuera, HttpStatusCode.BadRequest, "ubicacion_fuera_de_obra");
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        solicitud = solicitud with { LatitudLectura = Latitud };
        var primera = await CrearAsync<List<RespuestaAsistencia>>(cliente, "/api/asistencias/cuadrilla", solicitud);
        Assert.Equal(2, primera.Count);
        Assert.All(primera, a => Assert.Equal(Latitud, a.LatitudEntrada));
        var segunda = await CrearAsync<List<RespuestaAsistencia>>(cliente, "/api/asistencias/cuadrilla", solicitud);
        Assert.All(segunda, a => Assert.True(a.FueIdempotente));
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
    }

    [Fact]
    public async Task TrabajadorMarcaPorHttpRealYConsultaHistorialPaginadoBajoCarga()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(Latitud, Longitud, 100m);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var perfil = (await trabajador.GetFromJsonAsync<RespuestaSesionActiva>("/api/sesion/actual"))!;
        await entorno.EjecutarSqlAsync($"""
            INSERT INTO asistencias(id_asignacion, fecha_jornada, entrada_at, salida_at, estado, id_usuario_registro, uuid_registro)
            SELECT {escenario.Asignacion.IdAsignacion}, CURRENT_DATE,
                CURRENT_TIMESTAMP - interval '10000 minutes' + (i || ' minutes')::interval,
                CURRENT_TIMESTAMP - interval '10000 minutes' + (i || ' minutes')::interval + interval '20 seconds',
                'COMPLETA', {perfil.IdUsuario}, gen_random_uuid() FROM generate_series(1,1000) i;
            """);
        await using var servidor = await ServidorKestrelPruebas.IniciarAsync(entorno);
        var cliente = servidor.Cliente;
        var token = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
        await AutenticarAsync(cliente, perfil.NombreUsuario, Contrasena);
        cliente.DefaultRequestHeaders.Remove(token.Cabecera);
        token = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
        var asistencia = await CrearAsync<RespuestaAsistencia>(cliente, "/api/asistencias", Entrada(escenario));
        using var salida = await cliente.PatchAsJsonAsync($"/api/asistencias/{asistencia.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(asistencia.EntradaEn.AddHours(8), Latitud, Longitud, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.OK, salida.StatusCode);
        var tiempos = await Task.WhenAll(Enumerable.Range(0,25).Select(async _ =>
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await cliente.GetAsync("/api/asistencias?tamanoPagina=7");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var pagina = (await respuesta.Content.ReadFromJsonAsync<List<RespuestaAsistencia>>())!;
            Assert.Equal(7, pagina.Count);
            Assert.All(pagina, a => Assert.Equal(escenario.Trabajador.IdTrabajador, a.IdTrabajador));
            return reloj.Elapsed.TotalMilliseconds;
        }));
        var p95 = tiempos.Order().ElementAt((int)Math.Ceiling(tiempos.Length * .95) - 1);
        salidaPruebas.WriteLine($"Historial propio: 1001 asistencias, Kestrel HTTP + PostgreSQL, 25 solicitudes, p95={p95:F2} ms.");
        Assert.True(p95 < 5000, $"El p95 del historial propio fue {p95:F2} ms.");
    }
}
