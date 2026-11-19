using System.Net;
using ConstructoraEderTorres.Presentacion.Identidad;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

public static class ConfiguracionDespliegue
{
    public static IServiceCollection AgregarConfiguracionDespliegue(this IServiceCollection servicios)
    {
        servicios.AddSingleton(proveedor =>
        {
            var configuracion = proveedor.GetRequiredService<IConfiguration>();
            var opciones = configuracion.GetSection(OpcionesDespliegue.Seccion).Get<OpcionesDespliegue>() ?? new();
            opciones.Validar(configuracion, proveedor.GetRequiredService<IHostEnvironment>().IsProduction());
            return opciones;
        });
        servicios.AddDataProtection();
        servicios.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider, ProtectorDatosConfigurado>());
        servicios.AddOptions<ForwardedHeadersOptions>().Configure<OpcionesDespliegue>((cabeceras, opciones) =>
        {
            cabeceras.KnownProxies.Clear();
            cabeceras.KnownIPNetworks.Clear();
            cabeceras.ForwardLimit = opciones.MaximoSaltosProxy;
            cabeceras.RequireHeaderSymmetry = true;
            cabeceras.ForwardedHeaders = opciones.ProxiesConfiables.Length > 0
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto : ForwardedHeaders.None;
            foreach (var proxy in opciones.ProxiesConfiables) cabeceras.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        servicios.AddHostedService<VerificadorArranqueProduccion>();
        return servicios;
    }
}
