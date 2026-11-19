import { describe, expect, it } from "vitest";
import type { RespuestaAsistencia } from "../../src/api/contratos";
import {
    agruparAsistencia,
    desplazarFecha,
    porcentajeActividad,
} from "../../src/features/Inicio/datosGraficos";

const asistencia = (
    datos: Partial<RespuestaAsistencia>,
): RespuestaAsistencia => ({
    idAsistencia: 1,
    idAsignacion: 1,
    idTrabajador: 1,
    nombreCompletoTrabajador: "Persona de prueba",
    idObra: 1,
    fechaJornada: "2026-10-31",
    entradaEn: "2026-10-31T22:00:00Z",
    salidaEn: "2026-11-01T06:00:00Z",
    latitudEntrada: null,
    longitudEntrada: null,
    latitudSalida: null,
    longitudSalida: null,
    estado: "COMPLETA",
    idUsuarioRegistro: 1,
    uuidRegistro: "prueba-grafico",
    creadoEn: null,
    sincronizadoEn: null,
    ...datos,
});

describe("Gráficos calculados desde las respuestas de la API", () => {
    it("conserva días de calendario al cambiar de mes, año y año bisiesto", () => {
        expect(desplazarFecha("2026-01-01", -1)).toBe("2025-12-31");
        expect(desplazarFecha("2024-03-01", -1)).toBe("2024-02-29");
        expect(desplazarFecha("2026-12-31", 1)).toBe("2027-01-01");
    });
    it("agrupa por fecha de jornada y conserva días vacíos sin atribuirlos a ausencias", () => {
        const resultado = agruparAsistencia(
            [
                asistencia({}),
                asistencia({
                    idAsistencia: 2,
                    fechaJornada: "2026-10-31",
                    salidaEn: "2026-11-01T02:00:00Z",
                }),
                asistencia({ fechaJornada: "2026-10-29" }),
            ],
            "2026-11-01",
            3,
        );
        expect(resultado).toEqual([
            { fecha: "2026-10-30", marcaciones: 0, horas: 0 },
            { fecha: "2026-10-31", marcaciones: 2, horas: 12 },
            { fecha: "2026-11-01", marcaciones: 0, horas: 0 },
        ]);
    });
    it("no suma horas de jornadas abiertas, anuladas o con instantes inválidos", () => {
        const resultado = agruparAsistencia(
            [
                asistencia({ salidaEn: null, estado: "ABIERTA" }),
                asistencia({ entradaEn: null }),
                asistencia({ estado: "ANULADA" }),
                asistencia({ salidaEn: "2026-10-31T20:00:00Z" }),
                asistencia({ entradaEn: "fecha-invalida" }),
                asistencia({
                    salidaEn: "2026-10-31T22:20:00Z",
                    estado: "CORREGIDA",
                }),
            ],
            "2026-10-31",
            1,
        );
        expect(resultado[0].marcaciones).toBe(6);
        expect(resultado[0].horas).toBe(0.33);
    });
    it("protege la escala de actividades ante cantidades nulas y no finitas", () => {
        expect(porcentajeActividad(25, 100)).toBe(25);
        expect(porcentajeActividad(120, 100)).toBe(100);
        expect(porcentajeActividad(-5, 100)).toBe(0);
        for (const [ejecutada, planificada] of [
            [10, 0],
            [NaN, 20],
            [20, Infinity],
            [Infinity, 20],
        ])
            expect(porcentajeActividad(ejecutada, planificada)).toBe(0);
    });
});
