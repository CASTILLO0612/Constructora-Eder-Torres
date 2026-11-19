using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace ConstructoraEderTorres.Respaldos;

public sealed partial class ServicioRespaldos
{
    private const string NombreBase = "base.dump";
    private const string NombreManifiesto = "manifiesto.json";
    private static readonly JsonSerializerOptions Formato = new() { WriteIndented = true };

    public Task<ResultadoRespaldo> EjecutarAsync(OpcionesRespaldos opciones, CancellationToken cancelacion = default) => opciones.Accion switch
    {
        "crear" => CrearAsync(opciones, cancelacion),
        "verificar" => VerificarResultadoAsync(opciones, cancelacion),
        "restaurar" => RestaurarAsync(opciones, cancelacion),
        "depurar" => DepurarAsync(opciones, cancelacion),
        _ => throw new ExcepcionRespaldo("accion_invalida", "La acción no es válida.")
    };

    private static async Task<ResultadoRespaldo> CrearAsync(OpcionesRespaldos opciones, CancellationToken cancelacion)
    {
        var conexion = ProcesoPostgreSql.Conexion("ConnectionStrings__Respaldo", opciones.Exigir("base-esperada"));
        var destino = RutasRespaldo.Absoluta(opciones.Exigir("destino"));
        var origen = RutasRespaldo.Absoluta(opciones.Exigir("evidencias"));
        if (!Directory.Exists(origen) || RutasRespaldo.Contiene(origen, destino))
            throw new ExcepcionRespaldo("raiz_invalida", "La carpeta de evidencias debe existir y el respaldo debe quedar fuera de ella.");
        Directory.CreateDirectory(destino);
        var sufijo = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
        var pendiente = Path.Combine(destino, "pendiente-" + sufijo);
        var completa = Path.Combine(destino, "respaldo-" + sufijo);
        Directory.CreateDirectory(pendiente);
        await using var baseDatos = new NpgsqlConnection(conexion.ConnectionString);
        await baseDatos.OpenAsync(cancelacion);
        await using var transaccion = await baseDatos.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancelacion);
        await using (var lectura = new NpgsqlCommand("SET TRANSACTION READ ONLY", baseDatos, transaccion))
            await lectura.ExecuteNonQueryAsync(cancelacion);
        string instantanea;
        await using (var exportar = new NpgsqlCommand("SELECT pg_export_snapshot()", baseDatos, transaccion))
            instantanea = (string)(await exportar.ExecuteScalarAsync(cancelacion))!;
        var referencias = await LeerReferenciasAsync(baseDatos, transaccion, cancelacion);
        var migraciones = await LeerMigracionesAsync(baseDatos, transaccion, cancelacion);
        await ProcesoPostgreSql.EjecutarAsync(opciones.Exigir("binarios"), "pg_dump",
            ["--format=custom", "--no-owner", "--no-acl", "--no-password", "--snapshot", instantanea,
                ], conexion, cancelacion, archivoSalida: Path.Combine(pendiente, NombreBase));
        await ProcesoPostgreSql.EjecutarAsync(opciones.Exigir("binarios"), "pg_restore",
            ["--list"], null, cancelacion, archivoEntrada: Path.Combine(pendiente, NombreBase));
        var archivos = new List<ArchivoEvidenciaRespaldo>();
        foreach (var referencia in referencias)
        {
            var fuente = RutasRespaldo.Resolver(origen, referencia.RutaRelativa);
            if (!File.Exists(fuente)) throw new ExcepcionRespaldo("evidencia_ausente", "Una evidencia referenciada por la base no existe. El respaldo queda pendiente.");
            var copia = RutasRespaldo.Resolver(Path.Combine(pendiente, "evidencias"), referencia.RutaRelativa);
            await RutasRespaldo.CopiarAsync(fuente, copia, cancelacion);
            var huella = await RutasRespaldo.HuellaAsync(copia, cancelacion);
            if (!string.Equals(huella, referencia.HuellaSha256, StringComparison.OrdinalIgnoreCase))
                throw new ExcepcionRespaldo("huella_evidencia_invalida", "Una evidencia no coincide con la huella registrada en la base.");
            archivos.Add(new(referencia.RutaRelativa, huella, new FileInfo(copia).Length));
        }
        var archivoBase = Path.Combine(pendiente, NombreBase);
        var manifiesto = new ManifiestoRespaldo(1, DateTime.UtcNow, conexion.Database!,
            await RutasRespaldo.HuellaAsync(archivoBase, cancelacion), new FileInfo(archivoBase).Length, migraciones, archivos);
        await File.WriteAllTextAsync(Path.Combine(pendiente, NombreManifiesto), JsonSerializer.Serialize(manifiesto, Formato), cancelacion);
        await transaccion.CommitAsync(cancelacion);
        Directory.Move(pendiente, completa);
        return new("crear", completa, archivos.Count);
    }

    private static async Task<ManifiestoRespaldo> VerificarAsync(OpcionesRespaldos opciones, CancellationToken cancelacion)
    {
        var carpeta = RutasRespaldo.Absoluta(opciones.Exigir("carpeta"));
        var manifiesto = await LeerManifiestoAsync(carpeta, cancelacion);
        var baseDatos = RutasRespaldo.Resolver(carpeta, NombreBase);
        if (!File.Exists(baseDatos) || new FileInfo(baseDatos).Length != manifiesto.TamanoBaseBytes
            || await RutasRespaldo.HuellaAsync(baseDatos, cancelacion) != manifiesto.HuellaBaseSha256)
            throw new ExcepcionRespaldo("base_respaldo_alterada", "La copia de base no coincide con su manifiesto.");
        var rutas = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var evidencia in manifiesto.Evidencias)
        {
            var archivo = RutasRespaldo.Resolver(Path.Combine(carpeta, "evidencias"), evidencia.RutaRelativa);
            if (!rutas.Add(archivo) || !File.Exists(archivo) || new FileInfo(archivo).Length != evidencia.TamanoBytes
                || await RutasRespaldo.HuellaAsync(archivo, cancelacion) != evidencia.HuellaSha256)
                throw new ExcepcionRespaldo("evidencia_respaldo_alterada", "Una evidencia está ausente, duplicada o no coincide con su manifiesto.");
        }
        await ProcesoPostgreSql.EjecutarAsync(opciones.Exigir("binarios"), "pg_restore", ["--list"], null, cancelacion, archivoEntrada: baseDatos);
        return manifiesto;
    }

    private static async Task<ResultadoRespaldo> VerificarResultadoAsync(OpcionesRespaldos opciones, CancellationToken cancelacion)
    {
        var manifiesto = await VerificarAsync(opciones, cancelacion);
        return new("verificar", RutasRespaldo.Absoluta(opciones.Exigir("carpeta")), manifiesto.Evidencias.Count);
    }

    private static async Task<ResultadoRespaldo> RestaurarAsync(OpcionesRespaldos opciones, CancellationToken cancelacion)
    {
        var manifiesto = await VerificarAsync(opciones, cancelacion);
        var conexion = ProcesoPostgreSql.Conexion("ConnectionStrings__Restauracion", opciones.Exigir("base-esperada"));
        if (conexion.Database == manifiesto.BaseOrigen)
            throw new ExcepcionRespaldo("restauracion_sobre_origen", "La restauración requiere una base diferente del origen.");
        var destino = RutasRespaldo.Absoluta(opciones.Exigir("destino-evidencias"));
        if (File.Exists(destino) || Directory.Exists(destino))
            throw new ExcepcionRespaldo("destino_ocupado", "La carpeta de evidencias de restauración no debe existir.");
        await using var baseDatos = new NpgsqlConnection(conexion.ConnectionString);
        await baseDatos.OpenAsync(cancelacion);
        await using (var comprobar = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname !~ '^pg_' AND n.nspname <> 'information_schema' AND c.relkind IN ('r','p'))", baseDatos))
            if (await comprobar.ExecuteScalarAsync(cancelacion) is true)
                throw new ExcepcionRespaldo("base_destino_ocupada", "La base de restauración debe estar vacía; no se borrará ni reemplazará información.");
        var temporal = destino + ".pendiente-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temporal);
        var carpeta = RutasRespaldo.Absoluta(opciones.Exigir("carpeta"));
        foreach (var evidencia in manifiesto.Evidencias)
        {
            var copia = RutasRespaldo.Resolver(temporal, evidencia.RutaRelativa);
            await RutasRespaldo.CopiarAsync(RutasRespaldo.Resolver(Path.Combine(carpeta, "evidencias"), evidencia.RutaRelativa), copia, cancelacion);
            if (await RutasRespaldo.HuellaAsync(copia, cancelacion) != evidencia.HuellaSha256)
                throw new ExcepcionRespaldo("copia_restauracion_alterada", "Una copia de evidencia cambió durante la restauración.");
        }
        await ProcesoPostgreSql.EjecutarAsync(opciones.Exigir("binarios"), "pg_restore",
            ["--dbname", conexion.Database!, "--exit-on-error", "--single-transaction", "--no-owner", "--no-acl", "--no-password",
                ], conexion, cancelacion, archivoEntrada: Path.Combine(carpeta, NombreBase));
        var referencias = await LeerReferenciasAsync(baseDatos, null, cancelacion);
        var migraciones = await LeerMigracionesAsync(baseDatos, null, cancelacion);
        var indiceReferencias = manifiesto.Evidencias.ToDictionary(e => e.RutaRelativa, StringComparer.Ordinal);
        if (!migraciones.SequenceEqual(manifiesto.Migraciones)
            || referencias.Count != manifiesto.Evidencias.Count
            || referencias.Any(r => !indiceReferencias.TryGetValue(r.RutaRelativa, out var evidencia)
                || !string.Equals(evidencia.HuellaSha256, r.HuellaSha256, StringComparison.OrdinalIgnoreCase)))
            throw new ExcepcionRespaldo("restauracion_inconsistente", "El historial o las referencias restauradas no coinciden con el respaldo.");
        Directory.Move(temporal, destino);
        return new("restaurar", destino, referencias.Count);
    }

    private static async Task<ManifiestoRespaldo> LeerManifiestoAsync(string carpeta, CancellationToken cancelacion)
    {
        var archivo = RutasRespaldo.Resolver(carpeta, NombreManifiesto);
        var manifiesto = JsonSerializer.Deserialize<ManifiestoRespaldo>(await File.ReadAllTextAsync(archivo, cancelacion));
        if (manifiesto is null || manifiesto.Version != 1 || manifiesto.Evidencias is null || manifiesto.Migraciones is null
            || manifiesto.CreadoUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(manifiesto.BaseOrigen))
            throw new ExcepcionRespaldo("manifiesto_invalido", "El manifiesto de respaldo no es válido.");
        return manifiesto;
    }

    private static async Task<IReadOnlyList<ArchivoEvidenciaRespaldo>> LeerReferenciasAsync(NpgsqlConnection conexion,
        NpgsqlTransaction? transaccion, CancellationToken cancelacion)
    {
        await using var comando = new NpgsqlCommand("SELECT ruta_archivo, hash_archivo FROM public.evidencias ORDER BY id_evidencia", conexion, transaccion);
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        var referencias = new List<ArchivoEvidenciaRespaldo>();
        while (await lector.ReadAsync(cancelacion))
        {
            if (lector.IsDBNull(1)) throw new ExcepcionRespaldo("evidencia_sin_huella", "Una evidencia histórica no tiene huella de integridad; requiere revisión antes del respaldo.");
            referencias.Add(new(lector.GetString(0), lector.GetString(1).Trim(), 0));
        }
        return referencias;
    }

    private static async Task<IReadOnlyList<string>> LeerMigracionesAsync(NpgsqlConnection conexion,
        NpgsqlTransaction? transaccion, CancellationToken cancelacion)
    {
        await using var comando = new NpgsqlCommand("SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"", conexion, transaccion);
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        var migraciones = new List<string>();
        while (await lector.ReadAsync(cancelacion)) migraciones.Add(lector.GetString(0));
        return migraciones;
    }

    private static async Task<ResultadoRespaldo> DepurarAsync(OpcionesRespaldos opciones, CancellationToken cancelacion)
    {
        var destino = RutasRespaldo.Absoluta(opciones.Exigir("destino"));
        var dias = opciones.Entero("retencion-dias");
        var minimo = opciones.Entero("conservar-minimo", 2);
        var carpetas = new List<(string Ruta, DateTime Fecha)>();
        _ = RutasRespaldo.Resolver(destino, "comprobacion");
        foreach (var carpeta in Directory.EnumerateDirectories(destino).Where(c => NombreCarpetaCompleta().IsMatch(Path.GetFileName(c))))
        {
            cancelacion.ThrowIfCancellationRequested();
            var configuracionVerificacion = opciones.Valores.ToDictionary(e => e.Key, e => e.Value);
            configuracionVerificacion["carpeta"] = carpeta;
            var manifiesto = await VerificarAsync(new("verificar", configuracionVerificacion), cancelacion);
            carpetas.Add((carpeta, manifiesto.CreadoUtc));
        }
        var eliminados = 0;
        foreach (var candidata in carpetas.OrderByDescending(c => c.Fecha).Skip(minimo).Where(c => DateTime.UtcNow - c.Fecha > TimeSpan.FromDays(dias)))
        {
            cancelacion.ThrowIfCancellationRequested();
            var ruta = Path.GetFullPath(candidata.Ruta);
            if (!RutasRespaldo.Contiene(destino, ruta) || Path.GetDirectoryName(ruta) != destino)
                throw new ExcepcionRespaldo("retencion_fuera_de_raiz", "Se rechazó una carpeta fuera del destino de respaldos.");
            AsegurarSinEnlaces(ruta);
            Directory.Delete(ruta, recursive: true);
            eliminados++;
        }
        return new("depurar", destino, 0, eliminados);
    }

    private static void AsegurarSinEnlaces(string carpeta)
    {
        if ((File.GetAttributes(carpeta) & FileAttributes.ReparsePoint) != 0)
            throw new ExcepcionRespaldo("retencion_con_enlace", "No se depuran carpetas con enlaces.");
        foreach (var entrada in Directory.EnumerateFileSystemEntries(carpeta))
        {
            var atributos = File.GetAttributes(entrada);
            if ((atributos & FileAttributes.ReparsePoint) != 0)
                throw new ExcepcionRespaldo("retencion_con_enlace", "No se depuran carpetas con enlaces.");
            if ((atributos & FileAttributes.Directory) != 0) AsegurarSinEnlaces(entrada);
        }
    }

    [GeneratedRegex("^respaldo-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex NombreCarpetaCompleta();
}
