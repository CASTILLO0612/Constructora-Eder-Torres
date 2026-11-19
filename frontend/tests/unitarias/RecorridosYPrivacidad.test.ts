import { expect, it } from "vitest";
import {
    estadosActividadPermitidos,
    exigeJustificacion,
} from "../../src/features/Seguimiento/reglasActividad";
import {
    resumirOperacion,
    guardarOperacion,
    obtenerOperacion,
    type OperacionPendiente,
} from "../../src/offline/almacenamiento";

it("impide saltar de planificada a completa y mantiene terminales sin reapertura", () => {
    expect(
        estadosActividadPermitidos({
            estado: "PLANIFICADA",
            cantidadEjecutadaAcumulada: 10,
            cantidadPlanificada: 10,
        }),
    ).not.toContain("COMPLETA");
    expect(
        estadosActividadPermitidos({
            estado: "EN_PROCESO",
            cantidadEjecutadaAcumulada: 9,
            cantidadPlanificada: 10,
        }),
    ).not.toContain("COMPLETA");
    expect(
        estadosActividadPermitidos({
            estado: "EN_PROCESO",
            cantidadEjecutadaAcumulada: 10,
            cantidadPlanificada: 10,
        }),
    ).toContain("COMPLETA");
    expect(estadosActividadPermitidos({ estado: "COMPLETA" })).toEqual([
        "COMPLETA",
    ]);
    expect(estadosActividadPermitidos({ estado: "CANCELADA" })).toEqual([
        "CANCELADA",
    ]);
});
it("exige motivo en capturas incompletas y en actividades ya retrasadas", () => {
    expect(
        exigeJustificacion("SolicitudRegistrarAvance", {
            esIncompletoORetrasado: true,
        }),
    ).toBe(true);
    expect(
        exigeJustificacion("SolicitudRegistrarAvance", {}, "RETRASADA"),
    ).toBe(true);
    expect(
        exigeJustificacion("SolicitudRegistrarAvance", {}, "EN_PROCESO"),
    ).toBe(false);
});
it("minimiza datos confirmados y conserva íntegra una captura pendiente", async () => {
    const o: OperacionPendiente = {
        idLocal: crypto.randomUUID(),
        idUsuario: 14,
        coleccion: "asistencias",
        titulo: "Entrada",
        datos: {
            uuidRegistro: crypto.randomUUID(),
            idAsignacion: 20,
            latitudEntrada: 12,
            longitudEntrada: -86,
            precisionGpsEntradaMetros: 5,
            observaciones: "Información personal",
            contenidoArchivo: "bytes",
        },
        creadoEn: new Date().toISOString(),
        estado: "PENDIENTE",
        intentos: 0,
        proximoIntento: 0,
    };
    await guardarOperacion(o);
    expect((await obtenerOperacion(o.idLocal))!.datos).toEqual(o.datos);
    await guardarOperacion({ ...o, estado: "CONFIRMADA", idServidor: "44" });
    const confirmada = (await obtenerOperacion(o.idLocal))!;
    expect(confirmada.datos).toEqual({
        uuidRegistro: o.datos.uuidRegistro,
        idAsignacion: 20,
    });
    expect(confirmada.idServidor).toBe("44");
    expect(
        resumirOperacion({
            ...o,
            estado: "CONFIRMADA",
            coleccion: "asistenciaCuadrilla",
            datos: {
                marcaciones: [
                    {
                        idAsignacion: 20,
                        uuidRegistro: "identidad-original",
                        entradaEn: "hora",
                        latitud: 12,
                    },
                ],
            },
        }).datos,
    ).toEqual({
        marcaciones: [{ idAsignacion: 20, uuidRegistro: "identidad-original" }],
    });
});
