import { openDB, type DBSchema } from "idb";
import type { RespuestaSesionActiva } from "../api/contratos";

export type Coleccion =
    | "asistencias"
    | "asistenciaCuadrilla"
    | "salidasCuadrilla"
    | "salidasAsistencia"
    | "avances"
    | "evidencias"
    | "incidenciasObra"
    | "entregasEpp"
    | "devolucionesEpp"
    | "reposicionesEpp"
    | "asignacionesHerramienta"
    | "eventosHerramienta";
export type OperacionPendiente = {
    idLocal: string;
    idUsuario: number;
    coleccion: Coleccion;
    titulo: string;
    datos: Record<string, unknown>;
    creadoEn: string;
    estado: "PENDIENTE" | "ENVIANDO" | "ATENCION" | "CONFIRMADA";
    intentos: number;
    proximoIntento: number;
    mensaje?: string;
    codigo?: string;
    idServidor?: string;
    ultimoEnvio?: number;
    dependencia?: { idLocal: string; campo: string };
    esquema?: string;
    rutaConsulta?: string;
};
export type PerfilLocal = {
    clave: string;
    perfil: RespuestaSesionActiva;
    verificadaEn: number;
    ultimoUso: number;
};
type ConsultaLocal = {
    clave: string;
    idUsuario: number;
    datos: unknown;
    guardadoEn: string;
};
interface BaseLocal extends DBSchema {
    operaciones: {
        key: string;
        value: OperacionPendiente;
        indexes: { propietario: number };
    };
    resumenes: {
        key: string;
        value: OperacionPendiente;
        indexes: { propietario: number };
    };
    consultas: {
        key: string;
        value: ConsultaLocal;
        indexes: { propietario: number };
    };
    perfil: { key: string; value: PerfilLocal };
}
let apertura: ReturnType<typeof openDB<BaseLocal>> | undefined;
export function resumirOperacion(
    operacion: OperacionPendiente,
): OperacionPendiente {
    const datos = { ...operacion.datos };
    delete datos.contenidoArchivo;
    if (operacion.estado === "CONFIRMADA") {
        if (Array.isArray(datos.marcaciones))
            datos.marcaciones = datos.marcaciones.map((m) => ({
                idAsignacion: m.idAsignacion,
                uuidRegistro: m.uuidRegistro,
            }));
        for (const clave of Object.keys(datos)) {
            if (
                !/^(id[A-Z]|uuid[A-Z])/.test(clave) &&
                ![
                    "fechaJornada",
                    "entradaEn",
                    "salidaEn",
                    "idsAsistencias",
                    "marcaciones",
                ].includes(clave)
            )
                delete datos[clave];
        }
    }
    return { ...operacion, datos };
}
export const baseLocal = () =>
    (apertura ??= openDB<BaseLocal>("cet-operacion-local", 3, {
        upgrade(base, versionAnterior, _versionNueva, transaccion) {
            if (versionAnterior < 1) {
                base.createObjectStore("operaciones", {
                    keyPath: "idLocal",
                }).createIndex("propietario", "idUsuario");
                base.createObjectStore("consultas", {
                    keyPath: "clave",
                }).createIndex("propietario", "idUsuario");
                base.createObjectStore("perfil", { keyPath: "clave" });
            }
            if (versionAnterior < 2) {
                const resumenes = base.createObjectStore("resumenes", {
                    keyPath: "idLocal",
                });
                resumenes.createIndex("propietario", "idUsuario");
                void (async () => {
                    let cursor = await transaccion
                        .objectStore("operaciones")
                        .openCursor();
                    while (cursor) {
                        await resumenes.put(resumirOperacion(cursor.value));
                        cursor = await cursor.continue();
                    }
                })().catch(() => transaccion.abort());
            }
            if (versionAnterior < 3) {
                void (async () => {
                    let cursor = await transaccion
                        .objectStore("operaciones")
                        .openCursor();
                    while (cursor) {
                        if (cursor.value.estado === "CONFIRMADA") {
                            const minima = resumirOperacion(cursor.value);
                            await cursor.update(minima);
                            await transaccion
                                .objectStore("resumenes")
                                .put(minima);
                        }
                        cursor = await cursor.continue();
                    }
                })().catch(() => transaccion.abort());
            }
        },
    }));
export const avisarCola = () =>
    window.dispatchEvent(new Event("cet:cola-cambiada"));
export async function guardarOperacion(operacion: OperacionPendiente) {
    try {
        const base = await baseLocal();
        const transaccion = base.transaction(
            ["operaciones", "resumenes"],
            "readwrite",
        );
        await transaccion
            .objectStore("operaciones")
            .put(
                operacion.estado === "CONFIRMADA"
                    ? resumirOperacion(operacion)
                    : operacion,
            );
        await transaccion
            .objectStore("resumenes")
            .put(resumirOperacion(operacion));
        await transaccion.done;
    } catch {
        throw new Error(
            "No hay espacio suficiente o el navegador bloqueó el almacenamiento. La operación no se guardó; conserva esta pantalla e inténtalo de nuevo.",
        );
    }
    avisarCola();
}
export async function listarOperaciones(idUsuario: number) {
    return (
        await (
            await baseLocal()
        ).getAllFromIndex("operaciones", "propietario", idUsuario)
    ).sort((a, b) => a.creadoEn.localeCompare(b.creadoEn));
}
export const obtenerOperacion = async (idLocal: string) =>
    (await baseLocal()).get("operaciones", idLocal);
export async function listarResumenes(idUsuario: number) {
    return (
        await (
            await baseLocal()
        ).getAllFromIndex("resumenes", "propietario", idUsuario)
    ).sort((a, b) => a.creadoEn.localeCompare(b.creadoEn));
}
export async function guardarConsulta(
    idUsuario: number,
    ruta: string,
    datos: unknown,
) {
    // Un problema de cuota del cache no puede fingir que falló una consulta confirmada.
    try {
        await (
            await baseLocal()
        ).put("consultas", {
            clave: `${idUsuario}:${ruta}`,
            idUsuario,
            datos,
            guardadoEn: new Date().toISOString(),
        });
        return true;
    } catch {
        return false;
    }
}
export const leerConsulta = async (idUsuario: number, ruta: string) =>
    (await baseLocal()).get("consultas", `${idUsuario}:${ruta}`);
export async function limpiarConsultas(idUsuario: number) {
    const base = await baseLocal();
    const transaccion = base.transaction("consultas", "readwrite");
    let cursor = await transaccion.store
        .index("propietario")
        .openCursor(idUsuario);
    while (cursor) {
        await cursor.delete();
        cursor = await cursor.continue();
    }
    await transaccion.done;
}
export const guardarPerfil = async (perfil: PerfilLocal) =>
    (await baseLocal()).put("perfil", perfil);
export const leerPerfil = async () =>
    (await baseLocal()).get("perfil", "actual");
export const borrarPerfil = async () =>
    (await baseLocal()).delete("perfil", "actual");
