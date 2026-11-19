import {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useRef,
    useState,
    type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, ErrorApi, olvidarToken } from "../api/cliente";
import type { RespuestaSesionActiva } from "../api/contratos";
import {
    borrarPerfil,
    guardarPerfil,
    leerPerfil,
    limpiarConsultas,
} from "../offline/almacenamiento";
import { configuracion } from "./configuracion";
import { establecerPropietarioSincronizacion } from "../offline/sincronizacion";

type ContextoSesion = {
    perfil: RespuestaSesionActiva | null;
    cargando: boolean;
    sinVerificar: boolean;
    verificando: boolean;
    motivo: string;
    ingresar: (nombre: string, clave: string) => Promise<void>;
    salir: () => Promise<void>;
    puede: (...permisos: string[]) => boolean;
    actualizar: () => Promise<void>;
};
const contexto = createContext<ContextoSesion | null>(null);
export const useSesion = () => {
    const valor = useContext(contexto);
    if (!valor) throw new Error("Falta el proveedor de sesión.");
    return valor;
};

export function ProveedorSesion({ children }: { children: ReactNode }) {
    const [perfil, asignarPerfil] = useState<RespuestaSesionActiva | null>(
        null,
    );
    const [cargando, setCargando] = useState(true);
    const [sinVerificar, setSinVerificar] = useState(false);
    const [verificando, setVerificando] = useState(false);
    const [motivo, setMotivo] = useState("");
    const ultimoUso = useRef(Date.now());
    const ultimaPersistencia = useRef(0);
    const comprobacionEnCurso = useRef<Promise<void> | null>(null);
    const revisionSesion = useRef(0);
    const consultas = useQueryClient();
    useEffect(
        () => establecerPropietarioSincronizacion(perfil?.idUsuario ?? null),
        [perfil?.idUsuario],
    );
    const guardar = useCallback(async (p: RespuestaSesionActiva) => {
        establecerPropietarioSincronizacion(p.idUsuario);
        asignarPerfil(p);
        setSinVerificar(false);
        ultimoUso.current = Date.now();
        await guardarPerfil({
            clave: "actual",
            perfil: p,
            verificadaEn: Date.now(),
            ultimoUso: Date.now(),
        }).catch(() => undefined);
    }, []);
    const expirar = useCallback(() => {
        revisionSesion.current += 1;
        establecerPropietarioSincronizacion(null);
        asignarPerfil(null);
        setSinVerificar(false);
        olvidarToken();
        consultas.clear();
        void borrarPerfil().catch(() => undefined);
        setMotivo(
            "Tu sesión terminó. Inicia sesión para continuar; tus registros pendientes se conservan.",
        );
    }, [consultas]);
    const actualizar = useCallback(() => {
        if (comprobacionEnCurso.current) return comprobacionEnCurso.current;
        const revision = revisionSesion.current;
        setVerificando(true);
        const comprobar = async () => {
            try {
                const actual =
                    await api.leer<RespuestaSesionActiva>("/api/sesion/actual");
                if (revision !== revisionSesion.current) return;
                await guardar(actual);
                void consultas.invalidateQueries({
                    queryKey: ["datos", actual.idUsuario],
                });
            } catch (e) {
                if (revision !== revisionSesion.current) return;
                if (e instanceof ErrorApi && e.estado === 401) {
                    const anterior = await leerPerfil().catch(() => undefined);
                    if (revision !== revisionSesion.current) return;
                    if (anterior) expirar();
                    else {
                        asignarPerfil(null);
                        olvidarToken();
                    }
                } else if (e instanceof ErrorApi && e.estado < 500) {
                    asignarPerfil(null);
                    setMotivo(e.message);
                } else {
                    const local = await leerPerfil().catch(() => undefined);
                    if (revision !== revisionSesion.current) return;
                    const emitida =
                        local &&
                        Date.parse(String(local.perfil.autenticadoEnUtc));
                    if (
                        local &&
                        emitida &&
                        Date.now() - emitida < configuracion.duracionSesionMs &&
                        Date.now() - local.ultimoUso <
                            configuracion.inactividadMs
                    ) {
                        asignarPerfil(local.perfil);
                        establecerPropietarioSincronizacion(
                            local.perfil.idUsuario,
                        );
                        ultimoUso.current = local.ultimoUso;
                        setSinVerificar(true);
                    } else asignarPerfil(null);
                }
            } finally {
                setCargando(false);
                setVerificando(false);
            }
        };
        comprobacionEnCurso.current = comprobar().finally(() => {
            comprobacionEnCurso.current = null;
        });
        return comprobacionEnCurso.current;
    }, [guardar, expirar, consultas]);
    useEffect(() => {
        void actualizar();
    }, [actualizar]);
    useEffect(() => {
        if (!("BroadcastChannel" in window)) return;
        const canal = new BroadcastChannel("cet-cambio-sesion");
        canal.onmessage = () => {
            olvidarToken();
            void actualizar();
        };
        return () => canal.close();
    }, [actualizar]);
    useEffect(() => {
        const reconectar = () => {
            void actualizar().catch(() => undefined);
        };
        window.addEventListener("online", reconectar);
        return () => window.removeEventListener("online", reconectar);
    }, [actualizar]);
    useEffect(() => {
        if (!sinVerificar || !perfil) return;
        // Recuperar un enlace USB o un servidor no siempre dispara "online".
        const comprobar = () => {
            if (document.visibilityState === "visible") void actualizar();
        };
        const intervalo = window.setInterval(
            comprobar,
            configuracion.reintentoSesionMs,
        );
        document.addEventListener("visibilitychange", comprobar);
        window.addEventListener("focus", comprobar);
        return () => {
            clearInterval(intervalo);
            document.removeEventListener("visibilitychange", comprobar);
            window.removeEventListener("focus", comprobar);
        };
    }, [sinVerificar, perfil?.idUsuario, actualizar]);
    useEffect(() => {
        window.addEventListener("cet:sesion-expirada", expirar);
        return () => window.removeEventListener("cet:sesion-expirada", expirar);
    }, [expirar]);
    useEffect(() => {
        if (!perfil) return;
        const actividad = () => {
            ultimoUso.current = Date.now();
            if (Date.now() - ultimaPersistencia.current > 60_000) {
                ultimaPersistencia.current = Date.now();
                void guardarPerfil({
                    clave: "actual",
                    perfil,
                    verificadaEn: Date.now(),
                    ultimoUso: ultimoUso.current,
                }).catch(() => undefined);
            }
        };
        const comprobar = () => {
            const emitida = Date.parse(String(perfil.autenticadoEnUtc));
            if (
                Date.now() - ultimoUso.current >= configuracion.inactividadMs ||
                Date.now() - emitida >= configuracion.duracionSesionMs
            )
                expirar();
        };
        const intervalo = window.setInterval(comprobar, 15_000);
        const visible = () => {
            comprobar();
        };
        for (const evento of ["pointerdown", "keydown", "scroll"])
            window.addEventListener(evento, actividad, { passive: true });
        document.addEventListener("visibilitychange", visible);
        return () => {
            clearInterval(intervalo);
            for (const e of ["pointerdown", "keydown", "scroll"])
                window.removeEventListener(e, actividad);
            document.removeEventListener("visibilitychange", visible);
        };
    }, [perfil, expirar]);
    const ingresar = async (nombreUsuario: string, contrasena: string) => {
        revisionSesion.current += 1;
        olvidarToken();
        const p = await api.escribir<RespuestaSesionActiva>(
            "POST",
            "/api/sesion/iniciar",
            { nombreUsuario, contrasena },
        );
        olvidarToken();
        consultas.clear();
        setMotivo("");
        await guardar(p);
        if ("BroadcastChannel" in window) {
            const canal = new BroadcastChannel("cet-cambio-sesion");
            canal.postMessage("actualizar");
            canal.close();
        }
    };
    const salir = async () => {
        revisionSesion.current += 1;
        // Se informa si no se pudo revocar en el servidor; no se finge un cierre correcto.
        await api.escribir("POST", "/api/sesion/cerrar");
        establecerPropietarioSincronizacion(null);
        if (perfil) await limpiarConsultas(perfil.idUsuario);
        asignarPerfil(null);
        olvidarToken();
        consultas.clear();
        await borrarPerfil();
        setMotivo("");
        if ("BroadcastChannel" in window) {
            const canal = new BroadcastChannel("cet-cambio-sesion");
            canal.postMessage("actualizar");
            canal.close();
        }
    };
    return (
        <contexto.Provider
            value={{
                perfil,
                cargando,
                sinVerificar,
                verificando,
                motivo,
                ingresar,
                salir,
                actualizar,
                puede: (...ps) => ps.some((p) => perfil?.permisos.includes(p)),
            }}
        >
            {children}
        </contexto.Provider>
    );
}
