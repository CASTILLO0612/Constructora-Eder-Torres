export type CoordenadaMapa = { lat: number; lng: number };
export type EsquemaColorMapa = "LIGHT" | "DARK";

export interface GoogleMapBounds {
    extend(coordenada: CoordenadaMapa): GoogleMapBounds;
    union(limites: GoogleMapBounds): GoogleMapBounds;
}

export interface GoogleMapInstancia {
    fitBounds(limites: GoogleMapBounds, padding?: number): void;
    setCenter(coordenada: CoordenadaMapa): void;
    setZoom(nivel: number): void;
}

interface GoogleMapOpciones {
    center: CoordenadaMapa;
    zoom: number;
    mapId: string;
    colorScheme: EsquemaColorMapa;
    zoomControl: boolean;
    mapTypeControl: boolean;
    streetViewControl: boolean;
    fullscreenControl: boolean;
    clickableIcons: boolean;
}

interface GoogleCircleOpciones {
    map: GoogleMapInstancia;
    center: CoordenadaMapa;
    radius: number;
    strokeColor: string;
    strokeOpacity: number;
    strokeWeight: number;
    fillColor: string;
    fillOpacity: number;
    clickable: boolean;
}

interface GooglePinOpciones {
    background: string;
    borderColor: string;
    glyphColor: string;
    glyphText: string;
    scale: number;
}

interface GoogleMarkerOpciones {
    map: GoogleMapInstancia;
    position: CoordenadaMapa;
    title: string;
    gmpClickable: boolean;
}

export interface GoogleMarkerInstancia {
    map: GoogleMapInstancia | null;
    append(contenido: HTMLElement): void;
    addEventListener(tipo: "gmp-click", listener: () => void): void;
}

export interface GoogleInfoWindow {
    setContent(contenido: HTMLElement): void;
    open(opciones: {
        map: GoogleMapInstancia;
        anchor: GoogleMarkerInstancia;
    }): void;
    close(): void;
}

export interface GoogleCircleInstancia {
    getBounds(): GoogleMapBounds | null;
    setMap(map: GoogleMapInstancia | null): void;
}

interface GoogleMapLibrary {
    Map: new (
        elemento: HTMLElement,
        opciones: GoogleMapOpciones,
    ) => GoogleMapInstancia;
    Circle: new (opciones: GoogleCircleOpciones) => GoogleCircleInstancia;
    InfoWindow: new () => GoogleInfoWindow;
}

interface GoogleCoreLibrary {
    LatLngBounds: new () => GoogleMapBounds;
}

interface GoogleMarkerLibrary {
    AdvancedMarkerElement: new (
        opciones: GoogleMarkerOpciones,
    ) => GoogleMarkerInstancia;
    PinElement: new (opciones: GooglePinOpciones) => HTMLElement;
}

interface GoogleMapsNamespace {
    importLibrary(nombre: "core"): Promise<GoogleCoreLibrary>;
    importLibrary(nombre: "maps"): Promise<GoogleMapLibrary>;
    importLibrary(nombre: "marker"): Promise<GoogleMarkerLibrary>;
}

type VentanaGoogleMaps = Window & {
    google?: { maps?: GoogleMapsNamespace };
    __cetMapaGoogleListo?: () => void;
};

let cargaMaps: Promise<GoogleMapsNamespace> | undefined;

export function cargarGoogleMaps(clave: string): Promise<GoogleMapsNamespace> {
    if (!clave)
        return Promise.reject(new Error("Falta configurar Google Maps."));

    const ventana = window as VentanaGoogleMaps;
    const mapsExistente = ventana.google?.maps;
    if (mapsExistente?.importLibrary) return Promise.resolve(mapsExistente);
    if (cargaMaps) return cargaMaps;

    const promesa = new Promise<GoogleMapsNamespace>((resolver, rechazar) => {
        const script = document.createElement("script");
        script.async = true;
        script.dataset.cetGoogleMaps = "true";

        let finalizada = false;
        let limiteTiempo: number | undefined;
        const completar = (error?: Error) => {
            if (finalizada) return;
            finalizada = true;
            if (limiteTiempo !== undefined) window.clearTimeout(limiteTiempo);
            delete ventana.__cetMapaGoogleListo;
            if (error) {
                script.remove();
                rechazar(error);
                return;
            }
            const maps = ventana.google?.maps;
            if (!maps?.importLibrary) {
                rechazar(new Error("Google Maps no respondió correctamente."));
                return;
            }
            resolver(maps);
        };

        ventana.__cetMapaGoogleListo = () => completar();
        script.onerror = () =>
            completar(new Error("No se pudo cargar el mapa de Google."));
        limiteTiempo = window.setTimeout(
            () =>
                completar(
                    new Error("Google Maps tardó demasiado en responder."),
                ),
            20_000,
        );

        const url = new URL("https://maps.googleapis.com/maps/api/js");
        url.searchParams.set("key", clave);
        url.searchParams.set("v", "weekly");
        url.searchParams.set("loading", "async");
        url.searchParams.set("callback", "__cetMapaGoogleListo");
        script.src = url.toString();
        document.head.append(script);
    });

    cargaMaps = promesa.catch((error: unknown) => {
        cargaMaps = undefined;
        throw error;
    });
    return cargaMaps;
}
