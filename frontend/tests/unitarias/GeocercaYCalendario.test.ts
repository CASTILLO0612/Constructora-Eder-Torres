import { describe, it, expect } from "vitest";
import {
    distanciaMetros,
    validarLectura,
} from "../../src/features/Asistencia/geocerca";
import { fechaJornada, localAUtc } from "../../src/app/formatos";
describe("Captura con GPS y calendario de Managua", () => {
    it("conserva la fecha local al cruzar medianoche UTC", () => {
        expect(fechaJornada(new Date("2026-11-01T03:00:00Z"))).toBe(
            "2026-10-31",
        );
        expect(localAUtc("2026-10-31T21:00")).toBe("2026-11-01T03:00:00.000Z");
    });
    it("calcula distancias y considera el cambio de meridiano", () => {
        expect(distanciaMetros(0, 0, 0, 1)).toBeCloseTo(111194.93, 0);
        expect(distanciaMetros(0, 179.999, 0, -179.999)).toBeLessThan(225);
    });
    it("bloquea un círculo de incertidumbre que cruza el borde", () => {
        const l = {
            latitud: 0,
            longitud: 0.0008,
            precision: 12,
            capturadaEn: "",
        };
        expect(validarLectura(l, 0, 0, 100).habilitada).toBe(false);
        expect(
            validarLectura({ ...l, precision: 10 }, 0, 0, 100).habilitada,
        ).toBe(true);
    });
    it("rechaza coordenadas y precisiones inválidas", () => {
        for (const precision of [-1, NaN, Infinity])
            expect(
                validarLectura(
                    { latitud: 0, longitud: 0, precision, capturadaEn: "" },
                    0,
                    0,
                    100,
                ).habilitada,
            ).toBe(false);
        expect(
            validarLectura(
                { latitud: 100, longitud: 0, precision: 0, capturadaEn: "" },
                0,
                0,
                100,
            ).habilitada,
        ).toBe(false);
    });
    it("redondea la precisión hacia arriba antes de habilitar", () => {
        expect(
            validarLectura(
                {
                    latitud: 0,
                    longitud: 0,
                    precision: 100.0001,
                    capturadaEn: "",
                },
                0,
                0,
                100,
            ).habilitada,
        ).toBe(false);
    });
});
