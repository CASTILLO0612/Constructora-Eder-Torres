import { api, ErrorApi, mensajeError } from "../api/cliente";
import { mensajeParaUsuario } from "../app/mensajes";
import {
    avisarCola,
    baseLocal,
    guardarOperacion,
    listarResumenes,
    resumirOperacion,
    type Coleccion,
    type OperacionPendiente,
} from "./almacenamiento";

type Acuse = {
    uuidRegistro?: string;
    estadoAcuse: string;
    idRegistroServidor?: string | number | null;
    mensajeError?: string;
    codigoError?: string;
};
type ResultadoLote = { acuses: Acuse[] };
const confirmados = new Set(["SINCRONIZADO", "IDEMPOTENTE_PREVIO"]);
let ejecutando: Promise<void> | null = null;
let propietarioActivo: number | null = null;
let nuevaPasada = false;
export const establecerPropietarioSincronizacion = (
    idUsuario: number | null,
) => {
    propietarioActivo = idUsuario;
};

export async function encolar(
    idUsuario: number,
    coleccion: Coleccion,
    titulo: string,
    datos: Record<string, unknown>,
    esquema?: string,
    dependencia?: OperacionPendiente["dependencia"],
    rutaConsulta?: string,
) {
    const operacion: OperacionPendiente = {
        idLocal: crypto.randomUUID(),
        idUsuario,
        coleccion,
        titulo,
        datos: ["asistenciaCuadrilla", "salidasCuadrilla"].includes(coleccion)
            ? { ...datos }
            : {
                  ...datos,
                  uuidRegistro: datos.uuidRegistro || crypto.randomUUID(),
              },
        creadoEn: new Date().toISOString(),
        estado: "PENDIENTE",
        intentos: 0,
        proximoIntento: 0,
        esquema,
        dependencia,
        rutaConsulta,
    };
    await guardarOperacion(operacion);
    return operacion;
}

async function reclamar(idLocal: string) {
    const base = await baseLocal();
    const t = base.transaction(["operaciones", "resumenes"], "readwrite");
    const o = await t.objectStore("operaciones").get(idLocal);
    if (
        !o ||
        o.estado === "CONFIRMADA" ||
        o.estado === "ATENCION" ||
        o.proximoIntento > Date.now() ||
        (o.estado === "ENVIANDO" && Date.now() - (o.ultimoEnvio || 0) < 120_000)
    ) {
        await t.done;
        return undefined;
    }
    o.estado = "ENVIANDO";
    o.ultimoEnvio = Date.now();
    o.intentos += 1;
    await t.objectStore("operaciones").put(o);
    await t.objectStore("resumenes").put(resumirOperacion(o));
    await t.done;
    return o;
}

