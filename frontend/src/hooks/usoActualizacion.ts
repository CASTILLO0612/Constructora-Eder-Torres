import { useEffect, useState } from "react";
export function useActualizacion() {
    const [registro, setRegistro] = useState<ServiceWorkerRegistration>();
    const [disponible, setDisponible] = useState(false);
    useEffect(() => {
        if (!import.meta.env.PROD || !("serviceWorker" in navigator)) return;
        let desmontado = false;
        void navigator.serviceWorker
            .register("/sw.js")
            .then((r) => {
                if (desmontado) return;
                setRegistro(r);
                if (r.waiting) setDisponible(true);
                r.addEventListener("updatefound", () => {
                    const instalando = r.installing;
                    instalando?.addEventListener("statechange", () => {
                        if (
                            instalando.state === "installed" &&
                            navigator.serviceWorker.controller
                        )
                            setDisponible(true);
                    });
                });
            })
            .catch(() => {
                /* La interfaz online permanece disponible si el navegador bloquea la instalación. */
            });
        return () => {
            desmontado = true;
        };
    }, []);
    const actualizar = () => {
        navigator.serviceWorker.addEventListener(
            "controllerchange",
            () => location.reload(),
            { once: true },
        );
        registro?.waiting?.postMessage("ACTIVAR_VERSION");
    };
    return { disponible, ocultar: () => setDisponible(false), actualizar };
}
