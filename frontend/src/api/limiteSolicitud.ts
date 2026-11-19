import { configuracion } from "../app/configuracion";

// AbortController permite conservar cancelación y plazo en navegadores que
// todavía no incluyen los métodos estáticos AbortSignal.any/timeout.
export async function conLimiteSolicitud<T>(
    origen: AbortSignal | undefined,
    operacion: (signal: AbortSignal) => Promise<T>,
): Promise<T> {
    const controlador = new AbortController();
    let plazoAgotado = false;
    const cancelarOrigen = () => controlador.abort(origen?.reason);
    if (origen?.aborted)
        throw (
            origen.reason ||
            new DOMException("La consulta se canceló.", "AbortError")
        );
    origen?.addEventListener("abort", cancelarOrigen, { once: true });
    const plazo = window.setTimeout(() => {
        plazoAgotado = true;
        controlador.abort();
    }, configuracion.esperaSolicitudMs);
    try {
        return await operacion(controlador.signal);
    } catch (error) {
        if (plazoAgotado)
            throw new DOMException(
                "El servidor tardó demasiado en responder. Vuelve a intentarlo.",
                "TimeoutError",
            );
        throw error;
    } finally {
        clearTimeout(plazo);
        origen?.removeEventListener("abort", cancelarOrigen);
    }
}
