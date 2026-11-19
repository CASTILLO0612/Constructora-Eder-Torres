using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Presentacion.Identidad;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class ApiConsolidadaPruebas(ClusterPostgreSqlDesechableFixture cluster, ITestOutputHelper salida)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task DllRealRespondePorHttpConPostgreSqlYCargaDeVeinticincoClientes()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        await entorno.EjecutarSqlAsync($"""
            INSERT INTO asistencias(id_asignacion, fecha_jornada, entrada_at, salida_at, estado, id_usuario_registro, uuid_registro)
            SELECT {escenario.Asignacion.IdAsignacion}, CURRENT_DATE, CURRENT_TIMESTAMP + (i || ' minutes')::interval,
                CURRENT_TIMESTAMP + (i || ' minutes')::interval + interval '8 hours', 'COMPLETA',
                (SELECT id_usuario FROM usuarios LIMIT 1), gen_random_uuid() FROM generate_series(1, 2000) i;
            """);
        await using var servidor = await ServidorKestrelPruebas.IniciarAsync(entorno);
        var cliente = servidor.Cliente;
        using var contratoHttp = await cliente.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, contratoHttp.StatusCode);
        var token = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
        await AutenticarAsync(cliente, NombreAdministrador, Contrasena);
        cliente.DefaultRequestHeaders.Remove(token.Cabecera);
        token = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
        using var sinGpsPorHttp = await cliente.PostAsJsonAsync("/api/asistencias",
            new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion,
                CalendarioPruebas.Hoy, DateTime.UtcNow, PrecisionGpsEntradaMetros: 5m));
        Assert.Equal(HttpStatusCode.BadRequest, sinGpsPorHttp.StatusCode);
        var obra = await CrearAsync<RespuestaObra>(cliente, "/api/obras",
            new SolicitudCrearObra("KESTREL", "Obra por HTTP real", null, "Dirección", CalendarioPruebas.Hoy, CalendarioPruebas.Hoy,
                Latitud: 12m, Longitud: -86m, RadioAsistenciaMetros: 100m));
        using var detalle = await cliente.GetAsync($"/api/obras/{obra.IdObra}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        var tiempos = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await cliente.GetAsync($"/api/asistencias/consolidado?idObra={escenario.Obra.IdObra}");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var consolidado = (await respuesta.Content.ReadFromJsonAsync<List<ItemConsolidadoAsistencia>>())!;
            Assert.Equal(2000, Assert.Single(consolidado).TotalJornadasRegistradas);
            return reloj.Elapsed.TotalMilliseconds;
        }));
        var p95 = tiempos.Order().ElementAt((int)Math.Ceiling(tiempos.Length * .95) - 1);
        salida.WriteLine($"Kestrel HTTP real + PostgreSQL, 2000 asistencias, 25 solicitudes: p95={p95:F2} ms; máximo={tiempos.Max():F2} ms.");
        Assert.True(p95 < 5000, $"El p95 de Kestrel fue {p95:F2} ms.");
    }

    [Fact]
    public async Task TodosLosModulosExigenAutenticacion()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();
        foreach (var ruta in new[] { "/api/usuarios", "/api/roles", "/api/obras", "/api/personal/trabajadores",
            "/api/asistencias/consolidado", "/api/seguimiento/obras/1/actividades", "/api/epp/tipos", "/api/herramientas/categorias" })
        {
            using var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.True(respuesta.Headers.CacheControl?.NoStore);
        }
        using var lote = await cliente.PostAsJsonAsync("/api/sincronizacion/lote", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, lote.StatusCode);
    }

    [Fact]
    public async Task OchoTiposDeOperacionSeSincronizanYSeReenvianSinDuplicarDatosNiInventario()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var original = await entorno.CrearEppAsync();
        var sustituto = await entorno.CrearEppAsync(original.IdTipoEpp);
        var herramienta = await entorno.CrearHerramientaAsync();
        var ahora = DateTime.UtcNow;
        var hoy = CalendarioPruebas.Fecha(ahora);
        var loteInicial = new SolicitudSincronizacionLote(
            Asistencias: [new(escenario.Asignacion.IdAsignacion, hoy, ahora, LatitudEntrada: 12.1m, LongitudEntrada: -86.2m, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m)],
            Avances: [new(actividad.IdActividad, hoy, 10m, UuidRegistro: Guid.NewGuid())],
            IncidenciasObra: [new(escenario.Obra.IdObra, "CLIMA", ahora, null, null, "Lluvia", null, UuidRegistro: Guid.NewGuid())],
            EntregasEpp: [new(original.IdEpp, escenario.Asignacion.IdAsignacion, ahora, UuidRegistro: Guid.NewGuid())],
            AsignacionesHerramienta: [new(herramienta.IdHerramienta, escenario.Asignacion.IdAsignacion, ahora, UuidRegistro: Guid.NewGuid())]);
        var primera = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote", loteInicial);
        Assert.Equal(5, primera.TotalConfirmadas);
        long IdDe(string tipo) => long.Parse(primera.Acuses.Single(a => a.TipoOperacion == tipo).IdRegistroServidor!, System.Globalization.CultureInfo.InvariantCulture);
        var loteFinal = new SolicitudSincronizacionLote(
            Evidencias: [new(IdDe(TiposOperacionSincronizacion.Avance), TiposEvidencia.Fotografia,
                NombreArchivo: "evidencia.png", ContenidoArchivo: FotoPng, CapturadaEn: ahora,
                Latitud: 12.1m, Longitud: -86.2m, UuidRegistro: Guid.NewGuid())],
            ReposicionesEpp: [new(IdDe(TiposOperacionSincronizacion.EntregaEpp), sustituto.IdEpp,
                MotivosReposicionEpp.Dano, "Reposición de prueba", true, ahora.AddHours(1), Guid.NewGuid())],
            EventosHerramienta: [new(IdDe(TiposOperacionSincronizacion.AsignacionHerramienta),
                TiposIncidenciaHerramienta.Devolucion, ahora.AddHours(1), "Devolución", UuidRegistro: Guid.NewGuid())]);
        var segunda = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote", loteFinal);
        Assert.Equal(3, segunda.TotalConfirmadas);
        var movimientos = await entorno.EnBaseAsync(c => c.MovimientosExistenciasEpp.CountAsync());
        var repeticion1 = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote", loteInicial);
        var repeticion2 = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote", loteFinal);
        Assert.Equal(5, repeticion1.TotalIdempotentes);
        Assert.Equal(3, repeticion2.TotalIdempotentes);
        Assert.Equal(movimientos, await entorno.EnBaseAsync(c => c.MovimientosExistenciasEpp.CountAsync()));
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.EntregasEpp.CountAsync()));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
    }

    [Fact]
    public async Task LimitesDeLoteYDelCuerpoHttpEvitanOperacionesSinAcotar()
    {
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string, string?>
        {
            ["Api:MaximoBytesSolicitud"] = "2048",
            ["Sincronizacion:MaximoOperaciones"] = "1"
        });
        var ahora = DateTime.UtcNow;
        using var lote = await entorno.Cliente.PostAsJsonAsync("/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Asistencias: [
                new(1, CalendarioPruebas.Fecha(ahora), ahora, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m),
                new(2, CalendarioPruebas.Fecha(ahora), ahora, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m)]));
        Assert.Equal(HttpStatusCode.BadRequest, lote.StatusCode);
        using var grande = await entorno.Cliente.PostAsJsonAsync("/api/obras", new { relleno = new string('X', 4096) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, grande.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Obras.CountAsync()));
    }

    [Fact]
    public async Task LosDoceDisparadoresMultiobraRechazanAsociacionesYCambiosDeProyecto()
    {
        await using var entorno = await CrearAsync(cluster);
        var a = await entorno.CrearEscenarioAsync();
        var b = await entorno.CrearEscenarioAsync();
        var actividadA = await entorno.CrearActividadAsync(a);
        var actividadB = await entorno.CrearActividadAsync(b);
        var hoy = CalendarioPruebas.Hoy;
        var avanceB = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances", new SolicitudRegistrarAvance(actividadB.IdActividad, hoy, 1));
        var incidenciaA = await CrearAsync<RespuestaIncidenciaObra>(entorno.Cliente, "/api/seguimiento/incidencias",
            new SolicitudRegistrarIncidenciaObra(a.Obra.IdObra, "CLIMA", DateTime.UtcNow, null, null, "Prueba", null));
        var incidenciaB = await CrearAsync<RespuestaIncidenciaObra>(entorno.Cliente, "/api/seguimiento/incidencias",
            new SolicitudRegistrarIncidenciaObra(b.Obra.IdObra, "CLIMA", DateTime.UtcNow, null, null, "Prueba", null));
        var actaA = await CrearAsync<RespuestaActa>(entorno.Cliente, "/api/seguimiento/actas", new SolicitudElaborarActa(a.Obra.IdObra, "ACTA-1", hoy, hoy));
        var cuadrilla = await CrearAsync<RespuestaCuadrilla>(entorno.Cliente, "/api/personal/cuadrillas", new SolicitudCuadrilla("C-1", "Cuadrilla", null));
        var cuadrillaObra = await CrearAsync<RespuestaCuadrillaObra>(entorno.Cliente, "/api/personal/cuadrillas-obra",
            new SolicitudAsignarCuadrillaObra(cuadrilla.IdCuadrilla, a.Obra.IdObra, hoy));
        var sentencias = new[] {
            $"UPDATE actividades SET id_asignacion_responsable={b.Asignacion.IdAsignacion} WHERE id_actividad={actividadA.IdActividad}",
            $"INSERT INTO trabajadores_actividad(id_actividad,id_asignacion) VALUES({actividadA.IdActividad},{b.Asignacion.IdAsignacion})",
            $"INSERT INTO trabajadores_cuadrilla(id_asignacion,id_cuadrilla_obra,fecha_inicio) VALUES({b.Asignacion.IdAsignacion},{cuadrillaObra.IdCuadrillaObra},CURRENT_DATE)",
            $"INSERT INTO actividades_incidencia(id_actividad,id_incidencia) VALUES({actividadA.IdActividad},{incidenciaB.IdIncidencia})",
            $"INSERT INTO actas_avance(id_acta,id_avance) VALUES({actaA.IdActa},{avanceB.IdAvance})",
            $"INSERT INTO actas_incidencia(id_acta,id_incidencia) VALUES({actaA.IdActa},{incidenciaB.IdIncidencia})",
            $"INSERT INTO trabajadores_acta(id_acta,id_asignacion,tipo_participacion) VALUES({actaA.IdActa},{b.Asignacion.IdAsignacion},'ASISTENTE')",
            $"UPDATE actividades SET id_obra={b.Obra.IdObra} WHERE id_actividad={actividadA.IdActividad}",
            $"UPDATE actas SET id_obra={b.Obra.IdObra} WHERE id_acta={actaA.IdActa}",
            $"UPDATE asignaciones_trabajador_obra SET id_obra={b.Obra.IdObra} WHERE id_asignacion={a.Asignacion.IdAsignacion}",
            $"UPDATE cuadrillas_obra SET id_obra={b.Obra.IdObra} WHERE id_cuadrilla_obra={cuadrillaObra.IdCuadrillaObra}",
            $"UPDATE incidencias SET id_obra={b.Obra.IdObra} WHERE id_incidencia={incidenciaA.IdIncidencia}"
        };
        foreach (var sql in sentencias)
        {
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => entorno.EjecutarSqlAsync(sql));
            Assert.Equal("23514", error.SqlState);
        }
        Assert.Equal(a.Obra.IdObra, await entorno.EnBaseAsync(c => c.Actividades.Where(x => x.IdActividad == actividadA.IdActividad).Select(x => x.IdObra).SingleAsync()));
    }

    [Fact]
    public async Task InicializacionConcurrenteCreaSoloUnAdministradorYNoRestableceLaSeguridad()
    {
        await using var entorno = await CrearAsync(cluster, iniciarSesion: false, inicializar: false);
        async Task<bool> Inicializar(string usuario)
        {
            await using var alcance = entorno.Fabrica.Services.CreateAsyncScope();
            return (await alcance.ServiceProvider.GetRequiredService<IInicializadorSeguridad>()
                .CrearAdministradorInicialAsync(usuario, Contrasena)).EsExitoso;
        }
        var resultados = await Task.WhenAll(Inicializar("primero"), Inicializar("segundo"));
        Assert.Single(resultados, r => r);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Usuarios.CountAsync()));
        Assert.False(await Inicializar("tercero"));
    }

    [Fact]
    public async Task CsrfEsObligatorioYElTokenAnonimoNoSirveDespuesDeAutenticarse()
    {
        await using var entorno = await CrearAsync(cluster);
        using var cliente = entorno.Fabrica.CreateClient();
        var tokenAnonimo = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        using var iniciar = new HttpRequestMessage(HttpMethod.Post, "/api/sesion/iniciar")
        {
            Content = JsonContent.Create(new SolicitudIniciarSesion(NombreAdministrador, Contrasena))
        };
        iniciar.Headers.Add(tokenAnonimo.Cabecera, tokenAnonimo.Token);
        using var login = await cliente.SendAsync(iniciar);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), v => v.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var hoy = CalendarioPruebas.Hoy;
        var obra = new SolicitudCrearObra("CSRF", "Obra", null, "Dirección", hoy, hoy,
            Latitud: 12m, Longitud: -86m, RadioAsistenciaMetros: 100m);
        using var sinToken = await cliente.PostAsJsonAsync("/api/obras", obra);
        Assert.Equal(HttpStatusCode.BadRequest, sinToken.StatusCode);
        cliente.DefaultRequestHeaders.Add(tokenAnonimo.Cabecera, tokenAnonimo.Token);
        using var tokenAnterior = await cliente.PostAsJsonAsync("/api/obras", obra);
        Assert.Equal(HttpStatusCode.BadRequest, tokenAnterior.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Obras.CountAsync()));
        cliente.DefaultRequestHeaders.Remove(tokenAnonimo.Cabecera);
        var token = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
        using var valida = await cliente.PostAsJsonAsync("/api/obras", obra);
        Assert.Equal(HttpStatusCode.Created, valida.StatusCode);
    }

    [Theory]
    [InlineData("""{"codigo":"A","nombre":null,"direccion":"B","fechaInicioPlan":"2026-10-28","fechaFinPlan":"2026-10-28"}""")]
    [InlineData("""{"codigo":"A","nombre":"B","direccion":"C","fechaInicioPlan":"2026-10-28","fechaFinPlan":"2026-10-28","campoInventado":true}""")]
    [InlineData("""{"codigo":"A","nombre":"B","direccion":"C"}""")]
    [InlineData("""{"codigo":""")]
    public async Task ContratosInvalidosDevuelven400SinEfectosSecundarios(string json)
    {
        await using var entorno = await CrearAsync(cluster);
        using var respuesta = await entorno.Cliente.PostAsync("/api/obras", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Obras.CountAsync()));
    }

    [Fact]
    public async Task InicioSesionLimitaIntentosYNoRevelaSiLaCuentaExiste()
    {
        await using var entorno = await CrearAsync(cluster, iniciarSesion: false,
            ajustes: new Dictionary<string, string?> { ["Api:IntentosInicioSesionPorMinuto"] = "2" });
        using var inexistente = await entorno.Cliente.PostAsJsonAsync("/api/sesion/iniciar", new SolicitudIniciarSesion("inexistente", "Incorrecta2026!"));
        using var incorrecta = await entorno.Cliente.PostAsJsonAsync("/api/sesion/iniciar", new SolicitudIniciarSesion(NombreAdministrador, "Incorrecta2026!"));
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, incorrecta.StatusCode);
        var primero = await inexistente.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var segundo = await incorrecta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(primero.GetProperty("detail").GetString(), segundo.GetProperty("detail").GetString());
        using var limitada = await entorno.Cliente.PostAsJsonAsync("/api/sesion/iniciar", new SolicitudIniciarSesion(NombreAdministrador, Contrasena));
        Assert.Equal(HttpStatusCode.TooManyRequests, limitada.StatusCode);
        Assert.NotNull(limitada.Headers.RetryAfter);
    }

    [Fact]
    public async Task SesionCaducaTrasTreintaMinutosDeInactividad()
    {
        var tiempo = new TiempoControlado();
        await using var entorno = await CrearAsync(cluster, tiempo);
        tiempo.Avanzar(TimeSpan.FromMinutes(31));
        using var respuesta = await entorno.Cliente.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task RenovarSesionNoSuperaLasOchoHorasAbsolutas()
    {
        var tiempo = new TiempoControlado();
        await using var entorno = await CrearAsync(cluster, tiempo);
        for (var minuto = 20; minuto < 480; minuto += 20)
        {
            tiempo.Avanzar(TimeSpan.FromMinutes(20));
            using var vigente = await entorno.Cliente.GetAsync("/api/sesion/actual");
            Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
        }
        tiempo.Avanzar(TimeSpan.FromMinutes(20));
        using var vencida = await entorno.Cliente.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, vencida.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fecha-ilegible")]
    [InlineData("2099-01-01T00:00:00.0000000Z")]
    public async Task CookieFirmadaConFechaAusenteOInvalidaEsRechazada(string? emitida)
    {
        await using var entorno = await CrearAsync(cluster);
        var usuario = await entorno.EnBaseAsync(c => c.Usuarios.SingleAsync());
        var opciones = entorno.Fabrica.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var reclamos = new List<Claim> { new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()) };
        if (emitida is not null) reclamos.Add(new(ContextoUsuarioHttp.ReclamoEmitidaUtc, emitida));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(reclamos,
            CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            },
            CookieAuthenticationDefaults.AuthenticationScheme);
        using var cliente = entorno.Fabrica.CreateClient(new() { HandleCookies = false });
        cliente.DefaultRequestHeaders.Add("Cookie", opciones.Cookie.Name + "=" + opciones.TicketDataFormat.Protect(ticket));
        using var respuesta = await cliente.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task CambiarContrasenaRevocaLasSesionesExistentes()
    {
        await using var entorno = await CrearAsync(cluster);
        using var otraSesion = ClienteApiPruebas.Crear(entorno.Fabrica);
        await AutenticarAsync(otraSesion, NombreAdministrador, Contrasena);
        var usuario = await entorno.EnBaseAsync(c => c.Usuarios.SingleAsync());
        using var cambio = await entorno.Cliente.PutAsJsonAsync($"/api/usuarios/{usuario.IdUsuario}",
            new SolicitudActualizarUsuario(NombreAdministrador, null, "NuevaClaveDePruebas2026!"));
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        using var revocada = await otraSesion.GetAsync("/api/obras");
        Assert.Equal(HttpStatusCode.Unauthorized, revocada.StatusCode);
        await AutenticarAsync(otraSesion, NombreAdministrador, "NuevaClaveDePruebas2026!");
    }

    [Fact]
    public async Task PermisosRevocadosPersistenDespuesDeConsultarRolesEIniciarSesion()
    {
        await using var entorno = await CrearAsync(cluster);
        var rol = await CrearAsync<RespuestaRol>(entorno.Cliente, "/api/roles",
            new SolicitudCrearRol("CONSULTA_PRUEBA", null, [CodigosPermiso.ConsultarObras]));
        await CrearAsync<RespuestaUsuario>(entorno.Cliente, "/api/usuarios",
            new SolicitudCrearUsuario("consulta", Contrasena, null, [rol.IdRol]));
        using var cliente = ClienteApiPruebas.Crear(entorno.Fabrica);
        await AutenticarAsync(cliente, "consulta", Contrasena);
        using var revocar = await entorno.Cliente.PutAsJsonAsync($"/api/roles/{rol.IdRol}/permisos", new SolicitudAsignarPermisosRol([]));
        Assert.Equal(HttpStatusCode.OK, revocar.StatusCode);
        await entorno.Cliente.GetAsync("/api/roles");
        await AutenticarAsync(cliente, "consulta", Contrasena);
        using var denegada = await cliente.GetAsync("/api/obras");
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.RolesPermiso.CountAsync(p => p.IdRol == rol.IdRol)));
    }

    [Fact]
    public async Task ResidenteSoloAdministraTrabajadoresDeSusObrasYNoGestionaHerramientas()
    {
        await using var entorno = await CrearAsync(cluster);
        var propia = await entorno.CrearEscenarioAsync();
        var ajena = await entorno.CrearEscenarioAsync();
        using var residente = await entorno.CrearUsuarioAsync(NombresRol.IngenieroResidente, propia.Trabajador.IdTrabajador);
        var lista = (await residente.GetFromJsonAsync<List<RespuestaTrabajador>>("/api/personal/trabajadores?soloAsignables=true"))!;
        Assert.DoesNotContain(lista, t => t.IdTrabajador == ajena.Trabajador.IdTrabajador);
        using var cambio = await residente.PatchAsJsonAsync($"/api/personal/trabajadores/{ajena.Trabajador.IdTrabajador}/estado",
            new SolicitudCambiarEstadoTrabajador(EstadosTrabajador.Inactivo));
        Assert.Equal(HttpStatusCode.Forbidden, cambio.StatusCode);
        Assert.Equal(EstadosTrabajador.Activo, await entorno.EnBaseAsync(c => c.Trabajadores.Where(t => t.IdTrabajador == ajena.Trabajador.IdTrabajador).Select(t => t.Estado).SingleAsync()));
        var herramienta = await entorno.CrearHerramientaAsync();
        using var denegada = await residente.PostAsJsonAsync("/api/herramientas/asignaciones",
            new SolicitudAsignarHerramienta(herramienta.IdHerramienta, propia.Asignacion.IdAsignacion, DateTime.UtcNow));
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);
        using var higiene = await entorno.CrearUsuarioAsync(NombresRol.HigieneSeguridad);
        await CrearAsync<RespuestaAsignacionHerramienta>(higiene, "/api/herramientas/asignaciones",
            new SolicitudAsignarHerramienta(herramienta.IdHerramienta, propia.Asignacion.IdAsignacion, DateTime.UtcNow));
    }

    [Fact]
    public async Task PermisoDeAprobacionNoPermiteResolverRegistrosDeUnaObraAjena()
    {
        await using var entorno = await CrearAsync(cluster);
        var propia = await entorno.CrearEscenarioAsync();
        var ajena = await entorno.CrearEscenarioAsync();
        await CrearAsync<RespuestaRol>(entorno.Cliente, "/api/roles", new SolicitudCrearRol(
            "REVISOR_PRUEBAS", "Revisor limitado por obra",
            [CodigosPermiso.AprobarCorreccionAsistencia, CodigosPermiso.AprobarActas]));
        using var revisor = await entorno.CrearUsuarioAsync("REVISOR_PRUEBAS", propia.Trabajador.IdTrabajador);
        var ahora = DateTime.UtcNow.AddHours(-8);
        var hoy = CalendarioPruebas.Fecha(ahora);
        foreach (var escenario in new[] { ajena, propia })
        {
            var asistencia = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias",
                new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion, hoy, ahora, ahora.AddHours(8), 12.1m, -86.2m, 12.1m, -86.2m, PrecisionGpsEntradaMetros: 5m, PrecisionGpsSalidaMetros: 5m));
            var correccion = await CrearAsync<RespuestaCorreccionAsistencia>(entorno.Cliente, "/api/asistencias/correcciones",
                new SolicitudCrearCorreccionAsistencia(asistencia.IdAsistencia, ahora.AddMinutes(-15), null, "Revisión por obra"));
            var actividadRevision = await entorno.CrearActividadAsync(escenario);
            await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
                new SolicitudRegistrarAvance(actividadRevision.IdActividad, hoy, 1m));
            var acta = await CrearAsync<RespuestaActa>(entorno.Cliente, "/api/seguimiento/actas",
                new SolicitudElaborarActa(escenario.Obra.IdObra, "ACTA-REVISION", hoy, hoy, EnviarARevision: true));
            using var decisionAsistencia = await revisor.PatchAsJsonAsync($"/api/asistencias/correcciones/{correccion.IdCorreccion}/resolucion",
                new SolicitudResolverCorreccionAsistencia(EstadosCorreccionAsistencia.Aprobada));
            using var decisionActa = await revisor.PatchAsJsonAsync($"/api/seguimiento/actas/{acta.IdActa}/resolucion",
                new SolicitudResolverActa(DecisionesActa.Aprobada));
            var esperado = escenario == propia ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            Assert.Equal(esperado, decisionAsistencia.StatusCode);
            Assert.Equal(esperado, decisionActa.StatusCode);
            if (escenario == ajena)
            {
                Assert.Equal(EstadosCorreccionAsistencia.Pendiente, await entorno.EnBaseAsync(c => c.CorreccionesAsistencia
                    .Where(x => x.IdCorreccion == correccion.IdCorreccion).Select(x => x.Estado).SingleAsync()));
                Assert.Empty(await entorno.EnBaseAsync(c => c.AprobacionesActa.Where(x => x.IdActa == acta.IdActa).ToListAsync()));
            }
        }
    }

    [Fact]
    public async Task MarcacionPorCuadrillaEsAtomicaSiUnoDeLosTrabajadoresEsInvalido()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var auditorias = await entorno.EnBaseAsync(c => c.Auditorias.CountAsync());
        var ahora = DateTime.UtcNow.AddHours(-8);
        using var respuesta = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/cuadrilla",
            new SolicitudRegistrarAsistenciaCuadrilla(escenario.Obra.IdObra, CalendarioPruebas.Fecha(ahora), 12.1m, -86.2m,
                [new(escenario.Asignacion.IdAsignacion, ahora), new(int.MaxValue, ahora)], PrecisionGpsMetros: 5m));
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        Assert.Equal(auditorias, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync()));
    }

    [Fact]
    public async Task ReintentosConcurrentesConElMismoUuidSoloCreanUnaAsistencia()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var ahora = DateTime.UtcNow.AddHours(-8);
        var uuid = Guid.NewGuid();
        var solicitud = new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(ahora), ahora, LatitudEntrada: 12.1m, LongitudEntrada: -86.2m, UuidRegistro: uuid, PrecisionGpsEntradaMetros: 5m);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => entorno.Cliente.PostAsJsonAsync("/api/asistencias", solicitud)));
        try
        {
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
            Assert.All(respuestas, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
            Assert.Equal(1, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync(a => a.UuidRegistro == uuid)));
            Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.UuidOperacion == uuid)));
            using var alterada = await entorno.Cliente.PostAsJsonAsync("/api/asistencias", solicitud with { EntradaEn = ahora.AddMinutes(1) });
            Assert.Equal(HttpStatusCode.Conflict, alterada.StatusCode);
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnEquipoNoSeEntregaDosVecesBajoConcurrencia(bool esEpp)
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        string ruta;
        Func<object> solicitud;
        if (esEpp)
        {
            var epp = await entorno.CrearEppAsync();
            ruta = "/api/epp/entregas";
            solicitud = () => new SolicitudRegistrarEntregaEpp(epp.IdEpp, escenario.Asignacion.IdAsignacion, DateTime.UtcNow, UuidRegistro: Guid.NewGuid());
        }
        else
        {
            var herramienta = await entorno.CrearHerramientaAsync();
            ruta = "/api/herramientas/asignaciones";
            solicitud = () => new SolicitudAsignarHerramienta(herramienta.IdHerramienta, escenario.Asignacion.IdAsignacion, DateTime.UtcNow, UuidRegistro: Guid.NewGuid());
        }
        var respuestas = await Task.WhenAll(entorno.Cliente.PostAsJsonAsync(ruta, solicitud()), entorno.Cliente.PostAsJsonAsync(ruta, solicitud()));
        try
        {
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, esEpp
                ? await entorno.EnBaseAsync(c => c.EntregasEpp.CountAsync(e => e.Estado == EstadosEntregaEpp.Activa))
                : await entorno.EnBaseAsync(c => c.AsignacionesHerramienta.CountAsync(e => e.Estado == EstadosAsignacionHerramienta.Activa)));
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Fact]
    public async Task LoteRevierteSoloLaOperacionQueFallaEnLaBaseYContinuaConLaSiguiente()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var fallo = Guid.NewGuid();
        var correcto = Guid.NewGuid();
        await entorno.EjecutarSqlAsync($"""
            CREATE FUNCTION public.fallar_auditoria_prueba() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.uuid_operacion = '{fallo}'::uuid THEN RAISE EXCEPTION 'fallo de prueba' USING ERRCODE = '23514'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fallo_auditoria_prueba BEFORE INSERT ON public.auditorias FOR EACH ROW EXECUTE FUNCTION public.fallar_auditoria_prueba();
            """);
        var ahora = DateTime.UtcNow;
        var lote = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Asistencias: [
                new(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(ahora), ahora, LatitudEntrada: 12.1m, LongitudEntrada: -86.2m, UuidRegistro: fallo, PrecisionGpsEntradaMetros: 5m),
                new(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(ahora), ahora.AddMinutes(1), LatitudEntrada: 12.1m, LongitudEntrada: -86.2m, UuidRegistro: correcto, PrecisionGpsEntradaMetros: 5m)]));
        Assert.Equal(1, lote.TotalConfirmadas);
        Assert.Equal(1, lote.TotalRechazadas);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync(a => a.UuidRegistro == fallo)));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync(a => a.UuidRegistro == correcto)));
    }

    [Fact]
    public async Task EvidenciaSeDescargaPorObraYSeLimpiaElArchivoSiLaTransaccionFalla()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var ajeno = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var avance = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(actividad.IdActividad, CalendarioPruebas.Hoy, 10m));
        var fallido = Guid.NewGuid();
        await entorno.EjecutarSqlAsync($"""
            CREATE FUNCTION public.fallar_evidencia_prueba() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.uuid_operacion = '{fallido}'::uuid THEN RAISE EXCEPTION 'fallo de prueba' USING ERRCODE = '23514'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fallo_evidencia_prueba BEFORE INSERT ON public.auditorias FOR EACH ROW EXECUTE FUNCTION public.fallar_evidencia_prueba();
            """);
        var solicitud = new SolicitudRegistrarEvidencia(avance.IdAvance, TiposEvidencia.Fotografia,
            NombreArchivo: "prueba.png", ContenidoArchivo: FotoPng, CapturadaEn: DateTime.UtcNow,
            Latitud: 12.1m, Longitud: -86.2m, UuidRegistro: fallido);
        using var fallo = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias", solicitud);
        Assert.Equal(HttpStatusCode.Conflict, fallo.StatusCode);
        Assert.Empty(Directory.GetFiles(entorno.CarpetaEvidencias, "*", SearchOption.AllDirectories));
        var evidencia = await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias", solicitud with { UuidRegistro = Guid.NewGuid() });
        using var archivo = await entorno.Cliente.GetAsync($"/api/seguimiento/evidencias/{evidencia.IdEvidencia}/archivo");
        Assert.Equal(FotoPng, await archivo.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", archivo.Content.Headers.ContentDisposition?.DispositionType);
        using var residenteAjeno = await entorno.CrearUsuarioAsync(NombresRol.IngenieroResidente, ajeno.Trabajador.IdTrabajador);
        using var denegada = await residenteAjeno.GetAsync($"/api/seguimiento/evidencias/{evidencia.IdEvidencia}/archivo");
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);
        using var formatoInvalido = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias",
            solicitud with { UuidRegistro = Guid.NewGuid(), ContenidoArchivo = "contenido que no es PNG"u8.ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, formatoInvalido.StatusCode);
    }

    [Fact]
    public async Task ReposicionPendienteConservaInventarioYLaEjecucionEsIdempotente()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var original = await entorno.CrearEppAsync();
        var sustituto = await entorno.CrearEppAsync(original.IdTipoEpp);
        var entrega = await CrearAsync<RespuestaEntregaEpp>(entorno.Cliente, "/api/epp/entregas",
            new SolicitudRegistrarEntregaEpp(original.IdEpp, escenario.Asignacion.IdAsignacion, DateTime.UtcNow));
        var pendiente = await CrearAsync<RespuestaReposicionEpp>(entorno.Cliente, "/api/epp/reposiciones",
            new SolicitudRegistrarReposicionEpp(entrega.IdEntregaEpp, null, MotivosReposicionEpp.Dano, "Rotura", true));
        Assert.Equal(EstadosReposicionEpp.Pendiente, pendiente.Estado);
        Assert.Equal(EstadosEpp.Entregado, await entorno.EnBaseAsync(c => c.EquiposProteccionPersonal.Where(e => e.IdEpp == original.IdEpp).Select(e => e.Estado).SingleAsync()));
        var ejecutar = new SolicitudEjecutarReposicionEpp(sustituto.IdEpp, true);
        var primera = await CrearAsync<RespuestaReposicionEpp>(entorno.Cliente, $"/api/epp/reposiciones/{pendiente.IdReposicionEpp}/ejecutar", ejecutar);
        var repetida = await CrearAsync<RespuestaReposicionEpp>(entorno.Cliente, $"/api/epp/reposiciones/{pendiente.IdReposicionEpp}/ejecutar", ejecutar);
        Assert.Equal(EstadosReposicionEpp.Ejecutada, primera.Estado);
        Assert.True(repetida.FueIdempotente);
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.EntregasEpp.CountAsync()));
    }

    [Theory]
    [InlineData("DANO", true, "EN_REVISION", "DEVUELTA")]
    [InlineData("PERDIDA", true, "PERDIDA", "PERDIDA")]
    [InlineData("DEMORA", false, "ASIGNADA", "ACTIVA")]
    [InlineData("DEVOLUCION", true, "EN_REVISION", "DEVUELTA")]
    public async Task EventosDeHerramientaMantienenEstadosConsistentes(string tipo, bool cerrar, string estadoHerramienta, string estadoAsignacion)
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var herramienta = await entorno.CrearHerramientaAsync();
        var ahora = DateTime.UtcNow;
        var asignacion = await CrearAsync<RespuestaAsignacionHerramienta>(entorno.Cliente, "/api/herramientas/asignaciones",
            new SolicitudAsignarHerramienta(herramienta.IdHerramienta, escenario.Asignacion.IdAsignacion, ahora));
        var evento = await CrearAsync<RespuestaIncidenciaHerramienta>(entorno.Cliente, "/api/herramientas/eventos",
            new SolicitudRegistrarEventoHerramienta(asignacion.IdAsignacionHerramienta, tipo, ahora.AddMinutes(1),
                "Evento de prueba", CondicionesHerramienta.Danada, CerrarAsignacion: cerrar));
        Assert.Equal(estadoHerramienta, evento.EstadoHerramientaResultante);
        Assert.Equal(estadoAsignacion, evento.EstadoAsignacionResultante);
        if (tipo == TiposIncidenciaHerramienta.Dano)
            Assert.Equal(CondicionesHerramienta.Danada, await entorno.EnBaseAsync(c => c.AsignacionesHerramienta.Select(a => a.CondicionDevolucion).SingleAsync()));
    }

    [Fact]
    public async Task TurnoNocturnoSalidaYCorreccionesNoSobrescribenUnaJornadaYaCorregida()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var hoy = CalendarioPruebas.Hoy.AddDays(-2);
        var entrada = CalendarioPruebas.InstanteUtc(hoy, new TimeOnly(22, 0));
        var asistencia = await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias",
            new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion, hoy, entrada, LatitudEntrada: 12.1m, LongitudEntrada: -86.2m, PrecisionGpsEntradaMetros: 5m));
        using var salidaTurno = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/{asistencia.IdAsistencia}/salida",
            new SolicitudRegistrarSalidaAsistencia(entrada.AddHours(8), 12.1m, -86.2m, PrecisionGpsSalidaMetros: 5m));
        var cerrada = (await salidaTurno.Content.ReadFromJsonAsync<RespuestaAsistencia>())!;
        Assert.Equal(EstadosAsistencia.Completa, cerrada.Estado);
        var correccion1 = await CrearAsync<RespuestaCorreccionAsistencia>(entorno.Cliente, "/api/asistencias/correcciones",
            new SolicitudCrearCorreccionAsistencia(asistencia.IdAsistencia, entrada.AddMinutes(-15), null, "Ajuste autorizado"));
        var correccion2 = await CrearAsync<RespuestaCorreccionAsistencia>(entorno.Cliente, "/api/asistencias/correcciones",
            new SolicitudCrearCorreccionAsistencia(asistencia.IdAsistencia, entrada.AddMinutes(-30), null, "Otro ajuste"));
        using var aprobada = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/correcciones/{correccion1.IdCorreccion}/resolucion",
            new SolicitudResolverCorreccionAsistencia(EstadosCorreccionAsistencia.Aprobada));
        Assert.Equal(HttpStatusCode.OK, aprobada.StatusCode);
        using var obsoleta = await entorno.Cliente.PatchAsJsonAsync($"/api/asistencias/correcciones/{correccion2.IdCorreccion}/resolucion",
            new SolicitudResolverCorreccionAsistencia(EstadosCorreccionAsistencia.Aprobada));
        Assert.Equal(HttpStatusCode.Conflict, obsoleta.StatusCode);
        Assert.Equal(EstadosCorreccionAsistencia.Pendiente, await entorno.EnBaseAsync(c => c.CorreccionesAsistencia.Where(a => a.IdCorreccion == correccion2.IdCorreccion).Select(a => a.Estado).SingleAsync()));
    }

    [Fact]
    public async Task ConsultasPaginadasYConsolidadoMantienenTotalesBajoCargaConcurrente()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        await entorno.EjecutarSqlAsync($"""
            INSERT INTO asistencias(id_asignacion, fecha_jornada, entrada_at, salida_at, estado, id_usuario_registro, uuid_registro)
            SELECT {escenario.Asignacion.IdAsignacion}, CURRENT_DATE, CURRENT_TIMESTAMP + (i || ' minutes')::interval,
                CURRENT_TIMESTAMP + (i || ' minutes')::interval + interval '8 hours', 'COMPLETA',
                (SELECT id_usuario FROM usuarios LIMIT 1), gen_random_uuid() FROM generate_series(1, 2000) i;
            """);
        var primera = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaAsistencia>>("/api/asistencias?tamanoPagina=7"))!;
        var segunda = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaAsistencia>>("/api/asistencias?pagina=2&tamanoPagina=7"))!;
        Assert.Equal(7, primera.Count);
        Assert.Equal(7, segunda.Count);
        Assert.Empty(primera.Select(a => a.IdAsistencia).Intersect(segunda.Select(a => a.IdAsistencia)));
        var consolidado = (await entorno.Cliente.GetFromJsonAsync<List<ItemConsolidadoAsistencia>>("/api/asistencias/consolidado"))!;
        Assert.Equal(2000, Assert.Single(consolidado).TotalJornadasRegistradas);
        Assert.Equal(16000m, consolidado[0].HorasTrabajadasTotales);
        using var excesiva = await entorno.Cliente.GetAsync("/api/asistencias?tamanoPagina=100000");
        Assert.Equal(HttpStatusCode.BadRequest, excesiva.StatusCode);
        foreach (var ruta in new[] { "/api/obras", "/api/asistencias/consolidado", "/api/epp/reportes" })
        {
            var tiempos = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
            {
                var cronometro = Stopwatch.StartNew();
                using var respuesta = await entorno.Cliente.GetAsync(ruta);
                Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                return cronometro.Elapsed.TotalMilliseconds;
            }));
            var p95 = tiempos.Order().ElementAt((int)Math.Ceiling(tiempos.Length * .95) - 1);
            salida.WriteLine($"{ruta}: 25 solicitudes concurrentes, p95={p95:F2} ms, máximo={tiempos.Max():F2} ms.");
            Assert.True(p95 < (ruta == "/api/obras" ? 3000 : 5000), $"Latencia p95 excesiva en {ruta}: {p95:F2} ms");
        }
    }
}
