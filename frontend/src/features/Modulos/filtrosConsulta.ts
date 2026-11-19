import { horaLocal, localAUtc } from "../../app/formatos";

export function fechaFiltroLocal(valor: string): string {
    if (/(?:Z|[+-]\d{2}:\d{2})$/i.test(valor)) {
        const fecha = new Date(valor);
        if (Number.isFinite(fecha.getTime()))
            return horaLocal(fecha).slice(0, 16);
    }
    return valor;
}

export function prepararFiltrosConsulta(filtros: Record<string, unknown>): {
    parametros: Record<string, unknown>;
    error?: string;
} {
    const parametros = { ...filtros };
    for (const campo of ["desdeUtc", "hastaUtc"] as const) {
        const valor = filtros[campo];
        if (!valor) {
            delete parametros[campo];
            continue;
        }
        try {
            const local = String(valor);
            if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(local))
                throw new Error();
            const instante = localAUtc(local);
            if (horaLocal(new Date(instante)).slice(0, 16) !== local)
                throw new Error();
            // El selector trabaja por minutos; incluye todo el minuto final.
            parametros[campo] =
                campo === "hastaUtc"
                    ? new Date(Date.parse(instante) + 59_999).toISOString()
                    : instante;
        } catch {
            return {
                parametros,
                error: `Revisa la fecha y la hora de «${campo === "desdeUtc" ? "Desde" : "Hasta"}».`,
            };
        }
    }
    if (
        parametros.desdeUtc &&
        parametros.hastaUtc &&
        Date.parse(String(parametros.hastaUtc)) <
            Date.parse(String(parametros.desdeUtc))
    )
        return {
            parametros,
            error: "La fecha «Hasta» debe ser igual o posterior a «Desde».",
        };
    return { parametros };
}
