using System.Diagnostics;
using System.Globalization;
using Npgsql;

namespace ConstructoraEderTorres.Respaldos;

public static class ProcesoPostgreSql
{
    public static NpgsqlConnectionStringBuilder Conexion(string variable, string baseEsperada)
    {
        var valor = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(valor))
            throw new ExcepcionRespaldo("conexion_no_configurada", $"Configure {variable} en el gestor de secretos del proceso.");
        var conexion = new NpgsqlConnectionStringBuilder(valor) { IncludeErrorDetail = false, LogParameters = false };
        if (string.IsNullOrWhiteSpace(baseEsperada) || !string.Equals(conexion.Database, baseEsperada, StringComparison.Ordinal))
            throw new ExcepcionRespaldo("base_no_coincide", "La conexión no coincide con --base-esperada.");
        return conexion;
    }

    public static async Task EjecutarAsync(string binarios, string nombre, IEnumerable<string> argumentos,
        NpgsqlConnectionStringBuilder? conexion, CancellationToken cancelacion,
        string? archivoSalida = null, string? archivoEntrada = null)
    {
        var ejecutable = Path.Combine(RutasRespaldo.Absoluta(binarios), nombre + (OperatingSystem.IsWindows() ? ".exe" : ""));
        if (!File.Exists(ejecutable)) throw new ExcepcionRespaldo("binario_no_encontrado", $"No se encontró {nombre} en --binarios.");
        // En Windows se transmiten bytes para admitir rutas Unicode/largas en libpq.
        // En Linux se usa el archivo directamente: pg_restore --list puede cerrar
        // una tubería antes de consumir el archivo y provocar EPIPE al terminarla.
        var entradaPorTuberia = archivoEntrada is not null && OperatingSystem.IsWindows();
        var inicio = new ProcessStartInfo(ejecutable)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = entradaPorTuberia };
        foreach (var argumento in argumentos) inicio.ArgumentList.Add(argumento);
        if (archivoEntrada is not null && !entradaPorTuberia) inicio.ArgumentList.Add(archivoEntrada);
        if (conexion is not null)
        {
            inicio.Environment["PGHOST"] = conexion.Host;
            inicio.Environment["PGPORT"] = conexion.Port.ToString(CultureInfo.InvariantCulture);
            inicio.Environment["PGUSER"] = conexion.Username;
            inicio.Environment["PGDATABASE"] = conexion.Database;
            if (!string.IsNullOrEmpty(conexion.Password)) inicio.Environment["PGPASSWORD"] = conexion.Password;
            inicio.Environment["PGCONNECT_TIMEOUT"] = conexion.Timeout.ToString(CultureInfo.InvariantCulture);
            inicio.Environment["PGSSLMODE"] = conexion.SslMode switch
            { SslMode.VerifyCA => "verify-ca", SslMode.VerifyFull => "verify-full", _ => conexion.SslMode.ToString().ToLowerInvariant() };
            if (!string.IsNullOrWhiteSpace(conexion.RootCertificate)) inicio.Environment["PGSSLROOTCERT"] = conexion.RootCertificate;
        }
        inicio.Environment["PGOPTIONS"] = "";
        using var proceso = Process.Start(inicio) ?? throw new ExcepcionRespaldo("proceso_no_iniciado", "No se pudo iniciar la utilidad PostgreSQL.");
        Task salida = archivoSalida is null ? proceso.StandardOutput.ReadToEndAsync(cancelacion)
            : GuardarSalidaAsync(proceso.StandardOutput.BaseStream, archivoSalida, cancelacion);
        var entrada = !entradaPorTuberia ? Task.CompletedTask : EnviarEntradaAsync(proceso, archivoEntrada!,
            nombre == "pg_restore" && argumentos.Contains("--list"), cancelacion);
        var errores = proceso.StandardError.ReadToEndAsync(cancelacion);
        try
        {
            var espera = proceso.WaitForExitAsync(cancelacion);
            var pendientes = new HashSet<Task> { espera, salida, entrada };
            while (!espera.IsCompleted && pendientes.Count > 0)
            {
                var terminada = await Task.WhenAny(pendientes);
                pendientes.Remove(terminada);
                if (terminada.IsFaulted || terminada.IsCanceled)
                {
                    if (!proceso.HasExited) proceso.Kill(entireProcessTree: true);
                    await terminada;
                }
            }
            await espera;
        }
        catch (OperationCanceledException) { if (!proceso.HasExited) proceso.Kill(entireProcessTree: true); throw; }
        try { await Task.WhenAll(salida, entrada, errores); }
        catch (IOException) when (proceso.ExitCode != 0) { }
        if (proceso.ExitCode != 0)
            throw new ExcepcionRespaldo("utilidad_postgresql_fallida", $"{nombre} no terminó satisfactoriamente. La copia no se declara válida.");
    }

    private static async Task GuardarSalidaAsync(Stream salida, string ruta, CancellationToken cancelacion)
    {
        // La biblioteca PostgreSQL nativa de Windows puede rechazar rutas Unicode/largas: se transmiten bytes.
        await using var archivo = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await salida.CopyToAsync(archivo, cancelacion);
        await archivo.FlushAsync(cancelacion);
    }

    private static async Task EnviarEntradaAsync(Process proceso, string ruta, bool lecturaParcial, CancellationToken cancelacion)
    {
        await using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        try { await archivo.CopyToAsync(proceso.StandardInput.BaseStream, cancelacion); }
        catch (IOException) when (lecturaParcial) { /* --list puede cerrar la entrada después de leer el catálogo. */ }
        finally { proceso.StandardInput.Close(); }
    }
}
