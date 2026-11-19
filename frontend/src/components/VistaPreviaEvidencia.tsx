import { useEffect, useState } from "react";
import { Image, Maximize2, Minimize2, X, RefreshCw } from "lucide-react";
import { api } from "../api/cliente";
import { prepararImagenEvidencia } from "../features/Seguimiento/imagenEvidencia";
import { Boton, Cargando, ErrorVisible } from "./Interfaz";

type EstadoVista =
    | { tipo: "INICIAL" | "CARGANDO" }
    | { tipo: "LISTA"; url: string; nombre: string }
    | { tipo: "ERROR"; error: unknown };

export function VistaPreviaEvidencia({
    idEvidencia,
    automatica = false,
}: {
    idEvidencia: number;
    automatica?: boolean;
}) {
    const [abierta, setAbierta] = useState(automatica);
    const [ampliada, setAmpliada] = useState(false);
    const [intento, setIntento] = useState(0);
    const [estado, setEstado] = useState<EstadoVista>({ tipo: "INICIAL" });

    useEffect(() => {
        if (!abierta) {
            setEstado({ tipo: "INICIAL" });
            return;
        }
        const controlador = new AbortController();
        let vigente = true;
        let url: string | undefined;
        setEstado({ tipo: "CARGANDO" });
        void (async () => {
            try {
                const archivo = await api.archivo(
                    `/api/seguimiento/evidencias/${idEvidencia}/archivo`,
                    controlador.signal,
                );
                const imagen = await prepararImagenEvidencia(archivo.contenido);
                if (!vigente) return;
                url = URL.createObjectURL(imagen);
                setEstado({ tipo: "LISTA", url, nombre: archivo.nombre });
            } catch (error) {
                if (vigente) setEstado({ tipo: "ERROR", error });
            }
        })();
        return () => {
            vigente = false;
            controlador.abort();
            if (url) URL.revokeObjectURL(url);
        };
    }, [abierta, idEvidencia, intento]);

    if (!abierta)
        return (
            <Boton variante="secundario" onClick={() => setAbierta(true)}>
                <Image size={17} aria-hidden="true" />
                Ver imagen
            </Boton>
        );

    return (
        <section
            className={`visor-evidencia ${ampliada ? "visor-evidencia-ampliado" : ""}`}
            aria-label="Vista previa de evidencia"
        >
            <div className="cabecera-visor-evidencia">
                <h3>Vista previa</h3>
                <div>
                    {estado.tipo === "LISTA" && (
                        <Boton
                            variante="secundario"
                            onClick={() => setAmpliada((valor) => !valor)}
                            aria-pressed={ampliada}
                        >
                            {ampliada ? (
                                <Minimize2 size={16} aria-hidden="true" />
                            ) : (
                                <Maximize2 size={16} aria-hidden="true" />
                            )}
                            {ampliada ? "Reducir imagen" : "Ampliar imagen"}
                        </Boton>
                    )}
                    {!automatica && (
                        <Boton
                            variante="texto"
                            onClick={() => {
                                setAbierta(false);
                                setAmpliada(false);
                            }}
                        >
                            <X size={16} aria-hidden="true" />
                            Ocultar imagen
                        </Boton>
                    )}
                </div>
            </div>
            {estado.tipo === "CARGANDO" && (
                <Cargando texto="Cargando imagen…" variante="imagen" />
            )}
            {estado.tipo === "ERROR" && (
                <div className="error-visor-evidencia">
                    <ErrorVisible error={estado.error} />
                    <Boton
                        variante="secundario"
                        onClick={() => setIntento((valor) => valor + 1)}
                    >
                        <RefreshCw size={16} aria-hidden="true" />
                        Reintentar imagen
                    </Boton>
                </div>
            )}
            {estado.tipo === "LISTA" && (
                <figure>
                    <div
                        className="marco-visor-evidencia"
                        tabIndex={ampliada ? 0 : undefined}
                        role={ampliada ? "group" : undefined}
                        aria-label={
                            ampliada
                                ? "Imagen ampliada con desplazamiento"
                                : undefined
                        }
                    >
                        <img
                            src={estado.url}
                            alt={`Evidencia fotográfica: ${estado.nombre}`}
                            decoding="async"
                            onError={() =>
                                setEstado({
                                    tipo: "ERROR",
                                    error: new Error(
                                        "No se pudo mostrar la imagen. Reintenta o descarga el archivo para revisarlo.",
                                    ),
                                })
                            }
                        />
                    </div>
                    {ampliada && (
                        <p className="ayuda-campo">
                            Desliza o desplázate para recorrer la imagen
                            ampliada.
                        </p>
                    )}
                    <figcaption>{estado.nombre}</figcaption>
                </figure>
            )}
        </section>
    );
}
