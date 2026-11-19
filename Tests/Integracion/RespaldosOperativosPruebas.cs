using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Respaldos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class RespaldosOperativosPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private async Task<(int Codigo, string Salida, string Error)> EjecutarAsync(string[] argumentos, string? respaldo = null, string? restauracion = null)
    {
        var configuracion = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var inicio = new ProcessStartInfo("dotnet")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        inicio.ArgumentList.Add(Path.Combine(ProcesoPostgreSqlPruebas.RaizRepositorio(), "Tools", "Respaldos", "bin", configuracion, "net10.0", "ConstructoraEderTorres.Respaldos.dll"));
        foreach (var argumento in argumentos) inicio.ArgumentList.Add(argumento);
        inicio.Environment["ConnectionStrings__Respaldo"] = respaldo ?? "";
        inicio.Environment["ConnectionStrings__Restauracion"] = restauracion ?? "";
        using var proceso = Process.Start(inicio)!;
        var salida = proceso.StandardOutput.ReadToEndAsync();
        var errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await proceso.WaitForExitAsync(limite.Token);
        return (proceso.ExitCode, await salida, await errores);
    }

    private async Task<RespuestaEvidencia> PrepararAsync(EntornoApiPruebas entorno)
    {
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var avance = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 1m));
        var hora = entorno.AhoraUtc.AddMinutes(-1);
        await CrearAsync<RespuestaAsistencia>(entorno.Cliente, "/api/asistencias", new SolicitudRegistrarAsistencia(
            escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(hora), hora, LatitudEntrada:12m, LongitudEntrada:-86m,
            UuidRegistro:Guid.NewGuid(), PrecisionGpsEntradaMetros:5m));
        return await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias",
            new SolicitudRegistrarEvidencia(avance.IdAvance, TiposEvidencia.Fotografia, "captura.png", FotoPng,
                CapturadaEn:entorno.AhoraUtc, Latitud:12m, Longitud:-86m, UuidRegistro:Guid.NewGuid()));
    }

    private async Task<string> CrearCopiaAsync(EntornoApiPruebas entorno, string? destino = null)
    {
        var raiz = destino ?? Path.Combine(cluster.CarpetaEvidenciasPruebas, "respaldos-" + Guid.NewGuid().ToString("N"));
        var resultado = await EjecutarAsync(["crear", "--destino", raiz, "--evidencias", entorno.CarpetaEvidencias,
            "--binarios", cluster.BinariosPostgreSql, "--base-esperada", new NpgsqlConnectionStringBuilder(entorno.Conexion).Database!], entorno.Conexion);
        Assert.True(resultado.Codigo == 0, resultado.Error);
        return JsonSerializer.Deserialize<ResultadoRespaldo>(resultado.Salida)!.Carpeta;
    }

    [Fact]
    public async Task RespaldoRestauraBaseEvidenciaAsistenciaCuentasYDescargaPorApi()
    {
        await using var entorno = await CrearAsync(cluster);
        var foto = await PrepararAsync(entorno);
        var copia = await CrearCopiaAsync(entorno);
        var verificada = await EjecutarAsync(["verificar", "--carpeta", copia, "--binarios", cluster.BinariosPostgreSql]);
        Assert.True(verificada.Codigo == 0, verificada.Error);
        await entorno.CrearEscenarioAsync(); // Cambios posteriores no pertenecen al punto de recuperación.
        var nombre = "recuperacion_" + Guid.NewGuid().ToString("N");
        await using var control = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(cluster.CadenaConexion) { Pooling = false }.ConnectionString);
        await control.OpenAsync();
        await using (var crear = new NpgsqlCommand($"CREATE DATABASE {nombre} TEMPLATE template0", control)) await crear.ExecuteNonQueryAsync();
        var cadena = new NpgsqlConnectionStringBuilder(entorno.Conexion) { Database = nombre }.ConnectionString;
        var archivos = Path.Combine(cluster.CarpetaEvidenciasPruebas, nombre);
        try
        {
            var restaurada = await EjecutarAsync(["restaurar", "--carpeta", copia, "--binarios", cluster.BinariosPostgreSql,
                "--destino-evidencias", archivos, "--base-esperada", nombre], restauracion:cadena);
            Assert.True(restaurada.Codigo == 0, restaurada.Error);
            using var fabrica = entorno.Fabrica.WithWebHostBuilder(constructor => constructor.ConfigureAppConfiguration((_, configuracion) =>
                configuracion.AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:Constructora"] = cadena, ["Evidencias:RutaRaiz"] = archivos })));
            using var cliente = ClienteApiPruebas.Crear(fabrica);
            await AutenticarAsync(cliente, NombreAdministrador, Contrasena);
            using var descarga = await cliente.GetAsync($"/api/seguimiento/evidencias/{foto.IdEvidencia}/archivo");
            descarga.EnsureSuccessStatusCode();
            Assert.Equal(FotoPng, await descarga.Content.ReadAsByteArrayAsync());
            await using var origen = NpgsqlDataSource.Create(cadena);
            await using var consulta = origen.CreateCommand("SELECT (SELECT count(*) FROM public.obras), (SELECT count(*) FROM public.asistencias), (SELECT count(*) FROM public.usuarios), (SELECT count(*) FROM public.\"__EFMigrationsHistory\")");
            await using var lector = await consulta.ExecuteReaderAsync();
            Assert.True(await lector.ReadAsync());
            Assert.Equal(1, lector.GetInt64(0));
            Assert.Equal(1, lector.GetInt64(1));
            Assert.Equal(1, lector.GetInt64(2));
            var migracionesEsperadas = await entorno.EnBaseAsync(c => Task.FromResult(c.Database.GetMigrations().Count()));
            Assert.Equal(migracionesEsperadas, lector.GetInt64(3));
        }
        finally
        {
            Assert.Matches("^recuperacion_[0-9a-f]{32}$", nombre);
            await using var eliminar = new NpgsqlCommand($"DROP DATABASE {nombre} WITH (FORCE)", control);
            await eliminar.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RespaldoIncompletoNoSeDeclaraValidoSiFaltaOSeAlteraUnaEvidencia(bool alterar)
    {
        await using var entorno = await CrearAsync(cluster);
        await PrepararAsync(entorno);
        var relativa = await entorno.EnBaseAsync(c => c.Evidencias.Select(e => e.RutaArchivo).SingleAsync());
        var ruta = RutasRespaldo.Resolver(entorno.CarpetaEvidencias, relativa);
        if (alterar) await File.WriteAllTextAsync(ruta, "contenido alterado de prueba"); else File.Delete(ruta);
        var destino = Path.Combine(cluster.CarpetaEvidenciasPruebas, "respaldos-" + Guid.NewGuid().ToString("N"));
        var resultado = await EjecutarAsync(["crear", "--destino", destino, "--evidencias", entorno.CarpetaEvidencias,
            "--binarios", cluster.BinariosPostgreSql, "--base-esperada", new NpgsqlConnectionStringBuilder(entorno.Conexion).Database!], entorno.Conexion);
        Assert.Equal(1, resultado.Codigo);
        Assert.Contains(alterar ? "huella_evidencia_invalida" : "evidencia_ausente", resultado.Error);
        Assert.Empty(Directory.GetDirectories(destino, "respaldo-*"));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
    }

    [Fact]
    public async Task RestauracionRechazaOrigenBaseOcupadaYArchivosAlterados()
    {
        await using var entorno = await CrearAsync(cluster);
        await PrepararAsync(entorno);
        var copia = await CrearCopiaAsync(entorno);
        var nombre = new NpgsqlConnectionStringBuilder(entorno.Conexion).Database!;
        var argumentos = new[] { "restaurar", "--carpeta", copia, "--binarios", cluster.BinariosPostgreSql,
            "--destino-evidencias", Path.Combine(cluster.CarpetaEvidenciasPruebas, Guid.NewGuid().ToString("N")), "--base-esperada", nombre };
        var sobreOrigen = await EjecutarAsync(argumentos, restauracion:entorno.Conexion);
        Assert.Equal(1, sobreOrigen.Codigo);
        Assert.Contains("restauracion_sobre_origen", sobreOrigen.Error);
        await using var ocupada = await CrearAsync(cluster);
        argumentos[^1] = new NpgsqlConnectionStringBuilder(ocupada.Conexion).Database!;
        var sobreOcupada = await EjecutarAsync(argumentos, restauracion:ocupada.Conexion);
        Assert.Equal(1, sobreOcupada.Codigo);
        Assert.Contains("base_destino_ocupada", sobreOcupada.Error);
        var manifiesto = JsonSerializer.Deserialize<ManifiestoRespaldo>(await File.ReadAllTextAsync(Path.Combine(copia, "manifiesto.json")))!;
        var archivo = RutasRespaldo.Resolver(Path.Combine(copia,"evidencias"), manifiesto.Evidencias[0].RutaRelativa);
        await File.AppendAllTextAsync(archivo, "alteración de prueba");
        var alterada = await EjecutarAsync(["verificar", "--carpeta", copia, "--binarios", cluster.BinariosPostgreSql]);
        Assert.Equal(1, alterada.Codigo);
        Assert.Contains("evidencia_respaldo_alterada", alterada.Error);
    }

    [Fact]
    public async Task RetencionConservaCopiasMinimasYNoBorraDirectoriosAjenos()
    {
        await using var entorno = await CrearAsync(cluster);
        Directory.CreateDirectory(entorno.CarpetaEvidencias);
        var destino = Path.Combine(cluster.CarpetaEvidenciasPruebas, "retencion-" + Guid.NewGuid().ToString("N"));
        for (var i = 0; i < 3; i++)
        {
            var copia = await CrearCopiaAsync(entorno, destino);
            var archivo = Path.Combine(copia, "manifiesto.json");
            var manifiesto = JsonSerializer.Deserialize<ManifiestoRespaldo>(await File.ReadAllTextAsync(archivo))!;
            await File.WriteAllTextAsync(archivo, JsonSerializer.Serialize(manifiesto with { CreadoUtc = DateTime.UtcNow.AddDays(-30+i) }));
        }
        var ajena = Path.Combine(destino, "documentos-ajenos");
        Directory.CreateDirectory(ajena);
        await File.WriteAllTextAsync(Path.Combine(ajena, "conservar.txt"), "conservar");
        var resultado = await EjecutarAsync(["depurar", "--destino", destino, "--retencion-dias", "1",
            "--conservar-minimo", "2", "--binarios", cluster.BinariosPostgreSql]);
        Assert.True(resultado.Codigo == 0, resultado.Error);
        Assert.Equal(1, JsonSerializer.Deserialize<ResultadoRespaldo>(resultado.Salida)!.Eliminados);
        Assert.Equal(2, Directory.GetDirectories(destino, "respaldo-*").Length);
        Assert.True(File.Exists(Path.Combine(ajena, "conservar.txt")));
    }

    [Theory]
    [InlineData("../fuera.txt")]
    [InlineData("carpeta/../../fuera.txt")]
    [InlineData("/absoluto.txt")]
    [InlineData("C:\\fuera.txt")]
    public void RutasDeEvidenciaNoPuedenEscaparDeSuCarpeta(string relativa) =>
        Assert.Throws<ExcepcionRespaldo>(() => RutasRespaldo.Resolver(cluster.CarpetaEvidenciasPruebas, relativa));

    [Fact]
    public void OpcionesNoAdmitenCredencialesComoArgumento() =>
        Assert.Throws<ExcepcionRespaldo>(() => OpcionesRespaldos.Leer(["crear", "--conexion", "credencial-sintetica"]));
}
