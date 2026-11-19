import { it, expect, vi } from "vitest";
import { api, ErrorApi } from "../../src/api/cliente";
import {
    encolar,
    sincronizar,
    reintentarOperacion,
    establecerPropietarioSincronizacion,
} from "../../src/offline/sincronizacion";
import {
    baseLocal,
    listarOperaciones,
    guardarConsulta,
    leerConsulta,
    limpiarConsultas,
    listarResumenes,
    obtenerOperacion,
} from "../../src/offline/almacenamiento";

function autorizar(idUsuario: number) {
    establecerPropietarioSincronizacion(idUsuario);
    vi.spyOn(api, "leer").mockResolvedValue({ idUsuario } as never);
}

it("resuelve el ID confirmado del avance antes de enviar la evidencia dependiente", async () => {
    autorizar(1);
    const avance = await encolar(1, "avances", "Avance", {
        idActividad: 3,
        cantidadEjecutada: 2,
    });
    await encolar(
        1,
        "evidencias",
        "Foto",
        { idAvance: 0, contenidoArchivo: "Ynl0ZXM=" },
        undefined,
        { idLocal: avance.idLocal, campo: "idAvance" },
    );
    const enviar = vi
        .spyOn(api, "escribir")
        .mockImplementation(async (_m, _r, datos) => {
            const lote = datos as Record<string, Record<string, unknown>[]>;
            const nombre = Object.keys(lote)[0];
            if (nombre === "evidencias")
                expect(lote.evidencias[0]).toMatchObject({
                    idAvance: 91,
                    contenidoArchivo: "Ynl0ZXM=",
                });
            return {
                acuses: [
                    {
                        estadoAcuse: "SINCRONIZADO",
                        idRegistroServidor: nombre === "avances" ? "91" : "92",
                    },
                ],
            } as never;
        });
    await sincronizar(1);
    expect(enviar).toHaveBeenCalledTimes(2);
    const operaciones = await listarOperaciones(1);
    expect(operaciones.every((o) => o.estado === "CONFIRMADA")).toBe(true);
    expect(operaciones[1].datos.contenidoArchivo).toBeUndefined();
});
it("conserva datos y UUID ante un acuse perdido y reenvía sin crear otra identidad", async () => {
    autorizar(2);
    const o = await encolar(2, "asistencias", "Entrada", { idAsignacion: 4 });
    const enviado = vi
        .spyOn(api, "escribir")
        .mockRejectedValueOnce(
            new ErrorApi(503, { codigo: "confirmacion_no_verificable" }),
        )
        .mockResolvedValueOnce({
            acuses: [
                { estadoAcuse: "IDEMPOTENTE_PREVIO", idRegistroServidor: "35" },
            ],
        } as never);
    await sincronizar(2);
    let actual = (await listarOperaciones(2))[0];
    expect(actual.estado).toBe("PENDIENTE");
    expect(actual.datos.uuidRegistro).toBe(o.datos.uuidRegistro);
    await reintentarOperacion(actual);
    await sincronizar(2);
    actual = (await listarOperaciones(2))[0];
    expect(actual.estado).toBe("CONFIRMADA");
    expect(enviado.mock.calls[1][2]).toMatchObject({
        asistencias: [{ uuidRegistro: o.datos.uuidRegistro }],
    });
});
it("un rechazo no se borra ni se reintenta automáticamente", async () => {
    autorizar(3);
    await encolar(3, "entregasEpp", "Entrega", { idEpp: 8 });
    const envio = vi.spyOn(api, "escribir").mockResolvedValue({
        acuses: [
            {
                estadoAcuse: "RECHAZADO_VALIDACION",
                codigoError: "equipo_no_disponible",
                mensajeError: "El equipo ya no está disponible.",
            },
        ],
    } as never);
    await sincronizar(3);
    await sincronizar(3);
    expect(envio).toHaveBeenCalledTimes(1);
    expect((await listarOperaciones(3))[0]).toMatchObject({
        estado: "ATENCION",
        codigo: "equipo_no_disponible",
    });
});
it("respeta el propietario y separa las consultas entre usuarios", async () => {
    autorizar(11);
    await encolar(10, "avances", "Otro usuario", { idActividad: 2 });
    const envio = vi.spyOn(api, "escribir");
    await sincronizar(11);
    expect(envio).not.toHaveBeenCalled();
    await guardarConsulta(10, "/api/obras", [{ nombre: "Privada" }]);
    expect(await leerConsulta(11, "/api/obras")).toBeUndefined();
    await limpiarConsultas(10);
    expect(await leerConsulta(10, "/api/obras")).toBeUndefined();
    expect((await listarOperaciones(10)).length).toBe(1);
});
it.each(["asistenciaCuadrilla", "salidasCuadrilla"] as const)(
    "reenvía %s por su endpoint atómico sin campos fuera de contrato",
    async (coleccion) => {
        autorizar(4);
        const datos =
            coleccion === "asistenciaCuadrilla"
                ? {
                      idObra: 1,
                      marcaciones: [
                          {
                              idAsignacion: 4,
                              uuidRegistro: crypto.randomUUID(),
                          },
                      ],
                  }
                : {
                      idObra: 1,
                      salidaEn: new Date().toISOString(),
                      idsAsistencias: [11],
                  };
        await encolar(4, coleccion, "Lista", datos);
        const envio = vi
            .spyOn(api, "escribir")
            .mockResolvedValue([{ idAsistencia: 11 }] as never);
        await sincronizar(4);
        expect(envio).toHaveBeenCalledWith(
            "POST",
            coleccion === "salidasCuadrilla"
                ? "/api/asistencias/cuadrilla/salidas"
                : "/api/asistencias/cuadrilla",
            {
                ...datos,
                esSincronizacionDiferida: true,
            },
        );
        expect((await listarOperaciones(4))[0].estado).toBe("CONFIRMADA");
    },
);

