using System.Data;
using System.Diagnostics;
using ConstructoraEderTorres.Aplicacion.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ConstructoraEderTorres.Infraestructura.Configuracion;
using Microsoft.EntityFrameworkCore.Storage;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

public sealed class UnidadTrabajoPostgreSql(
    ContextoConstructora contexto,
    ILogger<UnidadTrabajoPostgreSql> registro,
    OpcionesTransacciones opciones) : IUnidadTrabajo
{
    private readonly List<Func<CancellationToken, Task>> _compensaciones = [];

    public void AlRevertir(Func<CancellationToken, Task> compensacion)
    {
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("La compensación requiere una unidad de trabajo activa.");
        }

        _compensaciones.Add(compensacion);
    }

    public async Task<T> EjecutarAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        Func<T, bool> debeConfirmar,
        CancellationToken cancelacion = default,
        AislamientoOperacion aislamiento = AislamientoOperacion.Serializable)
    {
        if (contexto.Database.CurrentTransaction is not null)
        {
            return await operacion(cancelacion);
        }

        for (var intento = 1; ; intento++)
        {
            await using var transaccion = await contexto.Database.BeginTransactionAsync(
                aislamiento == AislamientoOperacion.LecturaConfirmada ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
                cancelacion);
            var confirmacionIniciada = false;
            var inicio = Stopwatch.GetTimestamp();
            var resultadoMedicion = "fallida";
            using var actividad = DiagnosticoAplicacion.Fuente.StartActivity("persistencia.transaccion");
            actividad?.SetTag("cet.aislamiento", aislamiento.ToString());
            try
            {
                var resultado = await operacion(cancelacion);
                if (debeConfirmar(resultado))
                {
                    confirmacionIniciada = true;
                    await transaccion.CommitAsync(cancelacion);
                    _compensaciones.Clear();
                }
                else
                {
                    await RevertirYCompensarAsync(transaccion);
                }

                contexto.ChangeTracker.Clear();
                resultadoMedicion = debeConfirmar(resultado) ? "confirmada" : "revertida";
                return resultado;
            }
            catch (Exception excepcion)
            {
                var postgres = BuscarErrorPostgreSql(excepcion);
                if (confirmacionIniciada && postgres is null)
                {
                    // La pérdida del acuse de COMMIT no demuestra una reversión en PostgreSQL.
                    _compensaciones.Clear();
                    contexto.ChangeTracker.Clear();
                    registro.LogWarning("No se pudo verificar el acuse de una confirmación; se conservan los archivos asociados.");
                    throw new ExcepcionPersistencia(new ErrorNegocio("confirmacion_no_verificable",
                        "No se pudo verificar la confirmación. Consulte el registro y reintente con el mismo UUID cuando corresponda.",
                        TipoError.NoDisponible), excepcion);
                }
                await RevertirYCompensarAsync(transaccion);
                contexto.ChangeTracker.Clear();

                var esReintentable = postgres?.SqlState is PostgresErrorCodes.SerializationFailure
                    or PostgresErrorCodes.DeadlockDetected
                    || (intento == 1 && postgres?.SqlState == PostgresErrorCodes.UniqueViolation);
                if (esReintentable && intento < opciones.MaximoIntentos)
                {
                    DiagnosticoAplicacion.ReintentosTransaccion.Add(1,
                        new KeyValuePair<string, object?>("causa", postgres?.SqlState));
                    await transaccion.DisposeAsync();
                    await Task.Delay(opciones.DemoraBaseReintento * intento, cancelacion);
                    continue;
                }

                if (postgres is not null && ConvertirError(postgres) is { } error)
                {
                    throw new ExcepcionPersistencia(error, excepcion);
                }

                throw;
            }
            finally
            {
                actividad?.SetTag("cet.resultado", resultadoMedicion);
                if (resultadoMedicion == "fallida") actividad?.SetStatus(ActivityStatusCode.Error);
                DiagnosticoAplicacion.DuracionTransaccion.Record(Stopwatch.GetElapsedTime(inicio).TotalSeconds,
                    new KeyValuePair<string, object?>("resultado", resultadoMedicion),
                    new KeyValuePair<string, object?>("aislamiento", aislamiento.ToString()));
            }
        }
    }

    private async Task RevertirYCompensarAsync(IDbContextTransaction transaccion)
    {
        try
        {
            using var limite = new CancellationTokenSource(opciones.TiempoMaximoReversion);
            await transaccion.RollbackAsync(limite.Token);
        }
        catch (Exception error)
        {
            registro.LogWarning(error, "No se pudo confirmar la reversión de la conexión; se ejecutará la compensación local.");
        }
        finally { await CompensarAsync(); }
    }

    private async Task CompensarAsync()
    {
        foreach (var compensacion in _compensaciones.AsEnumerable().Reverse())
        {
            try
            {
                await compensacion(CancellationToken.None);
            }
            catch (Exception error)
            {
                DiagnosticoAplicacion.FallosEvidencias.Add(1, new KeyValuePair<string, object?>("operacion", "compensacion"));
                registro.LogError(error, "No se pudo limpiar un archivo de una operación revertida.");
            }
        }

        _compensaciones.Clear();
    }

    private static PostgresException? BuscarErrorPostgreSql(Exception excepcion) =>
        excepcion is PostgresException postgres ? postgres
            : excepcion.InnerException is { } interna ? BuscarErrorPostgreSql(interna) : null;

    private static ErrorNegocio? ConvertirError(PostgresException error) => error.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => ErrorNegocio.Conflicto("registro_duplicado", "Ya existe un registro con los mismos datos únicos."),
        PostgresErrorCodes.ForeignKeyViolation => ErrorNegocio.Conflicto("referencia_invalida", "La operación contiene referencias inexistentes o registros todavía vinculados."),
        PostgresErrorCodes.CheckViolation or PostgresErrorCodes.RaiseException => ErrorNegocio.Conflicto("regla_datos_incumplida", "La operación no cumple las reglas de integridad o pertenencia a la misma obra."),
        PostgresErrorCodes.NotNullViolation or PostgresErrorCodes.StringDataRightTruncation or PostgresErrorCodes.NumericValueOutOfRange => ErrorNegocio.Validacion("datos_fuera_de_limites", "Revise los campos obligatorios, las longitudes y los valores numéricos de la solicitud."),
        PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => ErrorNegocio.Conflicto("operacion_concurrente", "Otro usuario modificó estos datos. Actualice la información y reintente la operación."),
        _ => null
    };
}
