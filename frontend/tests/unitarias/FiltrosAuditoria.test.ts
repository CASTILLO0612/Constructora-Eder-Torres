import { expect, it } from "vitest";
import {
    fechaFiltroLocal,
    prepararFiltrosConsulta,
} from "../../src/features/Modulos/filtrosConsulta";

it("consulta un día completo de Managua y conserva el resto de los filtros", () => {
    expect(
        prepararFiltrosConsulta({
            desdeUtc: "2026-11-19T00:00",
            hastaUtc: "2026-11-19T23:59",
            entidad: "obras",
            idUsuario: 7,
        }),
    ).toEqual({
        parametros: {
            desdeUtc: "2026-11-19T06:00:00.000Z",
            hastaUtc: "2026-11-20T05:59:59.999Z",
            entidad: "obras",
            idUsuario: 7,
        },
    });
});
it.each([
    [
        { desdeUtc: "2026-11-19T08:00" },
        { desdeUtc: "2026-11-19T14:00:00.000Z" },
    ],
    [
        { hastaUtc: "2026-11-19T10:15" },
        { hastaUtc: "2026-11-19T16:15:59.999Z" },
    ],
    [{ desdeUtc: "", hastaUtc: "", estado: "ACTIVA" }, { estado: "ACTIVA" }],
])("admite límites abiertos y campos vacíos: %j", (entrada, parametros) => {
    expect(prepararFiltrosConsulta(entrada)).toEqual({ parametros });
});
it("rechaza rangos invertidos y admite un mismo minuto como inicio y final", () => {
    expect(
        prepararFiltrosConsulta({
            desdeUtc: "2026-11-19T10:00",
            hastaUtc: "2026-11-19T09:59",
        }).error,
    ).toBe("La fecha «Hasta» debe ser igual o posterior a «Desde».");
    expect(
        prepararFiltrosConsulta({
            desdeUtc: "2026-11-19T10:00",
            hastaUtc: "2026-11-19T10:00",
        }).error,
    ).toBeUndefined();
});
it.each(["fecha-invalida", "2026-02-30T10:00"])(
    "no normaliza silenciosamente la fecha incorrecta %s",
    (desdeUtc) => {
        expect(prepararFiltrosConsulta({ desdeUtc }).error).toBe(
            "Revisa la fecha y la hora de «Desde».",
        );
    },
);
it("presenta en hora local los filtros recibidos en un enlace", () => {
    expect(fechaFiltroLocal("2026-11-19T14:30:00Z")).toBe("2026-11-19T08:30");
    expect(fechaFiltroLocal("2026-11-19T08:30")).toBe("2026-11-19T08:30");
});
