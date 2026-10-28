using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Salud;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class SaludPruebas(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    [Fact]
    public async Task LaApiRespondeAunqueNoSeHayaConfiguradoPostgreSql()
    {
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.GetAsync("/salud/viva");
        var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaSalud>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(contenido);
        Assert.Equal("saludable", contenido.Estado);
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task LaDisponibilidadNoDeclaraPostgreSqlSaludableSinConfiguracion()
    {
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.GetAsync("/salud/lista");
        var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaSalud>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.NotNull(contenido);
        Assert.Equal("no_disponible", contenido.Estado);
        Assert.Equal("no_disponible", contenido.Componentes["postgresql"]);
    }

    [Fact]
    public async Task OpenApiNoSePublicaFueraDelEntornoDeDesarrollo()
    {
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task OpenApiSeGeneraEnDesarrolloConLosPuntosDeAccesoDeSalud()
    {
        using var servidor = fabrica.WithWebHostBuilder(constructor =>
            constructor.UseEnvironment("Development"));
        using var cliente = servidor.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var respuesta = await cliente.GetAsync("/openapi/v1.json");
        using var contenido = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var rutas = contenido.RootElement.GetProperty("paths");
        Assert.True(rutas.TryGetProperty("/salud/viva", out _));
        Assert.True(rutas.TryGetProperty("/salud/lista", out _));
    }
}
