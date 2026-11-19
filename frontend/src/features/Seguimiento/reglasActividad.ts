import type { Fila } from "../Modulos/catalogo";
export function estadosActividadPermitidos(fila: Fila): string[] {
    const estado = String(fila.estado || "PLANIFICADA");
    const opciones: Record<string, string[]> = {
        PLANIFICADA: ["PLANIFICADA", "EN_PROCESO", "CANCELADA"],
        EN_PROCESO: ["EN_PROCESO", "RETRASADA", "COMPLETA"],
        RETRASADA: ["RETRASADA", "EN_PROCESO", "COMPLETA"],
        COMPLETA: ["COMPLETA"],
        CANCELADA: ["CANCELADA"],
    };
    return (opciones[estado] || [estado]).filter(
        (e) =>
            e !== "COMPLETA" ||
            estado === "COMPLETA" ||
            Number(fila.cantidadEjecutadaAcumulada) >=
                Number(fila.cantidadPlanificada),
    );
}
export function exigeJustificacion(
    esquema: string,
    datos: Fila,
    estadoActual?: string,
): boolean {
    return (
        (esquema === "SolicitudRegistrarAvance" &&
            (datos.esIncompletoORetrasado === true ||
                datos.estadoActividad === "RETRASADA" ||
                estadoActual === "RETRASADA")) ||
        (esquema === "SolicitudActualizarActividad" &&
            datos.estado === "RETRASADA")
    );
}
export function campoJustificacion(esquema: string): string {
    return esquema === "SolicitudActualizarActividad"
        ? "motivoVariacion"
        : "observaciones";
}
