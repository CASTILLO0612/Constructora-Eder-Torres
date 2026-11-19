using Npgsql;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

public sealed class VerificadorArranqueProduccion(IServiceProvider servicios, IHostEnvironment entorno,
    OpcionesDespliegue opciones) : IHostedService
{
    public async Task StartAsync(CancellationToken cancelacion)
    {
        if (!entorno.IsProduction()) return;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(opciones.TiempoVerificacionArranque);
        await ValidarPermisosAsync(servicios.GetRequiredService<NpgsqlDataSource>(), limite.Token);
        _ = servicios.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
    }

    public static async Task ValidarPermisosAsync(NpgsqlDataSource origen, CancellationToken cancelacion = default)
    {
        const string consulta = """
            SELECT EXISTS (SELECT 1 FROM pg_roles r WHERE
                (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls)
                AND pg_has_role(current_user, r.oid, 'MEMBER'))
                OR has_schema_privilege(current_user, 'public', 'CREATE')
                OR has_database_privilege(current_user, current_database(), 'CREATE')
                OR EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'public' AND c.relkind IN ('r','p','S')
                    AND pg_has_role(current_user, c.relowner, 'MEMBER'))
                OR has_table_privilege(current_user, 'public.auditorias', 'UPDATE,DELETE,TRUNCATE')
            """;
        await using var conexion = await origen.OpenConnectionAsync(cancelacion);
        await using var comando = new NpgsqlCommand(consulta, conexion);
        if (await comando.ExecuteScalarAsync(cancelacion) is true)
            throw new InvalidOperationException("La cuenta PostgreSQL de producción tiene permisos administrativos, de propietario o de alteración de auditoría. Configure un rol limitado de ejecución.");
    }

    public Task StopAsync(CancellationToken cancelacion) => Task.CompletedTask;
}