async function procesar(idUsuario: number) {
    if (!navigator.onLine || propietarioActivo !== idUsuario) return;
    const lista = (await listarResumenes(idUsuario)).sort(
        (a, b) => Number(!!a.dependencia) - Number(!!b.dependencia),
    );
    if (
        !lista.some(
            (o) =>
                o.estado !== "CONFIRMADA" &&
                o.estado !== "ATENCION" &&
                o.proximoIntento <= Date.now(),
        )
    )
        return;
    try {
        const sesion = await api.leer<{ idUsuario: number }>(
            "/api/sesion/actual",
        );
        if (sesion.idUsuario !== idUsuario) return;
    } catch (e) {
        if (e instanceof ErrorApi && e.estado === 401)
            window.dispatchEvent(new Event("cet:sesion-expirada"));
        return;
    }
    for (const item of lista) {
        if (!navigator.onLine || propietarioActivo !== idUsuario) break;
        if (
            item.estado === "CONFIRMADA" ||
            item.estado === "ATENCION" ||
            item.proximoIntento > Date.now()
        )
            continue;
        let idDependencia: number | undefined;
        if (item.dependencia) {
            const anterior = await (
                await baseLocal()
            ).get("operaciones", item.dependencia.idLocal);
            if (
                !anterior ||
                anterior.idUsuario !== idUsuario ||
                anterior.estado !== "CONFIRMADA" ||
                !anterior.idServidor
            )
                continue;
            idDependencia = Number(anterior.idServidor);
        }
        const operacion = await reclamar(item.idLocal);
        if (!operacion) continue;
        if (operacion.dependencia && idDependencia !== undefined)
            operacion.datos[operacion.dependencia.campo] = idDependencia;
        avisarCola();
        try {
            const datos = { ...operacion.datos };
            if (
                ["asistenciaCuadrilla", "salidasCuadrilla"].includes(
                    operacion.coleccion,
                )
            ) {
                const resultado = await api.escribir<
                    { idAsistencia: number }[]
                >(
                    "POST",
                    operacion.coleccion === "salidasCuadrilla"
                        ? "/api/asistencias/cuadrilla/salidas"
                        : "/api/asistencias/cuadrilla",
                    {
                        ...datos,
                        esSincronizacionDiferida: true,
                    },
                );
                operacion.estado = "CONFIRMADA";
                operacion.idServidor = resultado
                    .map((r) => r.idAsistencia)
                    .join(",");
                operacion.mensaje = undefined;
                await guardarOperacion(operacion);
                window.dispatchEvent(new Event("cet:datos-cambiados"));
                continue;
            }
            if (
                ![
                    "salidasAsistencia",
                    "devolucionesEpp",
                    "eventosHerramienta",
                ].includes(operacion.coleccion)
            )
                datos.esSincronizacionDiferida = true;
            const resultado = await api.escribir<ResultadoLote>(
                "POST",
                "/api/sincronizacion/lote",
                { [operacion.coleccion]: [datos] },
            );
            const acuse = resultado.acuses[0];
            if (!acuse)
                throw new Error(
                    "Aún no se pudo confirmar la captura. Se conserva para volver a enviarla.",
                );
            if (confirmados.has(acuse.estadoAcuse)) {
                operacion.estado = "CONFIRMADA";
                operacion.idServidor = String(acuse.idRegistroServidor ?? "");
                operacion.mensaje = undefined;
                // Se conservan UUID y acuse, retirando bytes de evidencia ya confirmados.
                if (operacion.coleccion === "evidencias")
                    operacion.datos = {
                        uuidRegistro: operacion.datos.uuidRegistro,
                        nombreArchivo: operacion.datos.nombreArchivo,
                    };
            } else {
                operacion.estado =
                    acuse.estadoAcuse === "PENDIENTE_REINTENTO"
                        ? "PENDIENTE"
                        : "ATENCION";
                operacion.mensaje = mensajeParaUsuario(
                    acuse.mensajeError,
                    "Revisa los datos y permisos antes de reenviar.",
                );
                operacion.codigo = acuse.codigoError;
                operacion.proximoIntento =
                    Date.now() +
                    Math.min(
                        300_000,
                        2000 * 2 ** Math.min(operacion.intentos, 7),
                    );
            }
            await guardarOperacion(operacion);
            window.dispatchEvent(new Event("cet:datos-cambiados"));
        } catch (error) {
            const transitorio =
                !(error instanceof ErrorApi) ||
                [401, 429, 500, 502, 503, 504].includes(error.estado);
            operacion.estado = transitorio ? "PENDIENTE" : "ATENCION";
            operacion.mensaje = mensajeError(error);
            operacion.proximoIntento =
                Date.now() +
                Math.max(
                    error instanceof ErrorApi ? error.reintentarEnMs : 0,
                    Math.min(
                        300_000,
                        2000 * 2 ** Math.min(operacion.intentos, 7),
                    ),
                );
            await guardarOperacion(operacion);
            if (
                !(error instanceof ErrorApi) ||
                [401, 429, 503].includes(error.estado)
            )
                break;
        }
    }
}

export function sincronizar(idUsuario: number) {
    if (propietarioActivo !== idUsuario) return Promise.resolve();
    if (ejecutando) {
        nuevaPasada = true;
        return ejecutando;
    }
    const tarea = async () => {
        do {
            nuevaPasada = false;
            await procesar(idUsuario);
        } while (nuevaPasada && propietarioActivo === idUsuario);
    };
    ejecutando = (async () => {
        if ("locks" in navigator)
            await navigator.locks.request(
                "cet-sincronizacion",
                { ifAvailable: true },
                async (candado) => {
                    if (candado) await tarea();
                },
            );
        else await tarea();
    })().finally(() => {
        ejecutando = null;
        if (propietarioActivo !== null && propietarioActivo !== idUsuario)
            void sincronizar(propietarioActivo).catch(() => undefined);
    });
    return ejecutando;
}
export async function reintentarOperacion(operacion: OperacionPendiente) {
    const original = await (
        await baseLocal()
    ).get("operaciones", operacion.idLocal);
    if (
        !original ||
        original.estado === "CONFIRMADA" ||
        original.idUsuario !== propietarioActivo
    )
        return;
    await guardarOperacion({
        ...original,
        estado: "PENDIENTE",
        proximoIntento: 0,
    });
}
