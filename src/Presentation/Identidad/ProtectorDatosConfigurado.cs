using System.Security.Cryptography.X509Certificates;
using ConstructoraEderTorres.Presentacion.Configuracion;
using Microsoft.AspNetCore.DataProtection;

namespace ConstructoraEderTorres.Presentacion.Identidad;

public sealed class ProtectorDatosConfigurado : IDataProtectionProvider, IDisposable
{
    private readonly IDataProtectionProvider _proveedor;
    private readonly List<X509Certificate2> _certificados = [];

    public ProtectorDatosConfigurado(OpcionesDespliegue opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones.RutaClaves))
        {
            _proveedor = DataProtectionProvider.Create(opciones.NombreAplicacion);
            return;
        }
        try
        {
            var actual = Cargar(opciones.Certificado);
            if (actual.NotBefore.ToUniversalTime() > DateTime.UtcNow || actual.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                throw new InvalidOperationException("El certificado de protección de sesiones no está vigente.");
            foreach (var anterior in opciones.CertificadosAnteriores) Cargar(anterior);
            _proveedor = DataProtectionProvider.Create(new DirectoryInfo(opciones.RutaClaves), proteccion => proteccion
                .SetApplicationName(opciones.NombreAplicacion).ProtectKeysWithCertificate(actual)
                .UnprotectKeysWithAnyCertificate(_certificados.ToArray()));
        }
        catch { Dispose(); throw; }
    }

    private X509Certificate2 Cargar(CertificadoProteccion opciones)
    {
        var certificado = X509CertificateLoader.LoadPkcs12FromFile(opciones.Ruta, opciones.Contrasena,
            X509KeyStorageFlags.EphemeralKeySet);
        _certificados.Add(certificado);
        using var clave = certificado.GetRSAPrivateKey();
        if (clave is null) throw new InvalidOperationException("El certificado de sesiones requiere su clave privada RSA.");
        return certificado;
    }

    public IDataProtector CreateProtector(string purpose) => _proveedor.CreateProtector(purpose);
    public void Dispose() { foreach (var certificado in _certificados) certificado.Dispose(); }
}
