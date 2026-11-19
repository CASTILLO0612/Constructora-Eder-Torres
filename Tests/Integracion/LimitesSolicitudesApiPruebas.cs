using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Presentacion.Configuracion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class LimitesSolicitudesApiPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private static SolicitudRegistrarAsistencia Entrada(EscenarioObra escenario, DateTime hora) =>
        new(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(hora), hora,
            LatitudEntrada: 12m, LongitudEntrada: -86m, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m);

    [Fact]
    public async Task LimitarUnTrabajadorNoConsumeElCupoDeOtroNiElDePrevalidacion()
    {
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string,string?>
        { ["Api:LimitesSolicitudes:Asistencia:Permisos"] = "2" });
        var primero = await entorno.CrearEscenarioAsync();
        var segundo = await entorno.CrearEscenarioAsync();
        using var clienteA = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, primero.Trabajador.IdTrabajador);
        using var clienteB = await entorno.CrearUsuarioAsync(NombresRol.Trabajador, segundo.Trabajador.IdTrabajador);
        var captura = Entrada(primero, entorno.AhoraUtc.AddMinutes(-1));
        await CrearAsync<RespuestaAsistencia>(clienteA, "/api/asistencias", captura);
        Assert.True((await CrearAsync<RespuestaAsistencia>(clienteA, "/api/asistencias", captura)).FueIdempotente);
        using var limitada = await clienteA.PostAsJsonAsync("/api/asistencias", captura);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitada.StatusCode);
        Assert.True(limitada.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Contains("limite_solicitudes", await limitada.Content.ReadAsStringAsync());
        using var ubicacion = await clienteA.PostAsJsonAsync("/api/asistencias/validar-ubicacion",
            new SolicitudValidarUbicacionAsistencia(primero.Asignacion.IdAsignacion, 12m, -86m, 5m));
        Assert.Equal(HttpStatusCode.OK, ubicacion.StatusCode);
        await CrearAsync<RespuestaAsistencia>(clienteB, "/api/asistencias", Entrada(segundo, entorno.AhoraUtc.AddMinutes(-1)));
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        Assert.Equal(2, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias")));
    }

    [Fact]
    public async Task VentanaAgotadaSeRecuperaYUnaLecturaNoConsumeElCupoDeMarcacion()
    {
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string,string?>
        {
            ["Api:LimitesSolicitudes:Ubicacion:Permisos"] = "1",
            ["Api:LimitesSolicitudes:Ubicacion:Ventana"] = "00:00:01"
        });
        var escenario = await entorno.CrearEscenarioAsync();
        var solicitud = new SolicitudValidarUbicacionAsistencia(escenario.Asignacion.IdAsignacion, 12m, -86m, 5m);
        using var valida = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/validar-ubicacion", solicitud);
        Assert.Equal(HttpStatusCode.OK, valida.StatusCode);
        using var rechazada = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/validar-ubicacion", solicitud);
        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias", Entrada(escenario, entorno.AhoraUtc.AddMinutes(-1)));
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        using var recuperada = await entorno.Cliente.PostAsJsonAsync("/api/asistencias/validar-ubicacion", solicitud);
        Assert.Equal(HttpStatusCode.OK, recuperada.StatusCode);
    }

    [Fact]
    public async Task LoteRechazadoPorCupoNoEjecutaNingunaEscritura()
    {
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string,string?>
        { ["Api:LimitesSolicitudes:Sincronizacion:Permisos"] = "1" });
        var escenario = await entorno.CrearEscenarioAsync();
        var captura = Entrada(escenario, entorno.AhoraUtc.AddMinutes(-1));
        await CrearAsync<RespuestaSincronizacionLote>(entorno.Cliente, "/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Asistencias: [captura]));
        using var rechazada = await entorno.Cliente.PostAsJsonAsync("/api/sincronizacion/lote",
            new SolicitudSincronizacionLote(Asistencias: [captura with { UuidRegistro = Guid.NewGuid() }]));
        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
    }

    [Fact]
    public async Task CupoSimultaneoDeEvidenciasSeLiberaAlTerminarLaSolicitud()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["Api:LimitesSolicitudes:Evidencia:MaximoSimultaneo"] = "1" }).Build());
        servicios.AddSingleton(new OpcionesApi());
        servicios.AgregarLimitesSolicitudes();
        await using var proveedor = servicios.BuildServiceProvider();
        var limitador = proveedor.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/api/seguimiento/evidencias";
        contexto.Request.Method = "POST";
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "prueba")], "pruebas"));
        contexto.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(contexto.Request.Path), 0,
            new EndpointMetadataCollection(new LimiteOperacionApi(PerfilLimiteApi.Evidencia)), "prueba"));
        using (var primera = await limitador.AcquireAsync(contexto))
        {
            Assert.True(primera.IsAcquired);
            using var segunda = await limitador.AcquireAsync(contexto);
            Assert.False(segunda.IsAcquired);
        }
        using var tercera = await limitador.AcquireAsync(contexto);
        Assert.True(tercera.IsAcquired);
        limitador.Dispose();
    }
}