it("detiene la cola cuando la sesión del servidor pertenece a otra cuenta", async () => {
    autorizar(5);
    await encolar(5, "avances", "Pendiente propio", { idActividad: 1 });
    vi.spyOn(api, "leer").mockResolvedValue({ idUsuario: 6 } as never);
    const escribir = vi.spyOn(api, "escribir");
    await sincronizar(5);
    expect(escribir).not.toHaveBeenCalled();
    expect((await listarOperaciones(5))[0].estado).toBe("PENDIENTE");
});

it("los listados no cargan la fotografía y el reintento conserva todos sus bytes", async () => {
    autorizar(7);
    const original = await encolar(7, "evidencias", "Fotografía", {
        idAvance: 3,
        contenidoArchivo: "Ynl0ZXM=",
        nombreArchivo: "obra.png",
    });
    const enviar = vi.spyOn(api, "escribir").mockRejectedValue(
        new ErrorApi(503, {
            codigo: "servicio_temporalmente_no_disponible",
        }),
    );
    await sincronizar(7);
    const resumen = (await listarResumenes(7))[0];
    expect(resumen.datos.contenidoArchivo).toBeUndefined();
    expect(
        (await obtenerOperacion(original.idLocal))?.datos.contenidoArchivo,
    ).toBe("Ynl0ZXM=");
    await reintentarOperacion(resumen);
    enviar.mockResolvedValue({
        acuses: [{ estadoAcuse: "SINCRONIZADO", idRegistroServidor: "98" }],
    } as never);
    await sincronizar(7);
    expect(enviar.mock.calls[1][2]).toMatchObject({
        evidencias: [
            {
                contenidoArchivo: "Ynl0ZXM=",
                uuidRegistro: original.datos.uuidRegistro,
            },
        ],
    });
    expect((await listarResumenes(7))[0].estado).toBe("CONFIRMADA");
});
