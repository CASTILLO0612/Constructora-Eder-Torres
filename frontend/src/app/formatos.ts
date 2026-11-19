import { configuracion } from "./configuracion";

// Retira únicamente las etiquetas heredadas de los registros de demostración
// al mostrarlos. Los contratos, identificadores y valores editables conservan
// el contenido original recibido de la API.
export const textoRegistro = (valor: unknown) =>
    String(valor ?? "")
        .replace(/\s*·\s*simulaci[oó]n(?=\s*(?:·|$))/gi, "")
        .replace(/\s*·\s*ubicaci[oó]n ficticia de demostraci[oó]n$/i, "")
        .trim();
export const texto = (valor: unknown) => textoRegistro(valor) || "—";
export const nombrePersona = (fila: Record<string, unknown>) =>
    [
        fila.primerNombre,
        fila.segundoNombre,
        fila.primerApellido,
        fila.segundoApellido,
    ]
        .filter(Boolean)
        .join(" ");
export const etiqueta = (valor: string) =>
    ({
        DANADO: "Dañado",
        DANADA: "Dañada",
        DANO: "Daño",
        HIGIENE_SEGURIDAD: "Higiene y Seguridad",
        INGENIERO_RESIDENTE: "Ingeniero Residente",
        GERENTE_GENERAL: "Gerente General",
        GERENTE_OPERACIONES: "Gerente de Operaciones",
    })[valor] ||
    valor
        .toLowerCase()
        .replace(/_/g, " ")
        .replace(/^./, (x) => x.toUpperCase());
export const numero = (valor: unknown, decimales = 2) =>
    new Intl.NumberFormat("es-NI", { maximumFractionDigits: decimales }).format(
        Number(valor || 0),
    );
export function fecha(valor: unknown, hora = false) {
    if (!valor) return "—";
    const d = new Date(
        String(valor).length === 10 ? `${valor}T12:00:00Z` : String(valor),
    );
    if (!Number.isFinite(d.getTime())) return texto(valor);
    return new Intl.DateTimeFormat("es-NI", {
        timeZone: configuracion.zonaHoraria,
        day: "2-digit",
        month: "short",
        year: "numeric",
        ...(hora ? { hour: "2-digit", minute: "2-digit" } : {}),
    }).format(d);
}
export function fechaJornada(instante = new Date()) {
    const partes = new Intl.DateTimeFormat("en-CA", {
        timeZone: configuracion.zonaHoraria,
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
    }).formatToParts(instante);
    const parte = (tipo: string) => partes.find((x) => x.type === tipo)!.value;
    return `${parte("year")}-${parte("month")}-${parte("day")}`;
}
export function horaLocal(instante = new Date()) {
    return new Intl.DateTimeFormat("sv-SE", {
        timeZone: configuracion.zonaHoraria,
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hour12: false,
    })
        .format(instante)
        .replace(" ", "T");
}
export function localAUtc(valor: string) {
    const objetivo = Date.parse(
        `${valor.length === 16 ? valor + ":00" : valor}Z`,
    );
    if (!Number.isFinite(objetivo))
        throw new Error("La fecha y hora no son válidas.");
    let candidato = objetivo;
    for (let i = 0; i < 3; i++) {
        const partes = new Intl.DateTimeFormat("sv-SE", {
            timeZone: configuracion.zonaHoraria,
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            hour12: false,
        })
            .format(new Date(candidato))
            .replace(" ", "T");
        const proyectado = Date.parse(`${partes}Z`);
        candidato += objetivo - proyectado;
    }
    return new Date(candidato).toISOString();
}
