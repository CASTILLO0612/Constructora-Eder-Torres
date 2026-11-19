using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ConstructoraEderTorres.InspeccionEsquema;

internal static class InspectorEsquemaPostgreSql
{
    public static async Task<JsonDocument> InspeccionarAsync(
        string cadenaConexion,
        string esquema,
        CancellationToken cancelacion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(esquema);

        var configuracion = new NpgsqlConnectionStringBuilder(cadenaConexion)
        {
            ApplicationName = typeof(InspectorEsquemaPostgreSql).Assembly.GetName().Name,
            IncludeErrorDetail = false,
            PersistSecurityInfo = false,
            Enlist = false,
            Pooling = false,
            Multiplexing = false,
            SearchPath = "pg_catalog",
            Options = "-c default_transaction_read_only=on"
        };

        await using var origen = NpgsqlDataSource.Create(configuracion.ConnectionString);
        await using var conexion = await origen.OpenConnectionAsync(cancelacion);
        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancelacion);

        await using (var establecerLectura = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY", conexion, transaccion))
        {
            await establecerLectura.ExecuteNonQueryAsync(cancelacion);
        }

        await using var recurso = typeof(InspectorEsquemaPostgreSql).Assembly
            .GetManifestResourceStream("ConsultaEsquema.sql")
            ?? throw new InvalidOperationException("No se encontró la consulta de inspección.");
        using var lectorConsulta = new StreamReader(recurso);
        var consulta = await lectorConsulta.ReadToEndAsync(cancelacion);

        await using var comando = new NpgsqlCommand(consulta, conexion, transaccion);
        comando.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Text,
            Value = esquema
        });

        var contenido = await comando.ExecuteScalarAsync(cancelacion) as string
            ?? throw new InvalidOperationException("PostgreSQL no devolvió el inventario.");
        await transaccion.RollbackAsync(cancelacion);

        var inventario = JsonDocument.Parse(contenido);
        var raiz = inventario.RootElement;
        if (!raiz.GetProperty("soloLectura").GetBoolean()
            || !raiz.GetProperty("esquemaExiste").GetBoolean()
            || !raiz.GetProperty("puedeUsarEsquema").GetBoolean())
        {
            inventario.Dispose();
            throw new InvalidOperationException(
                "El esquema no existe, falta permiso USAGE o no se pudo garantizar solo lectura.");
        }

        return inventario;
    }
}
