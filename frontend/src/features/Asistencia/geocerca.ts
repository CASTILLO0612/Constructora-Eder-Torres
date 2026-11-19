import { configuracion } from "../../app/configuracion";
export type LecturaGps = {
    latitud: number;
    longitud: number;
    precision: number;
    capturadaEn: string;
};
export function distanciaMetros(
    latitud1: number,
    longitud1: number,
    latitud2: number,
    longitud2: number,
) {
    const rad = (n: number) => (n * Math.PI) / 180;
    const a =
        Math.sin(rad(latitud2 - latitud1) / 2) ** 2 +
        Math.cos(rad(latitud1)) *
            Math.cos(rad(latitud2)) *
            Math.sin(rad(longitud2 - longitud1) / 2) ** 2;
    return (
        6_371_000 *
        2 *
        Math.atan2(Math.sqrt(Math.min(1, a)), Math.sqrt(Math.max(0, 1 - a)))
    );
}
export function validarLectura(
    lectura: LecturaGps,
    latitud: number,
    longitud: number,
    radio: number,
) {
    const distancia = distanciaMetros(
        lectura.latitud,
        lectura.longitud,
        latitud,
        longitud,
    );
    const precision = Math.ceil(lectura.precision * 100) / 100;
    const coordenadasValidas =
        [lectura.latitud, lectura.longitud, latitud, longitud].every(
            Number.isFinite,
        ) &&
        Math.abs(lectura.latitud) <= 90 &&
        Math.abs(latitud) <= 90 &&
        Math.abs(lectura.longitud) <= 180 &&
        Math.abs(longitud) <= 180;
    return {
        distancia,
        precision,
        habilitada:
            coordenadasValidas &&
            Number.isFinite(radio) &&
            radio > 0 &&
            Number.isFinite(precision) &&
            precision >= 0 &&
            distancia + precision <= radio,
    };
}
export function obtenerGps(): Promise<LecturaGps> {
    return new Promise((resolver, rechazar) => {
        if (!navigator.geolocation) {
            rechazar(
                new Error(
                    "Este dispositivo o navegador no permite obtener ubicación.",
                ),
            );
            return;
        }
        navigator.geolocation.getCurrentPosition(
            (p) =>
                resolver({
                    latitud: p.coords.latitude,
                    longitud: p.coords.longitude,
                    precision: p.coords.accuracy,
                    capturadaEn: new Date(p.timestamp).toISOString(),
                }),
            (e) => {
                rechazar(
                    new Error(
                        e.code === 1
                            ? "Habilita el permiso de ubicación para registrar asistencia."
                            : e.code === 3
                              ? "La ubicación tardó demasiado. Busca un lugar con mejor señal e inténtalo de nuevo."
                              : "No se obtuvo una ubicación válida. Verifica el GPS del dispositivo.",
                    ),
                );
            },
            {
                enableHighAccuracy: true,
                maximumAge: 0,
                timeout: configuracion.esperaGpsMs,
            },
        );
    });
}
