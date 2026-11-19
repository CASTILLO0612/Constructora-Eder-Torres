using System.Globalization;
using System.Security.Claims;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Presentacion.Configuracion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ConstructoraEderTorres.Presentacion.Identidad;

public sealed class ContextoUsuarioHttp(IHttpContextAccessor accesorHttp) : IContextoUsuarioActual
{
    public const string ClaveItemUsuario = "CET_UsuarioAutenticado";
    public const string ReclamoIdTrabajador = "id_trabajador";
    public const string ReclamoEmitidaUtc = "emitida_utc";
    public const string ReclamoSelloCredenciales = "sello_credenciales";
    public const string ReclamoIdSesion = "id_sesion";

    public UsuarioAutenticado? ObtenerUsuario()
    {
        var contextoHttp = accesorHttp.HttpContext;
        if (contextoHttp is null)
        {
            return null;
        }

        if (contextoHttp.Items.TryGetValue(ClaveItemUsuario, out var valor)
            && valor is UsuarioAutenticado usuario)
        {
            return usuario with { IpOrigen = contextoHttp.Connection.RemoteIpAddress };
        }

        return null;
    }

    public static async Task EmitirCookieSesionAsync(
        HttpContext contextoHttp,
        ResultadoAutenticacion autenticacion,
        OpcionesSesion opciones)
    {
        var sesion = autenticacion.Perfil;
        var idSesion = await contextoHttp.RequestServices.GetRequiredService<IPuertoSesiones>().CrearAsync(
            sesion.IdUsuario, sesion.AutenticadoEnUtc, sesion.AutenticadoEnUtc.AddHours(opciones.HorasDuracionMaxima),
            contextoHttp.RequestAborted);
        var reclamos = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sesion.IdUsuario.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, sesion.NombreUsuario),
            new(ReclamoIdSesion, idSesion.ToString()),
            new(ReclamoEmitidaUtc, sesion.AutenticadoEnUtc.ToString("O", CultureInfo.InvariantCulture)),
            new(ReclamoSelloCredenciales, autenticacion.SelloCredenciales)
        };

        if (sesion.IdTrabajador is { } idTrabajador)
        {
            reclamos.Add(new Claim(ReclamoIdTrabajador, idTrabajador.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var rol in sesion.Roles)
        {
            reclamos.Add(new Claim(ClaimTypes.Role, rol));
        }

        var identidad = new ClaimsIdentity(reclamos, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identidad);

        var propiedades = new AuthenticationProperties
        {
            IsPersistent = false,
            IssuedUtc = new DateTimeOffset(DateTime.SpecifyKind(sesion.AutenticadoEnUtc, DateTimeKind.Utc)),
            ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(
                sesion.AutenticadoEnUtc.AddMinutes(Math.Max(1, opciones.MinutosInactividad)),
                DateTimeKind.Utc)),
            AllowRefresh = true
        };

        contextoHttp.Items[ClaveItemUsuario] = new UsuarioAutenticado(
            sesion.IdUsuario,
            sesion.NombreUsuario,
            sesion.IdTrabajador,
            sesion.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase),
            sesion.Permisos.ToHashSet(StringComparer.OrdinalIgnoreCase),
            sesion.ObrasAsignadas.ToHashSet(),
            contextoHttp.Connection.RemoteIpAddress);

        await contextoHttp.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            propiedades);
    }

    public static async Task RevocarCookieSesionAsync(HttpContext contextoHttp)
    {
        contextoHttp.Items.Remove(ClaveItemUsuario);
        await contextoHttp.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public static async Task CerrarSesionPersistidaAsync(HttpContext contextoHttp)
    {
        if (Guid.TryParse(contextoHttp.User.FindFirstValue(ReclamoIdSesion), out var idSesion)
            && int.TryParse(contextoHttp.User.FindFirstValue(ClaimTypes.NameIdentifier), out var idUsuario))
            await contextoHttp.RequestServices.GetRequiredService<IPuertoSesiones>().RevocarAsync(idSesion, idUsuario,
                contextoHttp.RequestServices.GetRequiredService<IRelojSistema>().AhoraUtc, contextoHttp.RequestAborted);
        await RevocarCookieSesionAsync(contextoHttp);
    }

    public static async Task ValidarPrincipalCookieAsync(
        CookieValidatePrincipalContext contextoCookie,
        OpcionesSesion opciones)
    {
        var principal = contextoCookie.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            contextoCookie.RejectPrincipal();
            return;
        }

        var valorIdUsuario = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var valorEmitida = principal.FindFirstValue(ReclamoEmitidaUtc);
        if (!Guid.TryParse(principal.FindFirstValue(ReclamoIdSesion), out var idSesion) || idSesion == Guid.Empty)
        {
            contextoCookie.RejectPrincipal();
            await contextoCookie.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (!int.TryParse(valorIdUsuario, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idUsuario) || idUsuario <= 0)
        {
            contextoCookie.RejectPrincipal();
            await contextoCookie.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var servicios = contextoCookie.HttpContext.RequestServices;
        var reloj = servicios.GetRequiredService<IRelojSistema>();

        if (!DateTime.TryParse(valorEmitida, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var emitidaUtc)
            || emitidaUtc.Kind != DateTimeKind.Utc || emitidaUtc > reloj.AhoraUtc
            || reloj.AhoraUtc >= emitidaUtc.AddHours(opciones.HorasDuracionMaxima))
        {
            contextoCookie.RejectPrincipal();
            await contextoCookie.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var puertoSeguridad = servicios.GetRequiredService<IPuertoSeguridad>();
        var usuarioVigente = await puertoSeguridad.ConstruirContextoUsuarioActivoAsync(
            idUsuario,
            servicios.GetRequiredService<ICalendarioLaboral>().Hoy,
            contextoCookie.HttpContext.Connection.RemoteIpAddress,
            contextoCookie.HttpContext.RequestAborted,
            idSesion, reloj.AhoraUtc);

        if (usuarioVigente is null || !string.Equals(principal.FindFirstValue(ReclamoSelloCredenciales), usuarioVigente.SelloCredenciales, StringComparison.Ordinal))
        {
            contextoCookie.RejectPrincipal();
            await contextoCookie.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        contextoCookie.HttpContext.Items[ClaveItemUsuario] = usuarioVigente with { AutenticadoEnUtc = emitidaUtc };
    }
}
