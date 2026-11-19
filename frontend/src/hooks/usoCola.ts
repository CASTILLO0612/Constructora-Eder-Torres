import { useCallback, useEffect, useRef, useState } from "react";
import { useSesion } from "../app/Sesion";
import {
    listarResumenes,
    type OperacionPendiente,
} from "../offline/almacenamiento";
import { sincronizar } from "../offline/sincronizacion";
export function useCola(habilitado = true) {
    const { perfil } = useSesion();
    const propietario = habilitado ? perfil?.idUsuario : undefined;
    const [resultado, setResultado] = useState<{
        propietario?: number;
        operaciones: OperacionPendiente[];
        error?: unknown;
    }>({ operaciones: [] });
    const peticion = useRef(0);
    const cargar = useCallback(async () => {
        const orden = ++peticion.current;
        try {
            const operaciones = propietario
                ? await listarResumenes(propietario)
                : [];
            if (orden === peticion.current)
                setResultado({ propietario, operaciones });
        } catch (e) {
            if (orden === peticion.current)
                setResultado((actual) => ({
                    propietario,
                    operaciones:
                        actual.propietario === propietario
                            ? actual.operaciones
                            : [],
                    error: e,
                }));
        }
    }, [propietario]);
    useEffect(() => {
        void cargar();
        if (habilitado) window.addEventListener("cet:cola-cambiada", cargar);
        return () => {
            ++peticion.current;
            window.removeEventListener("cet:cola-cambiada", cargar);
        };
    }, [cargar, habilitado]);
    const operaciones =
        resultado.propietario === propietario ? resultado.operaciones : [];
    return {
        operaciones,
        pendientes: operaciones.filter((o) => o.estado !== "CONFIRMADA"),
        refrescar: cargar,
        error:
            resultado.propietario === propietario ? resultado.error : undefined,
        cargando: !!propietario && resultado.propietario !== propietario,
    };
}
export function MotorSincronizacion() {
    const { perfil, sinVerificar } = useSesion();
    useEffect(() => {
        if (
            !perfil ||
            sinVerificar ||
            !perfil.permisos.includes("sincronizacion.operar")
        )
            return;
        const ejecutar = () => {
            void sincronizar(perfil.idUsuario).catch(() => undefined);
        };
        ejecutar();
        const intervalo = window.setInterval(ejecutar, 30_000);
        window.addEventListener("online", ejecutar);
        window.addEventListener("cet:cola-cambiada", ejecutar);
        return () => {
            clearInterval(intervalo);
            window.removeEventListener("online", ejecutar);
            window.removeEventListener("cet:cola-cambiada", ejecutar);
        };
    }, [perfil?.idUsuario, sinVerificar]);
    return null;
}
