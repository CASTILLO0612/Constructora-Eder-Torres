using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class VerificacionCorreccionesApiPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private static async Task ErrorAsync(HttpResponseMessage respuesta, string codigo)
    {
        using (respuesta)
        {
            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
            using var cuerpo = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync());
            Assert.Equal(codigo, cuerpo.RootElement.GetProperty("codigo").GetString());
        }
    }

    private static Task<RespuestaAvance> RegistrarAsync(EntornoApiPruebas entorno, SolicitudRegistrarAvance solicitud) =>
        CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances", solicitud);

    private static SolicitudRegistrarEvidencia Foto(EntornoApiPruebas entorno, long idAvance) =>
        new(idAvance, TiposEvidencia.Fotografia, "foto.png", FotoPng, entorno.AhoraUtc,
            12m, -86m, Guid.NewGuid());

    [Theory]
    [InlineData(EstadosObra.Planificada)]
    [InlineData(EstadosObra.Suspendida)]
    [InlineData(EstadosObra.Finalizada)]
    public async Task EvidenciasNuevasExigenObraActivaSinImpedirLecturaOReenvioConfirmado(string estado)
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var avance = await RegistrarAsync(entorno, new(actividad.IdActividad, entorno.HoyLaboral, 1m));
        var captura = Foto(entorno, avance.IdAvance);
        var evidencia = await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias", captura);
        if (estado == EstadosObra.Planificada)
        {
            // Estado histórico inconsistente posible en una base importada; no se habilita esa transición por API.
            await entorno.EnBaseAsync(c => c.Obras.Where(o => o.IdObra == escenario.Obra.IdObra)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(o => o.Estado, estado)));
        }
        else
        {
            using var cambio = await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{escenario.Obra.IdObra}/estado",
                new SolicitudCambiarEstadoObra(estado));
            cambio.EnsureSuccessStatusCode();
        }

        var nueva = captura with { UuidRegistro = Guid.NewGuid() };
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias", nueva), "obra_no_activa");
        var lote = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Evidencias: [captura, nueva]));
        Assert.Equal(1, lote.TotalIdempotentes);
        Assert.Equal(1, lote.TotalRechazadas);
        Assert.Contains(lote.Acuses, a => a.CodigoError == "obra_no_activa");
        using var descarga = await entorno.Cliente.GetAsync($"/api/seguimiento/evidencias/{evidencia.IdEvidencia}/archivo");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal(FotoPng, await descarga.Content.ReadAsByteArrayAsync());
        Assert.Single(Directory.GetFiles(entorno.CarpetaEvidencias, "*", SearchOption.AllDirectories));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "evidencias")));
    }

    [Fact]
    public async Task CuentaTrabajadorNoRecibePermisoParaAdjuntarEvidencias()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var avance = await RegistrarAsync(entorno, new(actividad.IdActividad, entorno.HoyLaboral, 1m));
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        using var respuesta = await trabajador.PostAsJsonAsync("/api/seguimiento/evidencias", Foto(entorno, avance.IdAvance));
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
    }

    [Fact]
    public async Task BorradorSeConfirmaConElMismoIdYUuidConAuditoriaSinDuplicarElDia()
    {
        await using var entorno = await CrearAsync(cluster);
        var actividad = await entorno.CrearActividadAsync(await entorno.CrearEscenarioAsync());
        var captura = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 1m,
            "Dato preliminar", EstadosAvance.Borrador, UuidRegistro: Guid.NewGuid());
        var borrador = await RegistrarAsync(entorno, captura);
        Assert.True((await RegistrarAsync(entorno, captura)).FueIdempotente);
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances",
            captura with { Estado = EstadosAvance.Registrado, UuidRegistro = Guid.NewGuid() }), "avance_dia_duplicado");
        var confirmacion = captura with
        {
            Estado = EstadosAvance.Registrado,
            CantidadEjecutada = 30m,
            Observaciones = "Cantidad contrastada en campo"
        };
        using var respuesta = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", confirmacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var registrado = (await respuesta.Content.ReadFromJsonAsync<RespuestaAvance>())!;
        Assert.DoesNotContain("fueConfirmacionBorrador", await respuesta.Content.ReadAsStringAsync());
        using var documentacionOculta = await entorno.Cliente.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, documentacionOculta.StatusCode);
        Assert.False(registrado.FueIdempotente);
        Assert.Equal(borrador.IdAvance, registrado.IdAvance);
        Assert.Equal(borrador.UuidRegistro, registrado.UuidRegistro);
        Assert.Equal(borrador.CreadoEn, registrado.CreadoEn);
        Assert.Equal(EstadosAvance.Registrado, registrado.Estado);
        Assert.True((await RegistrarAsync(entorno, confirmacion)).FueIdempotente);
        var ejecutada = await entorno.Cliente.GetFromJsonAsync<RespuestaActividad>($"/api/seguimiento/actividades/{actividad.IdActividad}");
        Assert.Equal(30m, ejecutada!.CantidadEjecutadaAcumulada);
        Assert.Equal(EstadosActividad.EnProceso, ejecutada.Estado);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Avances.CountAsync()));
        var auditoria = await entorno.EnBaseAsync(c => c.Auditorias.SingleAsync(
            a => a.Entidad == "avances" && a.Accion == AccionesAuditoria.Modificar));
        Assert.Contains("BORRADOR", auditoria.ValorAnterior);
        Assert.Contains("cantidad=1.00", auditoria.ValorAnterior);
        Assert.Contains("cantidad=30.00", auditoria.ValorNuevo);
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", confirmacion with { CantidadEjecutada = 99m }), "uuid_reutilizado");
    }

    [Fact]
    public async Task SincronizacionConfirmaBorradorYRevalidaObraSinPerderElRegistroPendiente()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var captura = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 20m,
            Estado: EstadosAvance.Borrador, UuidRegistro: Guid.NewGuid());
        var borrador = await RegistrarAsync(entorno, captura);
        using (var suspension = await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{escenario.Obra.IdObra}/estado",
            new SolicitudCambiarEstadoObra(EstadosObra.Suspendida))) suspension.EnsureSuccessStatusCode();
        var confirmacion = captura with { Estado = EstadosAvance.Registrado };
        var rechazado = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Avances: [confirmacion]));
        Assert.Equal("obra_no_activa", Assert.Single(rechazado.Acuses).CodigoError);
        Assert.Equal(EstadosAvance.Borrador, await entorno.EnBaseAsync(c => c.Avances.Select(a => a.Estado).SingleAsync()));
        using (var reanudacion = await entorno.Cliente.PatchAsJsonAsync($"/api/obras/{escenario.Obra.IdObra}/estado",
            new SolicitudCambiarEstadoObra(EstadosObra.Activa))) reanudacion.EnsureSuccessStatusCode();
        var aceptado = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Avances: [confirmacion]));
        Assert.Equal(0, aceptado.TotalRechazadas);
        Assert.Equal(0, aceptado.TotalIdempotentes);
        var repetido = await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Avances: [confirmacion]));
        Assert.Equal(1, repetido.TotalIdempotentes);
        Assert.Equal(borrador.IdAvance, await entorno.EnBaseAsync(c => c.Avances.Select(a => a.IdAvance).SingleAsync()));
        Assert.NotNull(await entorno.EnBaseAsync(c => c.Avances.Select(a => a.SincronizadoEn).SingleAsync()));
    }

    [Fact]
    public async Task DosConfirmacionesConcurrentesAplicanElProgresoUnaSolaVez()
    {
        await using var entorno = await CrearAsync(cluster);
        var actividad = await entorno.CrearActividadAsync(await entorno.CrearEscenarioAsync());
        var captura = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 10m,
            Estado: EstadosAvance.Borrador, UuidRegistro: Guid.NewGuid());
        await RegistrarAsync(entorno, captura);
        using var otro = ClienteApiPruebas.Crear(entorno.Fabrica);
        await AutenticarAsync(otro, NombreAdministrador, Contrasena);
        var confirmacion = captura with { Estado = EstadosAvance.Registrado };
        var resultados = await Task.WhenAll(RegistrarAsync(entorno, confirmacion),
            CrearAsync<RespuestaAvance>(otro, "/api/seguimiento/avances", confirmacion));
        Assert.Single(resultados, r => !r.FueIdempotente);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Avances.CountAsync()));
        var ejecutada = await entorno.Cliente.GetFromJsonAsync<RespuestaActividad>($"/api/seguimiento/actividades/{actividad.IdActividad}");
        Assert.Equal(10m, ejecutada!.CantidadEjecutadaAcumulada);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(
            a => a.Entidad == "avances" && a.Accion == AccionesAuditoria.Modificar)));
    }

    [Fact]
    public async Task FalloDeAuditoriaConservaBorradorYEstadoAnteriorHastaReintentar()
    {
        await using var entorno = await CrearAsync(cluster);
        var actividad = await entorno.CrearActividadAsync(await entorno.CrearEscenarioAsync());
        var captura = new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 10m,
            Estado: EstadosAvance.Borrador, UuidRegistro: Guid.NewGuid());
        await RegistrarAsync(entorno, captura);
        await entorno.EjecutarSqlAsync("""
            CREATE FUNCTION impedir_confirmacion() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.entidad = 'avances' AND NEW.accion = 'MODIFICAR' THEN
                    RAISE EXCEPTION 'Fallo controlado al confirmar';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER impedir_confirmacion BEFORE INSERT ON auditorias
                FOR EACH ROW EXECUTE FUNCTION impedir_confirmacion();
            """);
        var confirmacion = captura with { Estado = EstadosAvance.Registrado, CantidadEjecutada = 40m };
        await ErrorAsync(await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/avances", confirmacion), "regla_datos_incumplida");
        Assert.Equal(EstadosAvance.Borrador, await entorno.EnBaseAsync(c => c.Avances.Select(a => a.Estado).SingleAsync()));
        Assert.Equal(10m, await entorno.EnBaseAsync(c => c.Avances.Select(a => a.CantidadEjecutada).SingleAsync()));
        Assert.Equal(EstadosActividad.Planificada, await entorno.EnBaseAsync(c => c.Actividades.Select(a => a.Estado).SingleAsync()));
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Accion == AccionesAuditoria.Modificar)));
        await entorno.EjecutarSqlAsync("DROP TRIGGER impedir_confirmacion ON auditorias; DROP FUNCTION impedir_confirmacion();");
        Assert.Equal(EstadosAvance.Registrado, (await RegistrarAsync(entorno, confirmacion)).Estado);
    }

    [Fact]
    public async Task FalloAlGuardarBloqueoRevierteTambienLaRevocacionDeSesiones()
    {
        await using var entorno = await CrearAsync(cluster);
        using var usuario = await entorno.CrearUsuarioAsync(NombresRol.Administrativo);
        var perfil = (await usuario.GetFromJsonAsync<RespuestaSesionActiva>("/api/sesion/actual"))!;
        await entorno.EjecutarSqlAsync("""
            CREATE FUNCTION impedir_bloqueo() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.entidad = 'usuarios' AND NEW.accion = 'MODIFICAR' THEN
                    RAISE EXCEPTION 'Fallo controlado al guardar bloqueo';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER impedir_bloqueo BEFORE INSERT ON auditorias
                FOR EACH ROW EXECUTE FUNCTION impedir_bloqueo();
            """);
        await ErrorAsync(await entorno.Cliente.PatchAsJsonAsync($"/api/usuarios/{perfil.IdUsuario}/estado",
            new SolicitudCambiarEstadoUsuario(EstadosUsuario.Bloqueado)), "regla_datos_incumplida");
        Assert.Equal(EstadosUsuario.Activo, await entorno.EnBaseAsync(c => c.Usuarios
            .Where(u => u.IdUsuario == perfil.IdUsuario).Select(u => u.Estado).SingleAsync()));
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.SesionesUsuario.CountAsync(
            s => s.IdUsuario == perfil.IdUsuario && s.RevocadaEnUtc != null)));
        using var vigente = await usuario.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
    }

    [Fact]
    public async Task DepuracionSoloRetiraSesionesVencidasPorLotesSinBorrarAuditoriaNiCuentas()
    {
        await using var entorno = await CrearAsync(cluster);
        var ahora = DateTime.UtcNow;
        var ids = await entorno.EnBaseAsync(async c =>
        {
            var idUsuario = await c.Usuarios.Select(u => u.IdUsuario).SingleAsync();
            var sesiones = Enumerable.Range(0, 5).Select(i => new SesionUsuario
            {
                IdSesion = Guid.NewGuid(),
                IdUsuario = idUsuario,
                EmitidaEnUtc = ahora.AddHours(-4),
                ExpiraEnUtc = i < 3 ? ahora.AddHours(-1) : ahora.AddHours(4),
                RevocadaEnUtc = i == 4 ? ahora.AddHours(-2) : null
            }).ToArray();
            c.SesionesUsuario.AddRange(sesiones);
            await c.SaveChangesAsync();
            return sesiones.Select(s => s.IdSesion).ToArray();
        });
        var auditorias = await entorno.EnBaseAsync(c => c.Auditorias.CountAsync());
        var archivo = Path.Combine(ProcesoPostgreSqlPruebas.RaizRepositorio(), "Tools", "Operacion", "DepurarSesionesVencidas.sql");
        await ProcesoPostgreSqlPruebas.EjecutarPsqlAsync(cluster.BinariosPostgreSql, entorno.Conexion, archivo,
            new Dictionary<string, string> { ["limite_sesiones"] = "2" });
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.SesionesUsuario.CountAsync(s => ids.Take(3).Contains(s.IdSesion))));
        await ProcesoPostgreSqlPruebas.EjecutarPsqlAsync(cluster.BinariosPostgreSql, entorno.Conexion, archivo,
            new Dictionary<string, string> { ["limite_sesiones"] = "2" });
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.SesionesUsuario.CountAsync(s => ids.Take(3).Contains(s.IdSesion))));
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.SesionesUsuario.CountAsync(s => ids.Skip(3).Contains(s.IdSesion))));
        Assert.Equal(auditorias, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync()));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Usuarios.CountAsync()));
        using var vigente = await entorno.Cliente.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
    }
}
