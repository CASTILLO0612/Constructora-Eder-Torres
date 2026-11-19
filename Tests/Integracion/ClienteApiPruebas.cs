using System.Net;
using System.Net.Http.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ConstructoraEderTorres.Pruebas.Integracion;

internal sealed class ClienteApiPruebas : DelegatingHandler
{
    private readonly CookieContainer _cookies = new();

    public static HttpClient Crear(WebApplicationFactory<Program> fabrica) =>
        fabrica.CreateDefaultClient(new Uri("https://localhost"), new ClienteApiPruebas());

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
    {
        var uri = solicitud.RequestUri!;
        if (solicitud.Method != HttpMethod.Get && solicitud.Method != HttpMethod.Head && solicitud.Method != HttpMethod.Options)
        {
            using var pedirToken = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, "/api/sesion/antifalsificacion"));
            AgregarCookies(pedirToken);
            using var respuestaToken = await base.SendAsync(pedirToken, cancelacion);
            GuardarCookies(uri, respuestaToken);
            respuestaToken.EnsureSuccessStatusCode();
            var token = await respuestaToken.Content.ReadFromJsonAsync<RespuestaAntifalsificacion>(cancelacion);
            solicitud.Headers.Add(token!.Cabecera, token.Token);
        }
        AgregarCookies(solicitud);
        var respuesta = await base.SendAsync(solicitud, cancelacion);
        GuardarCookies(uri, respuesta);
        return respuesta;
    }

    private void AgregarCookies(HttpRequestMessage solicitud)
    {
        solicitud.Headers.Remove("Cookie");
        var valor = _cookies.GetCookieHeader(solicitud.RequestUri!);
        if (valor.Length > 0) solicitud.Headers.Add("Cookie", valor);
    }

    private void GuardarCookies(Uri uri, HttpResponseMessage respuesta)
    {
        if (respuesta.Headers.TryGetValues("Set-Cookie", out var valores))
            foreach (var valor in valores) _cookies.SetCookies(uri, valor);
    }
}
