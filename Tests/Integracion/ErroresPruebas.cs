using System.Net;
using System.Text.Json;
using ConstructoraEderTorres.Presentacion.Configuracion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class ErroresPruebas(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    [Fact]
    public async Task UnaRutaDesconocidaDevuelveUnProblemaConIdentificador()
    {
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.GetAsync("/ruta-inexistente");
        using var contenido = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Recurso no encontrado.", contenido.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            contenido.RootElement.GetProperty("identificador").GetString()));
    }

    [Fact]
    public async Task UnErrorNoControladoNoExponeSuMensajeInterno()
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Pruebas"
        });
        constructor.WebHost.UseTestServer();
        constructor.Services.AgregarPresentacion();

        await using var aplicacion = constructor.Build();
        aplicacion.UseExceptionHandler();
        aplicacion.Run(_ => throw new InvalidOperationException("DATO_INTERNO_NO_PUBLICO"));
        await aplicacion.StartAsync();

        using var cliente = aplicacion.GetTestClient();
        using var respuesta = await cliente.GetAsync("/");
        var contenido = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("DATO_INTERNO_NO_PUBLICO", contenido);
        Assert.Contains("identificador", contenido);
    }
}
