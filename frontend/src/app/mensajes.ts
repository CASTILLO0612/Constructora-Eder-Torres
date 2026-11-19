const mensajeGeneral =
    "No se pudo completar la operación. Vuelve a intentarlo.";

// Los datos originales de diagnóstico se conservan fuera de la presentación.
export function mensajeParaUsuario(
    valor: unknown,
    alternativa = mensajeGeneral,
): string {
    if (typeof valor !== "string" || !valor.trim()) return alternativa;
    const mensaje = valor.trim();
    if (/antifalsifica|\bcsrf\b|\btoken\b|\bcookie\b/i.test(mensaje))
        return "Tu sesión necesita actualizarse. Inicia sesión nuevamente.";
    if (/\buuid\b|idempoten/i.test(mensaje))
        return "No se pudo confirmar esta captura. Revisa tus registros antes de volver a enviarla.";
    if (/identificador.*mayor que cero/i.test(mensaje))
        return "Selecciona una opción válida.";
    if (/\butc\b|sufijo z/i.test(mensaje))
        return "Revisa la fecha y la hora indicadas.";
    if (/dos decimales conforme al esquema de datos/i.test(mensaje))
        return "El campo admite como máximo dos decimales.";
    if (
        /^(Unauthorized|Forbidden|Not Found|Bad Request|Internal Server Error|One or more validation errors occurred)[.!]?$/i.test(
            mensaje,
        )
    )
        return alternativa;
    if (
        /Failed to (?:fetch|execute)|The (?:operation|request) (?:failed|was aborted)|\b(?:Id|id|uuid)[A-Z]\w*\b|\b[a-z]+(?:_[a-z]+)+\b/.test(
            mensaje,
        )
    )
        return alternativa;
    if (
        /\b(api|servidor|servidores|json|http\w*|sql\w*|postgres\w*|npgsql|indexeddb|base de datos|esquema de datos|acuse|retry-after|stack\s?trace|traceidentifier)\b|\bSystem\.[A-Z]|\b\w*Exception\b|\/(?:api|salud)\/|https?:\/\/|[A-Z]:\\|\b[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}\b/i.test(
            mensaje,
        )
    )
        return alternativa;
    return mensaje;
}

export function mensajeEstado(estado: number): string {
    if (estado === 401) return "Inicia sesión nuevamente para continuar.";
    if (estado === 403)
        return "Tu cuenta no tiene permiso para realizar esta acción.";
    if (estado === 404)
        return "No se encontró el registro solicitado. Actualiza la página.";
    if (estado === 409)
        return "El registro cambió o ya existe. Actualiza los datos antes de continuar.";
    if (estado === 413)
        return "El archivo es demasiado grande. Selecciona uno de menor tamaño.";
    if (estado === 429)
        return "Espera unos momentos antes de volver a intentarlo.";
    if (estado >= 500)
        return "No se pudo completar la operación. Inténtalo en unos momentos.";
    if (estado === 400) return "Revisa los datos e inténtalo nuevamente.";
    return mensajeGeneral;
}
