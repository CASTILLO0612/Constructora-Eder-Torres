using System.Globalization;
using System.Security.Cryptography;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

namespace ConstructoraEderTorres.Infraestructura.Identidad;

public sealed class ServicioHashContrasenaPbkdf2 : IServicioHashContrasena
{
    private const string PrefijoAlgoritmo = "PBKDF2-SHA256";
    private const int Iteraciones = 600_000;
    private const int LongitudSalBytes = 16;
    private const int LongitudHashBytes = 32;
    private static readonly byte[] SalSimulada = RandomNumberGenerator.GetBytes(LongitudSalBytes);

    public string GenerarHash(string contrasenaPlana)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contrasenaPlana);
        var sal = RandomNumberGenerator.GetBytes(LongitudSalBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasenaPlana, sal, Iteraciones, HashAlgorithmName.SHA256, LongitudHashBytes);
        return string.Join('$', PrefijoAlgoritmo, Iteraciones.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(sal), Convert.ToBase64String(hash));
    }

    public bool VerificarHash(string contrasenaPlana, string hashAlmacenado)
    {
        if (string.IsNullOrEmpty(contrasenaPlana) || contrasenaPlana.Length > 256) return false;
        var valido = false;
        var iteraciones = Iteraciones;
        var sal = SalSimulada;
        var esperado = new byte[LongitudHashBytes];
        if (!string.IsNullOrWhiteSpace(hashAlmacenado) && hashAlmacenado.Length <= 512)
        {
            var partes = hashAlmacenado.Split('$');
            if (partes.Length == 4 && partes[0] == PrefijoAlgoritmo
                && int.TryParse(partes[1], out var vueltas) && vueltas is >= 10_000 and <= 1_000_000)
            {
                try
                {
                    var salLeida = Convert.FromBase64String(partes[2]);
                    var hashLeido = Convert.FromBase64String(partes[3]);
                    if (salLeida.Length is >= 16 and <= 64 && hashLeido.Length == LongitudHashBytes)
                    {
                        sal = salLeida;
                        esperado = hashLeido;
                        iteraciones = vueltas;
                        valido = true;
                    }
                }
                catch (FormatException) { }
            }
        }
        var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasenaPlana, sal, iteraciones, HashAlgorithmName.SHA256, LongitudHashBytes);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado) && valido;
    }
}
