using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace ConstructoraEderTorres.Pruebas.Integracion;

/// <summary>Arranca la DLL real en un proceso y puerto propios, usando únicamente la base desechable del fixture.</summary>
internal sealed class ServidorKestrelPruebas : IAsyncDisposable
{
    private readonly Process _proceso;
    private readonly List<string> _registro = [];
    public HttpClient Cliente { get; }

    private ServidorKestrelPruebas(Process proceso, Uri direccion)
    {
        _proceso = proceso;
        Cliente = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true })
        { BaseAddress = direccion, Timeout = TimeSpan.FromSeconds(10) };
        proceso.OutputDataReceived += (_, e) => Registrar(e.Data);
        proceso.ErrorDataReceived += (_, e) => Registrar(e.Data);
    }

    private void Registrar(string? linea)
    {
        if (linea is null) return;
        lock (_registro) { if (_registro.Count < 200) _registro.Add(linea); }
    }

    public static async Task<ServidorKestrelPruebas> IniciarAsync(EntornoApiPruebas entorno)
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "ConstructoraEderTorres.sln"))) raiz = raiz.Parent;
        if (raiz is null) throw new InvalidOperationException("No se localizó la solución de pruebas.");
        var configuracion = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var carpeta = Path.Combine(raiz.FullName, "src", "Presentation");
        var dll = Path.Combine(carpeta, "bin", configuracion, "net10.0", "ConstructoraEderTorres.Presentacion.dll");
        using var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();
        var direccion = new Uri($"http://127.0.0.1:{puerto}");
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = carpeta,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        inicio.ArgumentList.Add(dll);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ASPNETCORE_URLS"] = direccion.ToString();
        inicio.Environment["ConnectionStrings__Constructora"] = entorno.Conexion;
        inicio.Environment["Evidencias__RutaRaiz"] = entorno.CarpetaEvidencias;
        inicio.Environment["Seguridad__Sesion__PoliticaSeguridadCookie"] = "SameAsRequest";
        inicio.Environment["Api__IntentosInicioSesionPorMinuto"] = "1000";
        var servidor = new ServidorKestrelPruebas(new Process { StartInfo = inicio }, direccion);
        try
        {
            servidor._proceso.Start();
            servidor._proceso.BeginOutputReadLine();
            servidor._proceso.BeginErrorReadLine();
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            while (!limite.IsCancellationRequested)
            {
                if (servidor._proceso.HasExited) throw new InvalidOperationException(string.Join(Environment.NewLine, servidor._registro));
                try
                {
                    using var salud = await servidor.Cliente.GetAsync("/salud/lista", limite.Token);
                    if (salud.IsSuccessStatusCode) return servidor;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100, limite.Token);
            }
            throw new TimeoutException("Kestrel no quedó disponible para la prueba.");
        }
        catch { await servidor.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        Cliente.Dispose();
        if (!_proceso.HasExited)
        {
            _proceso.Kill(entireProcessTree: true);
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _proceso.WaitForExitAsync(limite.Token);
        }
        _proceso.Dispose();
    }
}
