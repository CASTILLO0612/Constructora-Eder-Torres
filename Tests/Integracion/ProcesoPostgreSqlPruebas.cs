using System.Diagnostics;
using Npgsql;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

internal static class ProcesoPostgreSqlPruebas
{
    public static async Task EjecutarPsqlAsync(string binarios, string conexion, string archivo,
        IReadOnlyDictionary<string,string>? variables = null)
    {
        var cadena = new NpgsqlConnectionStringBuilder(conexion);
        var inicio = new ProcessStartInfo(Path.Combine(binarios, "psql.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argumento in new[] { "-X", "-q", "-w", "--set=ON_ERROR_STOP=1", "--file", archivo }) inicio.ArgumentList.Add(argumento);
        foreach (var (clave,valor) in variables ?? new Dictionary<string,string>()) inicio.ArgumentList.Add($"--set={clave}={valor}");
        inicio.Environment["PGHOST"] = cadena.Host;
        inicio.Environment["PGPORT"] = cadena.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        inicio.Environment["PGUSER"] = cadena.Username;
        inicio.Environment["PGDATABASE"] = cadena.Database;
        inicio.Environment["PGPASSWORD"] = cadena.Password ?? "";
        inicio.Environment["PGOPTIONS"] = "";
        using var proceso = Process.Start(inicio)!;
        var salida = proceso.StandardOutput.ReadToEndAsync();
        var errores = proceso.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await proceso.WaitForExitAsync(limite.Token);
        Assert.True(proceso.ExitCode == 0, await errores + await salida);
    }

    public static string RaizRepositorio()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "ConstructoraEderTorres.sln"))) carpeta = carpeta.Parent;
        return carpeta?.FullName ?? throw new InvalidOperationException("No se encontró la solución.");
    }
}
