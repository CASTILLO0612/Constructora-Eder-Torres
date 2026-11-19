import type { RespuestaAsistencia } from "../../api/contratos";

export type PuntoAsistencia = {
    fecha: string;
    marcaciones: number;
    horas: number;
};
export function desplazarFecha(fecha: string, dias: number) {
    const instante = new Date(`${fecha}T12:00:00.000Z`);
    instante.setUTCDate(instante.getUTCDate() + dias);
    return instante.toISOString().slice(0, 10);
}
export function agruparAsistencia(
    registros: RespuestaAsistencia[],
    hasta: string,
    dias: number,
): PuntoAsistencia[] {
    const puntos = Array.from({ length: dias }, (_, i) => ({
        fecha: desplazarFecha(hasta, i - dias + 1),
        marcaciones: 0,
        horas: 0,
    }));
    const fechas = new Map(puntos.map((p) => [p.fecha, p]));
    for (const r of registros) {
        const punto = fechas.get(r.fechaJornada);
        if (!punto) continue;
        punto.marcaciones++;
        if (r.entradaEn && r.salidaEn && r.estado !== "ANULADA") {
            const duracion =
                (Date.parse(r.salidaEn) - Date.parse(r.entradaEn)) / 3_600_000;
            if (Number.isFinite(duracion) && duracion > 0)
                punto.horas += duracion;
        }
    }
    return puntos.map((p) => ({
        ...p,
        horas: Math.round(p.horas * 100) / 100,
    }));
}
export function porcentajeActividad(ejecutada: number, planificada: number) {
    return planificada > 0 &&
        Number.isFinite(ejecutada) &&
        Number.isFinite(planificada)
        ? Math.min(100, Math.max(0, (ejecutada / planificada) * 100))
        : 0;
}
