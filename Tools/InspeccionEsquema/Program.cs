using System.Text.Json;
using ConstructoraEderTorres.InspeccionEsquema;
using Microsoft.Extensions.Configuration;
using Npgsql;

if (args is ["--ayuda"])
{
    MostrarAyuda();
    return 0;
}

var argumentos = new Dictionary<string, string>(StringComparer.Ordinal);
if (args.Length != 4)
{
    MostrarAyuda();
    return 2;
}

for (var indice = 0; indice < args.Length; indice += 2)
{
    if (args[indice] is not ("--esquema" or "--salida")
        || string.IsNullOrWhiteSpace(args[indice + 1])
        || !argumentos.TryAdd(args[indice], args[indice + 1]))
    {
        MostrarAyuda();
        return 2;
    }
}

using var cancelacion = new CancellationTokenSource();
Console.CancelKeyPress += (_, evento) =>
{
    evento.Cancel = true;
    cancelacion.Cancel();
};

string? archivoTemporal = null;
try
{
    var salida = Path.GetFullPath(argumentos["--salida"]);
    if (File.Exists(salida) || Directory.Exists(salida))
    {
        Console.Error.WriteLine("La salida ya existe. Elige otro nombre para conservar la evidencia anterior.");
        return 2;
    }

    var configuracion = new ConfigurationBuilder()
        .AddUserSecrets(typeof(InspectorEsquemaPostgreSql).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();
    var cadenaConexion = configuracion.GetConnectionString("Constructora");
    if (string.IsNullOrWhiteSpace(cadenaConexion))
    {
        Console.Error.WriteLine(
            "Configura ConnectionStrings:Constructora en secretos de usuario o ConnectionStrings__Constructora en el entorno.");
        return 2;
    }

    using var inventario = await InspectorEsquemaPostgreSql.InspeccionarAsync(
        cadenaConexion, argumentos["--esquema"], cancelacion.Token);
    var carpeta = Path.GetDirectoryName(salida)!;
    Directory.CreateDirectory(carpeta);
    archivoTemporal = Path.Combine(carpeta, $".{Path.GetFileName(salida)}.{Guid.NewGuid():N}.tmp");

    await using (var archivo = new FileStream(
        archivoTemporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        await JsonSerializer.SerializeAsync(
            archivo, inventario.RootElement,
            new JsonSerializerOptions { WriteIndented = true }, cancelacion.Token);
    }

    File.Move(archivoTemporal, salida, overwrite: false);
    archivoTemporal = null;
    Console.WriteLine($"Inventario de solo lectura guardado en: {salida}");
    Console.WriteLine($"Tablas inspeccionadas: {inventario.RootElement.GetProperty("tablas").GetArrayLength()}.");
    Console.WriteLine("La conciliación requiere revisar este inventario contra el DDL y los cambios posteriores.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Inspección cancelada.");
    return 130;
}
catch (PostgresException error)
{
    Console.Error.WriteLine($"PostgreSQL rechazó la inspección (SQLSTATE {error.SqlState}). No se generó un inventario válido.");
    return 1;
}
catch (NpgsqlException)
{
    Console.Error.WriteLine("No se pudo inspeccionar PostgreSQL. Revisa la conexión, las credenciales y la disponibilidad.");
    return 1;
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException
    or IOException or UnauthorizedAccessException or FormatException or JsonException)
{
    Console.Error.WriteLine("No se pudo completar la inspección. Revisa el esquema, la configuración y la ruta de salida.");
    return 1;
}
finally
{
    if (archivoTemporal is not null)
    {
        try
        {
            File.Delete(archivoTemporal);
        }
        catch (IOException)
        {
            Console.Error.WriteLine("No se pudo retirar el archivo temporal de la inspección incompleta.");
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("No se pudo retirar el archivo temporal de la inspección incompleta.");
        }
    }
}

static void MostrarAyuda()
{
    Console.WriteLine("Inspección del esquema PostgreSQL de Constructora Eder Torres.");
    Console.WriteLine("Uso: dotnet run --project Tools/InspeccionEsquema -- --esquema public --salida artifacts/esquema-postgresql.json");
    Console.WriteLine("Usa la conexión del backend y consulta únicamente metadatos. Requiere un archivo de salida nuevo.");
}
