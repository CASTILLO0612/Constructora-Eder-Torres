using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class FabricaApi : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Pruebas");
        constructor.ConfigureAppConfiguration((_, configuracion) =>
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Constructora"] = string.Empty
            }));
    }

    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}
