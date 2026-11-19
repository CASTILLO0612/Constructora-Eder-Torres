using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ConstructoraEderTorres.Presentacion.Configuracion;
using ConstructoraEderTorres.Presentacion.Identidad;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Dominio.Reglas;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class DespliegueSeguroPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private static IConfiguration Configuracion(Dictionary<string,string?>? cambios = null)
    {
        var valores = new Dictionary<string,string?> { ["AllowedHosts"] = "api.empresa.example",
            ["Seguridad:Sesion:PoliticaSeguridadCookie"] = "Always" };
        foreach (var par in cambios ?? []) valores[par.Key] = par.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.empresa.example")]
    [InlineData("")]
    public void ProduccionRechazaHostsAbiertos(string hosts) =>
        Assert.Throws<InvalidOperationException>(() => new OpcionesDespliegue().Validar(
            Configuracion(new() { ["AllowedHosts"] = hosts }), true));

    [Theory]
    [InlineData("Seguridad:Sesion:PoliticaSeguridadCookie", "SameAsRequest")]
    [InlineData("Seguridad:Sesion:OrigenesCorsPermitidos:0", "http://cliente.example")]
    public void ProduccionRechazaCookiesOCorsSinHttps(string clave, string valor) =>
        Assert.Throws<InvalidOperationException>(() => new OpcionesDespliegue().Validar(Configuracion(new() { [clave] = valor }), true));

    [Fact]
    public void PersistenciaExplicitaDeClavesSinCifradoSeRechaza() =>
        Assert.Throws<InvalidOperationException>(() => new OpcionesDespliegue { RutaClaves = "claves" }.Validar(Configuracion(), false));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CabecerasReenviadasSoloSeAplicanDesdeUnProxyConfiable(bool confiable)
    {
        var opciones = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
        { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, RequireHeaderSymmetry = true };
        opciones.KnownProxies.Clear();
        opciones.KnownIPNetworks.Clear();
        opciones.KnownProxies.Add(IPAddress.Parse(confiable ? "127.0.0.1" : "192.0.2.10"));
        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = IPAddress.Loopback;
        contexto.Request.Scheme = "http";
        contexto.Request.Headers["X-Forwarded-For"] = "203.0.113.8";
        contexto.Request.Headers["X-Forwarded-Proto"] = "https";
        var intermediario = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(opciones));
        await intermediario.Invoke(contexto);
        Assert.Equal(confiable ? "https" : "http", contexto.Request.Scheme);
        Assert.Equal(confiable ? "203.0.113.8" : "127.0.0.1", contexto.Connection.RemoteIpAddress!.ToString());
    }

    private CertificadoProteccion CrearCertificado(string carpeta, string nombre)
    {
        Directory.CreateDirectory(carpeta);
        using var clave = RSA.Create(3072);
        var solicitud = new CertificateRequest("CN=pruebas-cet", clave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var ruta = Path.Combine(carpeta, nombre + ".pfx");
        File.WriteAllBytes(ruta, certificado.Export(X509ContentType.Pfx, "ClaveCertificadoSintetica!"));
        return new() { Ruta = ruta, Contrasena = "ClaveCertificadoSintetica!" };
    }

    [Fact]
    public void ClavesCifradasSobrevivenReinicioYRotacionDelCertificado()
    {
        var carpeta = Path.Combine(cluster.CarpetaEvidenciasPruebas, Guid.NewGuid().ToString("N"));
        var primero = CrearCertificado(carpeta, "anterior");
        var segundo = CrearCertificado(carpeta, "actual");
        var claves = Path.Combine(carpeta, "claves");
        string protegido;
        using (var inicial = new ProtectorDatosConfigurado(new() { RutaClaves = claves, Certificado = primero }))
            protegido = inicial.CreateProtector("sesion-prueba").Protect("valor-sesion-prueba");
        var archivos = Directory.GetFiles(claves, "key-*.xml");
        Assert.NotEmpty(archivos);
        foreach (var archivo in archivos)
        {
            var contenido = File.ReadAllText(archivo);
            Assert.Contains("encryptedSecret", contenido);
            Assert.DoesNotContain("<masterKey", contenido);
        }
        using var reiniciado = new ProtectorDatosConfigurado(new() { RutaClaves = claves, Certificado = primero });
        Assert.Equal("valor-sesion-prueba", reiniciado.CreateProtector("sesion-prueba").Unprotect(protegido));
        using var rotado = new ProtectorDatosConfigurado(new() { RutaClaves = claves, Certificado = segundo, CertificadosAnteriores = [primero] });
        Assert.Equal("valor-sesion-prueba", rotado.CreateProtector("sesion-prueba").Unprotect(protegido));
    }

    [Fact]
    public async Task ApiEnProduccionFuncionaConRolLimitadoSinPermitirDdlNiAlterarAuditoria()
    {
        await using var entorno = await CrearAsync(cluster);
        var rol = "ejecucion_" + Guid.NewGuid().ToString("N");
        await ProcesoPostgreSqlPruebas.EjecutarPsqlAsync(cluster.BinariosPostgreSql, entorno.Conexion,
            Path.Combine(ProcesoPostgreSqlPruebas.RaizRepositorio(), "Tools", "Operacion", "ConfigurarRolEjecucion.sql"),
            new Dictionary<string,string> { ["rol_ejecucion"] = rol, ["rol_propietario"] = "postgres" });
        var cadena = new NpgsqlConnectionStringBuilder(entorno.Conexion) { Username = rol }.ConnectionString;
        await using var origen = NpgsqlDataSource.Create(cadena);
        await VerificadorArranqueProduccion.ValidarPermisosAsync(origen);
        await using var administrativo = NpgsqlDataSource.Create(entorno.Conexion);
        await Assert.ThrowsAsync<InvalidOperationException>(() => VerificadorArranqueProduccion.ValidarPermisosAsync(administrativo));
        foreach (var sentencia in new[] { "CREATE TABLE public.prohibida(id integer)",
                     "UPDATE public.auditorias SET id_usuario = null", "DELETE FROM public.auditorias",
                     "INSERT INTO public.\"__EFMigrationsHistory\" VALUES ('falsa','10.0.14')" })
        {
            await using var comando = origen.CreateCommand(sentencia);
            var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        var carpeta = Path.Combine(cluster.CarpetaEvidenciasPruebas, Guid.NewGuid().ToString("N"));
        var certificado = CrearCertificado(carpeta, "proteccion");
        using var fabrica = entorno.Fabrica.WithWebHostBuilder(constructor =>
        {
            constructor.UseEnvironment("Production");
            constructor.ConfigureAppConfiguration((_, configuracion) => configuracion.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["AllowedHosts"] = "localhost",
                ["ConnectionStrings:Constructora"] = cadena,
                ["Seguridad:Sesion:PoliticaSeguridadCookie"] = "Always",
                ["Seguridad:Despliegue:RutaClaves"] = Path.Combine(carpeta, "claves"),
                ["Seguridad:Despliegue:Certificado:Ruta"] = certificado.Ruta,
                ["Seguridad:Despliegue:Certificado:Contrasena"] = certificado.Contrasena
            }));
        });
        using var cliente = ClienteApiPruebas.Crear(fabrica);
        await AutenticarAsync(cliente, NombreAdministrador, Contrasena);
        using var salud = await cliente.GetAsync("/salud/lista");
        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
        using var obras = await cliente.GetAsync("/api/obras");
        Assert.Equal(HttpStatusCode.OK, obras.StatusCode);
        await CrearAsync<RespuestaObra>(cliente, "/api/obras", new SolicitudCrearObra("ROL-" + Guid.NewGuid().ToString("N")[..8],
            "Obra con rol limitado", null, "Dirección de pruebas", entorno.HoyLaboral, entorno.HoyLaboral.AddDays(10), EstadosObra.Activa, 12m, -86m, 100m));
        using var documentos = await cliente.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, documentos.StatusCode);
    }
}
