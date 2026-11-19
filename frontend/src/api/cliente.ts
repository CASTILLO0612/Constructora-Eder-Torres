import { configuracion } from "../app/configuracion";
import type { RespuestaAntifalsificacion } from "./contratos";
import { conLimiteSolicitud } from "./limiteSolicitud";

export type ProblemaApi = {
    title?: string;
    detail?: string;
    codigo?: string;
    identificador?: string;
    errors?: Record<string, string[]>;
};

export class ErrorApi extends Error {
    constructor(
        public estado: number,
        public problema: ProblemaApi,
        public reintentarEnMs = 0,
    ) {
        super(
            problema.detail ||
                problema.title ||
                `No se pudo completar la solicitud (${estado}).`,
        );
        this.name = "ErrorApi";
    }
}

let antifalsificacion: RespuestaAntifalsificacion | null = null;
let obteniendoToken: Promise<RespuestaAntifalsificacion> | null = null;
export const olvidarToken = () => {
    antifalsificacion = null;
    obteniendoToken = null;
};
export function construirRuta(
    ruta: string,
    parametros: Record<string, unknown> = {},
) {
    const consulta = new URLSearchParams();
    for (const [nombre, valor] of Object.entries(parametros).sort(([a], [b]) =>
        a.localeCompare(b),
    )) {
        if (valor !== "" && valor !== null && valor !== undefined)
            consulta.set(nombre, String(valor));
    }
    return `${configuracion.apiBase}${ruta}${consulta.size ? `?${consulta}` : ""}`;
}

async function respuesta<T>(resultado: Response, ruta: string): Promise<T> {
    if (!resultado.ok) {
        const tipo = resultado.headers.get("content-type") || "";
        const problema: ProblemaApi = tipo.includes("json")
            ? await resultado.json().catch(() => ({}))
            : {};
        if (
            resultado.status === 401 &&
            !ruta.includes("/sesion/iniciar") &&
            !ruta.includes("/sesion/actual")
        ) {
            window.dispatchEvent(new Event("cet:sesion-expirada"));
        }
        const cabecera = resultado.headers.get("Retry-After");
        const segundos = cabecera ? Number(cabecera) : NaN;
        const espera = cabecera
            ? Number.isFinite(segundos)
                ? segundos * 1000
                : Math.max(0, Date.parse(cabecera) - Date.now())
            : 0;
        throw new ErrorApi(resultado.status, problema, espera);
    }
    if (resultado.status === 204) return undefined as T;
    return resultado.json() as Promise<T>;
}

async function token() {
    if (antifalsificacion) return antifalsificacion;
    obteniendoToken ??= conLimiteSolicitud(undefined, async (signal) =>
        respuesta<RespuestaAntifalsificacion>(
            await fetch(construirRuta("/api/sesion/antifalsificacion"), {
                credentials: "include",
                cache: "no-store",
                signal,
            }),
            "/api/sesion/antifalsificacion",
        ),
    )
        .then((t) => {
            antifalsificacion = t;
            return t;
        })
        .finally(() => {
            obteniendoToken = null;
        });
    return obteniendoToken;
}

export const api = {
    async leer<T>(
        ruta: string,
        parametros: Record<string, unknown> = {},
        signal?: AbortSignal,
    ): Promise<T> {
        return conLimiteSolicitud(signal, async (cancelacion) =>
            respuesta<T>(
                await fetch(construirRuta(ruta, parametros), {
                    credentials: "include",
                    cache: "no-store",
                    signal: cancelacion,
                    headers: { Accept: "application/json" },
                }),
                ruta,
            ),
        );
    },
    async escribir<T>(
        metodo: "POST" | "PUT" | "PATCH" | "DELETE",
        ruta: string,
        datos?: unknown,
    ): Promise<T> {
        for (let intento = 0; intento < 2; intento++) {
            const proteccion = await token();
            try {
                return await conLimiteSolicitud(undefined, async (signal) =>
                    respuesta<T>(
                        await fetch(construirRuta(ruta), {
                            method: metodo,
                            credentials: "include",
                            cache: "no-store",
                            signal,
                            headers: {
                                "Content-Type": "application/json",
                                Accept: "application/json",
                                [proteccion.cabecera]: proteccion.token,
                            },
                            body:
                                datos === undefined
                                    ? undefined
                                    : JSON.stringify(datos),
                        }),
                        ruta,
                    ),
                );
            } catch (e) {
                // Este código lo produce el middleware antes de ejecutar la operación.
                if (
                    intento === 0 &&
                    e instanceof ErrorApi &&
                    e.problema.codigo === "antifalsificacion_requerida"
                ) {
                    olvidarToken();
                    continue;
                }
                throw e;
            }
        }
        throw new Error(
            "No se pudo renovar la protección de la sesión. Inicia sesión nuevamente.",
        );
    },
    async archivo(ruta: string, cancelacion?: AbortSignal) {
        return conLimiteSolicitud(cancelacion, async (signal) => {
            const r = await fetch(construirRuta(ruta), {
                credentials: "include",
                cache: "no-store",
                signal,
            });
            if (!r.ok) return respuesta<never>(r, ruta);
            const contenido = await r.blob();
            const nombre = r.headers
                .get("content-disposition")
                ?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)/i)?.[1];
            return {
                contenido,
                nombre: nombre ? decodeURIComponent(nombre) : "evidencia",
            };
        });
    },
};

export const mensajeError = (error: unknown) =>
    error instanceof ErrorApi
        ? error.message
        : error instanceof TypeError
          ? "No se pudo contactar con el servidor. Revisa la conexión."
          : error instanceof Error
            ? error.message
            : "No se pudo completar la operación.";
