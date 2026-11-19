using System.Net;
using System.Net.Http.Json;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class CabecerasSeguridadPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task RespuestasPublicasProtegidasYErroresImpidenCacheYFraming()
    {
        await using var entorno = await CrearAsync(cluster, iniciarSesion: false);
        foreach (var ruta in new[] { "/salud/lista", "/api/obras", "/api/sesion/antifalsificacion" })
        {
            using var respuesta = await entorno.Cliente.GetAsync(ruta);
            Assert.Contains("no-store", respuesta.Headers.CacheControl!.ToString());
            Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", respuesta.Headers.GetValues("Referrer-Policy").Single());
            Assert.Contains("frame-ancestors 'none'", respuesta.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Contains("geolocation=(self)", respuesta.Headers.GetValues("Permissions-Policy").Single());
        }
        using var sinSesion = await entorno.Cliente.GetAsync("/api/obras");
        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
        using var clienteSinToken = entorno.Fabrica.CreateClient();
        using var sinToken = await clienteSinToken.PostAsJsonAsync("/api/sesion/iniciar", new { nombreUsuario = "invalido", contrasena = "invalida" });
        Assert.Equal(HttpStatusCode.BadRequest, sinToken.StatusCode);
        Assert.Contains("no-store", sinToken.Headers.CacheControl!.ToString());
        Assert.DoesNotContain("Server", sinToken.Headers.Select(h => h.Key));
    }
}
