namespace ConstructoraEderTorres.Aplicacion.Comun;

public enum AislamientoOperacion { Serializable, LecturaConfirmada }

/// <summary>Confirma una operación completa o revierte todos sus cambios, incluida su auditoría.</summary>
public interface IUnidadTrabajo
{
    Task<T> EjecutarAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        Func<T, bool> debeConfirmar,
        CancellationToken cancelacion = default,
        AislamientoOperacion aislamiento = AislamientoOperacion.Serializable);

    void AlRevertir(Func<CancellationToken, Task> compensacion);
}

public sealed class ExcepcionPersistencia(ErrorNegocio error, Exception? causa = null)
    : Exception(error.Mensaje, causa)
{
    public ErrorNegocio Error { get; } = error;
}
