using System.Net;
using System.Net.Http.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class RevocacionSesionesApiPruebas(ClusterPostgreSqlDesechableFixture cluster) : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task LogoutRevocaLaCookieCopiadaSinCerrarOtraSesionDelMismoUsuario()
    {
        await using var entorno = await CrearAsync(cluster);
        using var clienteA = ClienteApiPruebas.Crear(entorno.Fabrica);
        using var clienteB = ClienteApiPruebas.Crear(entorno.Fabrica);
        using var entrada = await clienteA.PostAsJsonAsync("/api/sesion/iniciar", new SolicitudIniciarSesion(NombreAdministrador, Contrasena));
        entrada.EnsureSuccessStatusCode();
        var cookie = entrada.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("cet_sesion_pruebas=", StringComparison.Ordinal)).Split(';')[0];
        await AutenticarAsync(clienteB, NombreAdministrador, Contrasena);
        using var copiada = entorno.Fabrica.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        copiada.DefaultRequestHeaders.Add("Cookie", cookie);
        using (var antes = await copiada.GetAsync("/api/sesion/actual")) Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        using (var cierre = await clienteA.PostAsync("/api/sesion/cerrar", null)) Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        using (var despues = await copiada.GetAsync("/api/sesion/actual")) Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
        using (var otra = await clienteB.GetAsync("/api/sesion/actual")) Assert.Equal(HttpStatusCode.OK, otra.StatusCode);
        await AutenticarAsync(clienteA, NombreAdministrador, Contrasena);
        using (var reenvio = await copiada.GetAsync("/api/sesion/actual")) Assert.Equal(HttpStatusCode.Unauthorized, reenvio.StatusCode);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.SesionesUsuario.CountAsync(s => s.RevocadaEnUtc != null)));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "sesiones_usuario")));
    }

    [Fact]
    public async Task ReactivarUnaCuentaNoResucitaSusSesionesAnteriores()
    {
        await using var entorno = await CrearAsync(cluster);
        var roles = await entorno.Cliente.GetFromJsonAsync<RespuestaRol[]>("/api/roles");
        var usuario = await CrearAsync<RespuestaUsuario>(entorno.Cliente, "/api/usuarios",
            new SolicitudCrearUsuario("usuario.revocacion", Contrasena, null,
                [roles!.Single(r => r.Nombre == NombresRol.Administrativo).IdRol]));
        using var cliente = ClienteApiPruebas.Crear(entorno.Fabrica);
        using var entrada = await cliente.PostAsJsonAsync("/api/sesion/iniciar",
            new SolicitudIniciarSesion(usuario.NombreUsuario, Contrasena));
        entrada.EnsureSuccessStatusCode();
        var cookie = entrada.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith("cet_sesion_pruebas=", StringComparison.Ordinal)).Split(';')[0];
        using var copiada = entorno.Fabrica.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        copiada.DefaultRequestHeaders.Add("Cookie", cookie);

        foreach (var estado in new[] { EstadosUsuario.Bloqueado, EstadosUsuario.Activo })
        {
            using var cambio = await entorno.Cliente.PatchAsJsonAsync($"/api/usuarios/{usuario.IdUsuario}/estado",
                new SolicitudCambiarEstadoUsuario(estado));
            cambio.EnsureSuccessStatusCode();
            using var intento = await copiada.GetAsync("/api/sesion/actual");
            Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
        }

        await AutenticarAsync(cliente, usuario.NombreUsuario, Contrasena);
        using var nueva = await cliente.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.OK, nueva.StatusCode);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.SesionesUsuario.CountAsync(
            s => s.IdUsuario == usuario.IdUsuario && s.RevocadaEnUtc != null)));
    }

    [Fact]
    public async Task CookieRevocadaSigueRechazadaDespuesDeReiniciarLaDllReal()
    {
        await using var entorno = await CrearAsync(cluster);
        string cookie;
        await using (var servidor = await ServidorKestrelPruebas.IniciarAsync(entorno))
        {
            var token = (await servidor.Cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
            servidor.Cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
            using var entrada = await servidor.Cliente.PostAsJsonAsync("/api/sesion/iniciar", new SolicitudIniciarSesion(NombreAdministrador, Contrasena));
            entrada.EnsureSuccessStatusCode();
            cookie = entrada.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("cet_sesion=", StringComparison.Ordinal)).Split(';')[0];
            servidor.Cliente.DefaultRequestHeaders.Remove(token.Cabecera);
            token = (await servidor.Cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
            servidor.Cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
            using var cierre = await servidor.Cliente.PostAsync("/api/sesion/cerrar", null);
            Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        }
        await using var reiniciado = await ServidorKestrelPruebas.IniciarAsync(entorno);
        using var clon = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = reiniciado.Cliente.BaseAddress };
        clon.DefaultRequestHeaders.Add("Cookie", cookie);
        using var respuesta = await clon.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
