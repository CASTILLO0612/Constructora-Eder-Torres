import { useEffect, useMemo, useRef, useState } from "react";
import { ArrowUpRight, MapPin } from "lucide-react";
import { configuracion } from "../../app/configuracion";
import {
    cargarGoogleMaps,
    type CoordenadaMapa,
    type EsquemaColorMapa,
    type GoogleCircleInstancia,
    type GoogleInfoWindow,
    type GoogleMapInstancia,
    type GoogleMarkerInstancia,
} from "../../app/googleMaps";
import { Estado, Vacio } from "../../components/Interfaz";
import { texto } from "../../app/formatos";
import type { Fila } from "../Modulos/catalogo";

type ObraGeolocalizada = {
    obra: Fila;
    coordenada: CoordenadaMapa;
    radio: number | null;
};
type EstadoMapa = "cargando" | "listo" | "error";

const temaActual = (): EsquemaColorMapa =>
    document.documentElement.dataset.tema === "oscuro" ? "DARK" : "LIGHT";

function obtenerObrasGeolocalizadas(obras: Fila[]): ObraGeolocalizada[] {
    return obras.flatMap((obra) => {
        const latitud = obra.latitud;
        const longitud = obra.longitud;
        if (
            typeof latitud !== "number" ||
            typeof longitud !== "number" ||
            !Number.isFinite(latitud) ||
            !Number.isFinite(longitud) ||
            Math.abs(latitud) > 90 ||
            Math.abs(longitud) > 180
        ) {
            return [];
        }

        const radio = obra.radioAsistenciaMetros;
        return [
            {
                obra,
                coordenada: { lat: latitud, lng: longitud },
                radio:
                    typeof radio === "number" &&
                    Number.isFinite(radio) &&
                    radio > 0
                        ? radio
                        : null,
            },
        ];
    });
}

function crearContenidoObra(
    elemento: HTMLElement,
    obra: Fila,
    coordenada: CoordenadaMapa,
    radio: number | null,
    seleccionar?: (obra: Fila) => void,
) {
    const contenido = document.createElement("div");
    contenido.className = "mapa-info-obra";

    const titulo = document.createElement("strong");
    titulo.textContent = texto(obra.nombre || "Obra");
    contenido.append(titulo);

    const codigo = document.createElement("span");
    codigo.textContent = String(obra.codigo || "");
    contenido.append(codigo);

    if (radio !== null) {
        const detalleRadio = document.createElement("span");
        detalleRadio.textContent = `Radio de asistencia: ${radio} m`;
        contenido.append(detalleRadio);
    }

    const acciones = document.createElement("div");
    acciones.className = "mapa-acciones-obra";

    const ir = new URL("https://www.google.com/maps/dir/");
    ir.searchParams.set("api", "1");
    ir.searchParams.set("destination", `${coordenada.lat},${coordenada.lng}`);
    const enlaceRuta = document.createElement("a");
    enlaceRuta.href = ir.toString();
    enlaceRuta.target = "_blank";
    enlaceRuta.rel = "noopener noreferrer";
    enlaceRuta.textContent = "Cómo llegar";
    acciones.append(enlaceRuta);

    if (seleccionar) {
        const boton = document.createElement("button");
        boton.type = "button";
        boton.textContent = "Ver obra";
        boton.onclick = () => seleccionar(obra);
        acciones.append(boton);
    }

    contenido.append(acciones);
    elemento.replaceChildren(contenido);
}

