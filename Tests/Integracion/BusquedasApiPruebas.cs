using System.Diagnostics;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Xunit.Abstractions;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

[Collection(ColeccionPruebasRendimiento.Nombre)]
public sealed class BusquedasApiPruebas(ClusterPostgreSqlDesechableFixture cluster, ITestOutputHelper salida)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task BuscaAntesDePaginarYReconoceTildesApellidosYComodinesLiterales()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        await entorno.EjecutarSqlAsync($"""
            INSERT INTO trabajadores(id_cargo, cedula, primer_nombre, primer_apellido, fecha_ingreso, estado)
            SELECT {escenario.Trabajador.IdCargo}, 'BUS-' || i, 'Coincidente', 'Apellido', CURRENT_DATE, 'ACTIVO'
            FROM generate_series(1, 160) i;
            INSERT INTO trabajadores(id_cargo, cedula, primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, fecha_ingreso, estado)
            VALUES ({escenario.Trabajador.IdCargo}, 'UNICO-BUS', 'José', 'Ángel', 'Núñez', 'Pérez', CURRENT_DATE, 'ACTIVO');
            """);
        var inicial = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaTrabajador>>("/api/personal/trabajadores?tamanoPagina=100"))!;
        Assert.DoesNotContain(inicial, t => t.Cedula == "UNICO-BUS");
        var hallados = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaTrabajador>>(
            "/api/personal/trabajadores?buscar=JOSE%20angel%20nunez%20perez&tamanoPagina=25"))!;
        Assert.Equal("UNICO-BUS", Assert.Single(hallados).Cedula);
        var ids = new List<int>();
        for (var pagina = 1; pagina <= 7; pagina++)
        {
            var filas = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaTrabajador>>(
                $"/api/personal/trabajadores?buscar=coincidente&pagina={pagina}&tamanoPagina=25"))!;
            Assert.Equal(pagina < 7 ? 25 : 10, filas.Count);
            ids.AddRange(filas.Select(f => f.IdTrabajador));
        }
        Assert.Equal(160, ids.Distinct().Count());
        var literal = await CrearAsync<RespuestaObra>(entorno.Cliente, "/api/obras", new SolicitudCrearObra(
            "LIT-01", "Bodega 50%_Norte", null, "Dirección", entorno.HoyLaboral, entorno.HoyLaboral,
            Latitud: 12m, Longitud: -86m, RadioAsistenciaMetros: 100m));
        var obras = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaObra>>("/api/obras?buscar=50%25_Norte"))!;
        Assert.Equal(literal.IdObra, Assert.Single(obras).IdObra);
        Assert.Empty((await entorno.Cliente.GetFromJsonAsync<List<RespuestaObra>>("/api/obras?buscar=%27%20OR%201%3D1"))!);
    }

    [Fact]
    public async Task LaBusquedaMantienePermisosAlcanceYFiltrosDeEstado()
    {
        await using var entorno = await CrearAsync(cluster);
        var propia = await entorno.CrearEscenarioAsync();
        var ajena = await entorno.CrearEscenarioAsync();
        using var residente = await entorno.CrearUsuarioAsync(NombresRol.IngenieroResidente, propia.Trabajador.IdTrabajador);
        var obras = (await residente.GetFromJsonAsync<List<RespuestaObra>>("/api/obras?buscar=Obra"))!;
        Assert.Equal(propia.Obra.IdObra, Assert.Single(obras).IdObra);
        Assert.Empty((await residente.GetFromJsonAsync<List<RespuestaObra>>($"/api/obras?buscar={ajena.Obra.Codigo}"))!);
        Assert.Empty((await entorno.Cliente.GetFromJsonAsync<List<RespuestaObra>>("/api/obras?buscar=Obra&estado=PLANIFICADA"))!);
        using var trabajador = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, ajena.Trabajador.IdTrabajador);
        using var denegada = await trabajador.GetAsync("/api/usuarios?buscar=administrador");
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);
        var hash = await entorno.EnBaseAsync(c => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(c.Usuarios));
        Assert.Empty((await entorno.Cliente.GetFromJsonAsync<List<JsonElement>>("/api/usuarios?buscar=" + Uri.EscapeDataString(hash.ContrasenaHash[..50])))!);
    }

    [Fact]
    public async Task TodasLasListasFiltranEnSqlIncluidosJoinsAgrupacionesYAuditoriaPaginada()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var avance = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 1m));
        var obra = escenario.Obra.IdObra;
        var rutas = new[] {
            "/api/obras", "/api/usuarios", "/api/roles", "/api/permisos", "/api/auditorias",
            "/api/personal/trabajadores", "/api/personal/cargos", "/api/personal/especialidades", "/api/personal/cuadrillas",
            $"/api/personal/obras/{obra}/asignaciones", $"/api/personal/obras/{obra}/cuadrillas", $"/api/personal/obras/{obra}/miembros-cuadrilla",
            "/api/asistencias", "/api/asistencias/consolidado", "/api/asistencias/correcciones",
            $"/api/seguimiento/obras/{obra}/actividades", $"/api/seguimiento/obras/{obra}/avances",
            $"/api/seguimiento/obras/{obra}/incidencias", $"/api/seguimiento/obras/{obra}/actas",
            $"/api/seguimiento/avances/{avance.IdAvance}/evidencias",
            "/api/epp/tipos", "/api/epp/existencias", "/api/epp/existencias/resumen", "/api/epp/entregas", "/api/epp/reposiciones",
            $"/api/epp/trabajadores/{escenario.Trabajador.IdTrabajador}/historial", "/api/epp/reportes",
            "/api/herramientas", "/api/herramientas/categorias", "/api/herramientas/tipos", "/api/herramientas/asignaciones", "/api/herramientas/eventos"
        };
        foreach (var ruta in rutas)
        {
            using var respuesta = await entorno.Cliente.GetAsync(ruta + "?buscar=prueba&tamanoPagina=25");
            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"{ruta}: {respuesta.StatusCode} {await respuesta.Content.ReadAsStringAsync()}");
            var filas = (await respuesta.Content.ReadFromJsonAsync<List<JsonElement>>())!;
            Assert.InRange(filas.Count, 0, 25);
        }
        var pagina1 = (await entorno.Cliente.GetFromJsonAsync<List<JsonElement>>("/api/auditorias?tamanoPagina=1&pagina=1"))!;
        var pagina2 = (await entorno.Cliente.GetFromJsonAsync<List<JsonElement>>("/api/auditorias?tamanoPagina=1&pagina=2"))!;
        Assert.NotEqual(Assert.Single(pagina1).GetProperty("idAuditoria").GetInt64(), Assert.Single(pagina2).GetProperty("idAuditoria").GetInt64());
        Assert.Single((await entorno.Cliente.GetFromJsonAsync<List<JsonElement>>("/api/auditorias?limite=1"))!);
        using var propio = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, escenario.Trabajador.IdTrabajador);
        var asignadas = (await propio.GetFromJsonAsync<List<RespuestaAsignacionAsistencia>>("/api/asistencias/mis-asignaciones?buscar=pruebas"))!;
        Assert.Equal(obra, Assert.Single(asignadas).IdObra);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a b c d e f g h i")]
    public async Task AcotaElTextoAntesDeConsultar(string buscar)
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();
        using var respuesta = await cliente.GetAsync("/api/obras?buscar=" + Uri.EscapeDataString(buscar));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task UsaIndiceConVeinteMilObrasYDevuelvePaginasPequenasBajoCarga()
    {
        var captura = new CapturaBusqueda();
        await using var entorno = await CrearAsync(cluster, configurarServicios: servicios =>
            servicios.AddDbContext<ContextoConstructora>(opciones => opciones.AddInterceptors(captura)));
        await entorno.EjecutarSqlAsync("""
            INSERT INTO obras(codigo, nombre, direccion, fecha_inicio_plan, fecha_fin_plan, estado)
            SELECT 'CARGA-' || i, CASE WHEN i = 19990 THEN 'Taller Único Zafiro' ELSE 'Obra industrial ' || i END,
                'Managua', CURRENT_DATE, CURRENT_DATE, 'PLANIFICADA' FROM generate_series(1, 20000) i;
            ANALYZE public.obras;
            """);
        await using var conexion = new NpgsqlConnection(entorno.Conexion);
        await conexion.OpenAsync();
        var ruta = "/api/obras?buscar=zafiro&tamanoPagina=25";
        Assert.Single((await entorno.Cliente.GetFromJsonAsync<List<RespuestaObra>>(ruta))!);
        salida.WriteLine(captura.Sql);
        await using var comando = new NpgsqlCommand("EXPLAIN (ANALYZE, FORMAT JSON) " + captura.Sql, conexion);
        foreach (var parametro in captura.Parametros) comando.Parameters.Add((NpgsqlParameter)((ICloneable)parametro).Clone());
        var plan = (string)(await comando.ExecuteScalarAsync())!;
        salida.WriteLine(plan);
        Assert.Contains("ix_busqueda_obras", plan);
        var tiempos = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            var reloj = Stopwatch.StartNew();
            var resultado = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaObra>>(ruta))!;
            Assert.Equal("CARGA-19990", Assert.Single(resultado).Codigo);
            return reloj.Elapsed.TotalMilliseconds;
        }));
        var p95 = tiempos.Order().ElementAt(23);
        salida.WriteLine($"20 000 obras, índice GIN verificado, 25 búsquedas concurrentes: p95={p95:F2} ms; máximo={tiempos.Max():F2} ms.");
        Assert.True(p95 < 2000, $"p95 fuera del objetivo: {p95:F2} ms.");
    }

    private sealed class CapturaBusqueda : DbCommandInterceptor
    {
        public string Sql { get; private set; } = "";
        public NpgsqlParameter[] Parametros { get; private set; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand comando,
            CommandEventData evento, InterceptionResult<DbDataReader> resultado, CancellationToken cancelacion = default)
        {
            if (comando.CommandText.Contains("cet_texto_busqueda", StringComparison.Ordinal))
            {
                Sql = comando.CommandText;
                Parametros = comando.Parameters.Cast<NpgsqlParameter>()
                    .Select(p => (NpgsqlParameter)((ICloneable)p).Clone()).ToArray();
            }
            return base.ReaderExecutingAsync(comando, evento, resultado, cancelacion);
        }
    }
}
