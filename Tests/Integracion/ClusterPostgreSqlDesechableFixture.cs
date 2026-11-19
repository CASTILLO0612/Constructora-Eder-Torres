using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class ClusterPostgreSqlDesechableFixture : IAsyncLifetime
{
    private string _carpetaTemporal = string.Empty;
    private string _carpetaDatos = string.Empty;
    private string _rutaPgCtl = string.Empty;
    private bool _iniciado;
    private int _puerto;

    public string CadenaConexion { get; private set; } = string.Empty;

    public string CarpetaEvidenciasPruebas { get; private set; } = string.Empty;
    public string BinariosPostgreSql { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var binarios = ResolverRutaBinariosPostgreSql();
        BinariosPostgreSql = binarios;
        var rutaInitDb = Path.Combine(binarios, "initdb.exe");
        var rutaPsql = Path.Combine(binarios, "psql.exe");
        _rutaPgCtl = Path.Combine(binarios, "pg_ctl.exe");

        var raiz = ResolverRaizRepositorio();
        _carpetaTemporal = Path.Combine(
            raiz,
            "artifacts",
            "PruebasIntegracionPg",
            Guid.NewGuid().ToString("N"));
        _carpetaDatos = Path.Combine(_carpetaTemporal, "datos");
        CarpetaEvidenciasPruebas = Path.Combine(_carpetaTemporal, "evidencias");
        Directory.CreateDirectory(_carpetaTemporal);
        Directory.CreateDirectory(CarpetaEvidenciasPruebas);

        var registroLog = Path.Combine(_carpetaTemporal, "postgresql.log");
        var puerto = ReservarPuertoLibre();
        _puerto = puerto;

        try
        {
            await EjecutarProcesoAsync(
                rutaInitDb,
                $"-D \"{_carpetaDatos}\" -U postgres -A trust --no-locale --encoding=UTF8",
                raiz,
                capturarSalida: true);

            _iniciado = true;
            // Importante en Windows: no redirigir stdout/stderr en pg_ctl start porque el proceso hijo
            // postgres.exe hereda los descriptores de tubería y mantendría abierto el stream indefinidamente.
            await EjecutarProcesoAsync(
                _rutaPgCtl,
                $"start -D \"{_carpetaDatos}\" -l \"{registroLog}\" -o \"-h 127.0.0.1 -p {puerto} -c shared_buffers=16MB -c max_connections=40\" -w -t 30",
                raiz,
                capturarSalida: false);

            await EjecutarProcesoAsync(
                rutaPsql,
                $"-X -q -w -h 127.0.0.1 -p {puerto} -U postgres -d postgres -v ON_ERROR_STOP=1 -f \"database/EsquemaConciliadoPostgreSql.sql\"",
                raiz,
                capturarSalida: true);

            await EjecutarProcesoAsync(
                rutaPsql,
                $"-X -q -w -h 127.0.0.1 -p {puerto} -U postgres -d postgres -v ON_ERROR_STOP=1 -f \"database/RegistrarLineaBaseExistente.sql\"",
                raiz,
                capturarSalida: true);

            CadenaConexion = $"Host=127.0.0.1;Port={puerto};Database=postgres;Username=postgres;Pooling=true;Maximum Pool Size=30";
            await using var contexto = new ContextoConstructora(new DbContextOptionsBuilder<ContextoConstructora>()
                .UseNpgsql(CadenaConexion).Options);
            await contexto.Database.MigrateAsync();
            using var conexionPlantilla = new Npgsql.NpgsqlConnection(CadenaConexion);
            Npgsql.NpgsqlConnection.ClearPool(conexionPlantilla);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public WebApplicationFactory<Program> CrearFabricaWeb() =>
        new FabricaApi().WithWebHostBuilder(constructor =>
        {
            constructor.UseEnvironment("Pruebas");
            constructor.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Constructora"] = CadenaConexion,
                    ["Evidencias:RutaRaiz"] = CarpetaEvidenciasPruebas,
                    ["Api:IntentosInicioSesionPorMinuto"] = "1000",
                    // Las pruebas funcionales de concurrencia aíslan integridad; los límites se prueban por separado.
                    ["Api:LimitesSolicitudes:Sincronizacion:MaximoSimultaneo"] = "100",
                    ["Api:LimitesSolicitudes:Evidencia:MaximoSimultaneo"] = "100",
                    ["Seguridad:Sesion:NombreCookie"] = "cet_sesion_pruebas",
                    ["Seguridad:Sesion:MinutosInactividad"] = "30",
                    ["Seguridad:Sesion:HorasDuracionMaxima"] = "8",
                    ["Seguridad:Sesion:PoliticaSameSite"] = "Lax",
                    ["Seguridad:Sesion:PoliticaSeguridadCookie"] = "SameAsRequest"
                });
            });
        });

    public async Task DetenerAsync()
    {
        if (!_iniciado) return;
        await EjecutarProcesoAsync(_rutaPgCtl, $"stop -D \"{_carpetaDatos}\" -m fast -w -t 30",
            ResolverRaizRepositorio(), capturarSalida: false);
        _iniciado = false;
    }

    public async Task ReiniciarAsync()
    {
        if (_iniciado) return;
        var log = Path.Combine(_carpetaTemporal, "postgresql.log");
        await EjecutarProcesoAsync(_rutaPgCtl,
            $"start -D \"{_carpetaDatos}\" -l \"{log}\" -o \"-h 127.0.0.1 -p {_puerto} -c shared_buffers=16MB -c max_connections=40\" -w -t 30",
            ResolverRaizRepositorio(), capturarSalida: false);
        _iniciado = true;
    }

    public async Task DisposeAsync()
    {
        if (_iniciado && !string.IsNullOrWhiteSpace(_rutaPgCtl) && Directory.Exists(_carpetaDatos))
        {
            try
            {
                await EjecutarProcesoAsync(
                    _rutaPgCtl,
                    $"stop -D \"{_carpetaDatos}\" -m fast -w -t 30",
                    Directory.GetCurrentDirectory(),
                    capturarSalida: false,
                    lanzarEnError: false);
            }
            catch
            {
                // Ignorar errores de parada en limpieza final.
            }
            finally
            {
                _iniciado = false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_carpetaTemporal) && Directory.Exists(_carpetaTemporal))
        {
            try
            {
                var raizPermitida = Path.GetFullPath(Path.Combine(ResolverRaizRepositorio(), "artifacts", "PruebasIntegracionPg")) + Path.DirectorySeparatorChar;
                var destino = Path.GetFullPath(_carpetaTemporal);
                if (!destino.StartsWith(raizPermitida, StringComparison.OrdinalIgnoreCase)
                    || !Guid.TryParseExact(Path.GetFileName(destino), "N", out _))
                    throw new InvalidOperationException("La limpieza quedó fuera de la carpeta de pruebas autorizada.");
                Directory.Delete(destino, recursive: true);
            }
            catch
            {
                // Ignorar si algún descriptor del sistema operativo aún libera el directorio.
            }
        }
    }

    private static string ResolverRutaBinariosPostgreSql()
    {
        var desdeEntorno = Environment.GetEnvironmentVariable("PG_BIN");
        var candidatos = new[]
        {
            desdeEntorno,
            @"C:\Program Files\PostgreSQL\16\bin",
            @"C:\Program Files\PostgreSQL\17\bin",
            @"C:\Program Files\PostgreSQL\15\bin"
        };

        foreach (var candidato in candidatos)
        {
            if (!string.IsNullOrWhiteSpace(candidato)
                && File.Exists(Path.Combine(candidato, "initdb.exe"))
                && File.Exists(Path.Combine(candidato, "pg_ctl.exe"))
                && File.Exists(Path.Combine(candidato, "psql.exe")))
            {
                return candidato;
            }
        }

        throw new InvalidOperationException(
            "No se encontraron los binarios de PostgreSQL (initdb, pg_ctl, psql). Configure la variable de entorno PG_BIN.");
    }

    private static string ResolverRaizRepositorio()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null && !File.Exists(Path.Combine(actual.FullName, "ConstructoraEderTorres.sln")))
        {
            actual = actual.Parent;
        }

        return actual?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz de la solución ConstructoraEderTorres.sln.");
    }

    private static int ReservarPuertoLibre()
    {
        using var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();
        return puerto;
    }

    private static async Task EjecutarProcesoAsync(
        string archivo,
        string argumentos,
        string directorioTrabajo,
        bool capturarSalida,
        bool lanzarEnError = true)
    {
        var info = new ProcessStartInfo
        {
            FileName = archivo,
            Arguments = argumentos,
            WorkingDirectory = directorioTrabajo,
            UseShellExecute = false,
            RedirectStandardOutput = capturarSalida,
            RedirectStandardError = capturarSalida,
            CreateNoWindow = true
        };
        info.Environment["PGOPTIONS"] = string.Empty;

        using var proceso = Process.Start(info)
            ?? throw new InvalidOperationException($"No se pudo iniciar el proceso {archivo}.");

        Task<string>? tareaStdout = capturarSalida ? proceso.StandardOutput.ReadToEndAsync() : null;
        Task<string>? tareaStderr = capturarSalida ? proceso.StandardError.ReadToEndAsync() : null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        await proceso.WaitForExitAsync(cts.Token);

        var stdout = tareaStdout is not null ? await tareaStdout : string.Empty;
        var stderr = tareaStderr is not null ? await tareaStderr : string.Empty;

        if (lanzarEnError && proceso.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"El comando '{Path.GetFileName(archivo)} {argumentos}' falló con código {proceso.ExitCode}. Stderr: {stderr} Stdout: {stdout}");
        }
    }
}