export default function MapaObras({
    obras,
    seleccionar,
}: {
    obras: Fila[];
    seleccionar?: (obra: Fila) => void;
}) {
    const elemento = useRef<HTMLDivElement>(null);
    const mapaActual = useRef<GoogleMapInstancia | null>(null);
    const geolocalizadas = useMemo(
        () => obtenerObrasGeolocalizadas(obras),
        [obras],
    );
    const [temaMapa, setTemaMapa] = useState(temaActual);
    const [estadoMapa, setEstadoMapa] = useState<EstadoMapa>(() =>
        configuracion.mapaGoogleClave ? "cargando" : "error",
    );

    useEffect(() => {
        const observador = new MutationObserver(() =>
            setTemaMapa(temaActual()),
        );
        observador.observe(document.documentElement, {
            attributes: true,
            attributeFilter: ["data-tema"],
        });
        return () => observador.disconnect();
    }, []);

    useEffect(() => {
        const contenedor = elemento.current;
        if (!contenedor || geolocalizadas.length === 0) return;

        let cancelado = false;
        let mapa: GoogleMapInstancia | undefined;
        let ventanaInfo: GoogleInfoWindow | undefined;
        const marcadores: GoogleMarkerInstancia[] = [];
        const radios: GoogleCircleInstancia[] = [];

        if (!configuracion.mapaGoogleClave) {
            setEstadoMapa("error");
            return;
        }

        setEstadoMapa("cargando");
        void (async () => {
            try {
                const api = await cargarGoogleMaps(
                    configuracion.mapaGoogleClave,
                );
                const [bibliotecaBase, bibliotecaMapa, bibliotecaMarcadores] =
                    await Promise.all([
                        api.importLibrary("core"),
                        api.importLibrary("maps"),
                        api.importLibrary("marker"),
                    ]);
                if (cancelado) return;

                mapa = new bibliotecaMapa.Map(contenedor, {
                    center: geolocalizadas[0].coordenada,
                    zoom: 13,
                    mapId: "DEMO_MAP_ID",
                    colorScheme: temaMapa,
                    zoomControl: true,
                    mapTypeControl: false,
                    streetViewControl: false,
                    fullscreenControl: false,
                    clickableIcons: false,
                });
                mapaActual.current = mapa;
                ventanaInfo = new bibliotecaMapa.InfoWindow();

                const oscuro = temaMapa === "DARK";
                const color = oscuro ? "#f5f5f5" : "#171717";
                const limites = new bibliotecaBase.LatLngBounds();

                for (const item of geolocalizadas) {
                    const { obra, coordenada, radio } = item;
                    limites.extend(coordenada);

                    if (radio !== null) {
                        try {
                            const circulo = new bibliotecaMapa.Circle({
                                map: mapa,
                                center: coordenada,
                                radius: radio,
                                strokeColor: color,
                                strokeOpacity: 0.72,
                                strokeWeight: 2,
                                fillColor: color,
                                fillOpacity: oscuro ? 0.12 : 0.08,
                                clickable: false,
                            });
                            radios.push(circulo);
                            const limitesRadio = circulo.getBounds();
                            if (limitesRadio) limites.union(limitesRadio);
                        } catch {
                            // Un radio inválido no debe ocultar la ubicación de la obra.
                        }
                    }

                    const marcador =
                        new bibliotecaMarcadores.AdvancedMarkerElement({
                            map: mapa,
                            position: coordenada,
                            title: texto(obra.nombre || "Obra"),
                            gmpClickable: true,
                        });
                    marcadores.push(marcador);
                    try {
                        const pin = new bibliotecaMarcadores.PinElement({
                            background: color,
                            borderColor: oscuro ? "#111111" : "#ffffff",
                            glyphColor: oscuro ? "#111111" : "#ffffff",
                            glyphText: "O",
                            scale: 1.15,
                        });
                        marcador.append(pin);
                    } catch {
                        // Conserva el pin predeterminado si falla su personalización.
                    }
                    marcador.addEventListener("gmp-click", () => {
                        if (!mapa || !ventanaInfo) return;
                        const contenido = document.createElement("div");
                        crearContenidoObra(
                            contenido,
                            obra,
                            coordenada,
                            radio,
                            seleccionar,
                        );
                        ventanaInfo.setContent(contenido);
                        ventanaInfo.open({ map: mapa, anchor: marcador });
                    });
                }

                if (
                    geolocalizadas.length === 1 &&
                    geolocalizadas[0].radio === null
                ) {
                    mapa.setCenter(geolocalizadas[0].coordenada);
                    mapa.setZoom(14);
                } else {
                    mapa.fitBounds(limites, 36);
                }

                setEstadoMapa("listo");
            } catch {
                if (!cancelado) {
                    ventanaInfo?.close();
                    for (const marcador of marcadores) marcador.map = null;
                    for (const radio of radios) radio.setMap(null);
                    if (mapaActual.current === mapa) {
                        mapaActual.current = null;
                        contenedor.replaceChildren();
                    }
                    setEstadoMapa("error");
                }
            }
        })();

        return () => {
            cancelado = true;
            ventanaInfo?.close();
            for (const marcador of marcadores) marcador.map = null;
            for (const radio of radios) radio.setMap(null);
            if (mapaActual.current === mapa) {
                mapaActual.current = null;
                contenedor.replaceChildren();
            }
        };
    }, [geolocalizadas, seleccionar, temaMapa]);

    if (!geolocalizadas.length)
        return (
            <Vacio
                titulo="No hay ubicaciones para mostrar"
                detalle="Las obras con coordenadas registradas aparecerán en esta vista."
            />
        );

    const enfocarObra = (item: ObraGeolocalizada) => {
        mapaActual.current?.setCenter(item.coordenada);
        mapaActual.current?.setZoom(15);
        seleccionar?.(item.obra);
    };

    return (
        <div className="mapa-gerencial">
            <div
                className="mapa-lienzo"
                ref={elemento}
                role="region"
                aria-label="Mapa de Google con ubicaciones y radios de asistencia de las obras"
            />
            {estadoMapa !== "listo" && (
                <div
                    className="aviso-mapa"
                    role={estadoMapa === "cargando" ? "status" : "alert"}
                    aria-live="polite"
                >
                    <MapPin size={15} />
                    {estadoMapa === "cargando"
                        ? "Cargando el mapa de Google…"
                        : "El mapa no está disponible. Consulta las ubicaciones en la lista."}
                </div>
            )}
            {estadoMapa === "listo" &&
                geolocalizadas.some((item) => item.radio !== null) && (
                    <div
                        className="mapa-leyenda"
                        aria-label="Simbología del mapa"
                    >
                        <span>
                            <i className="mapa-simbolo-obra" />
                            Centro de la obra
                        </span>
                        <span>
                            <i className="mapa-simbolo-radio" />
                            Radio de asistencia
                        </span>
                    </div>
                )}
            <div className="lista-mapa">
                <div>
                    <span className="ceja">OBRAS EN ESTA VISTA</span>
                    <strong>{geolocalizadas.length} ubicaciones</strong>
                </div>
                {geolocalizadas.map((item) => (
                    <button
                        key={String(item.obra.idObra)}
                        onClick={() => enfocarObra(item)}
                    >
                        <span>
                            <small>{String(item.obra.codigo)}</small>
                            <strong>{texto(item.obra.nombre)}</strong>
                        </span>
                        <Estado valor={item.obra.estado} />
                        <ArrowUpRight size={17} />
                    </button>
                ))}
                {obras.length >= 100 && (
                    <p className="ayuda-campo">
                        Se muestran las primeras 100 obras cargadas. Consulta
                        las páginas de la lista para revisar las restantes.
                    </p>
                )}
            </div>
        </div>
    );
}
