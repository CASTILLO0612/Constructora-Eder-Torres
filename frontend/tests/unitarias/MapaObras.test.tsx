import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
    cleanup,
    render,
    screen,
    waitFor,
    within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import MapaObras from "../../src/features/Obras/MapaObras";
import * as googleMaps from "../../src/app/googleMaps";
import { configuracion } from "../../src/app/configuracion";

const obras = [
    {
        idObra: 1,
        codigo: "OB-01",
        nombre: "Obra Managua",
        estado: "ACTIVA",
        latitud: 12.136,
        longitud: -86.251,
        radioAsistenciaMetros: 100,
    },
    {
        idObra: 2,
        codigo: "OB-02",
        nombre: "Obra Masaya",
        estado: "PLANIFICADA",
        latitud: 11.974,
        longitud: -86.094,
        radioAsistenciaMetros: 75,
    },
];

// Respeta la separación real del SDK: LatLngBounds solo existe en core.
function sdkMapa({ fallarPin = false } = {}) {
    const mapas: Mapa[] = [];
    const marcadores: Marcador[] = [];
    const circulos: Circulo[] = [];
    class Limites {
        coordenadas: googleMaps.CoordenadaMapa[] = [];
        extend(coordenada: googleMaps.CoordenadaMapa) {
            this.coordenadas.push(coordenada);
            return this;
        }
        union() {
            return this;
        }
    }
    class Mapa {
        fitBounds = vi.fn();
        setCenter = vi.fn();
        setZoom = vi.fn();
        constructor(
            public elemento: HTMLElement,
            public opciones: { colorScheme: string },
        ) {
            mapas.push(this);
        }
    }
    class Marcador {
        elemento = document.createElement("button");
        private mapa: Mapa | null = null;
        constructor(
            public opciones: {
                map: Mapa;
                title: string;
                position: googleMaps.CoordenadaMapa;
            },
        ) {
            this.elemento.title = opciones.title;
            this.elemento.setAttribute("aria-label", opciones.title);
            this.map = opciones.map;
            marcadores.push(this);
        }
        get map() {
            return this.mapa;
        }
        set map(valor: Mapa | null) {
            this.mapa = valor;
            if (valor) valor.elemento.append(this.elemento);
            else this.elemento.remove();
        }
        append(pin: HTMLElement) {
            this.elemento.append(pin);
        }
        addEventListener(_tipo: string, listener: () => void) {
            this.elemento.addEventListener("click", listener);
        }
    }
    class Circulo {
        setMap = vi.fn();
        getBounds = () => new Limites();
        constructor(
            public opciones: {
                center: googleMaps.CoordenadaMapa;
                radius: number;
            },
        ) {
            circulos.push(this);
        }
    }
    class Pin {
        constructor() {
            if (fallarPin) throw new Error("Personalización no disponible");
            return document.createElement("span");
        }
    }
    const ventana = {
        setContent: vi.fn(),
        open: vi.fn(),
        close: vi.fn(),
    };
    const bibliotecas = {
        core: { LatLngBounds: Limites },
        maps: {
            Map: Mapa,
            Circle: Circulo,
            InfoWindow: class {
                constructor() {
                    return ventana;
                }
            },
        },
        marker: { AdvancedMarkerElement: Marcador, PinElement: Pin },
    };
    const importLibrary = vi.fn(
        async (nombre: keyof typeof bibliotecas) => bibliotecas[nombre],
    );
    vi.spyOn(googleMaps, "cargarGoogleMaps").mockResolvedValue({
        importLibrary,
    } as unknown as Awaited<ReturnType<typeof googleMaps.cargarGoogleMaps>>);
    return { mapas, marcadores, circulos, ventana };
}

const claveAnterior = configuracion.mapaGoogleClave;
beforeEach(() => {
    configuracion.mapaGoogleClave = "clave-ficticia-de-prueba";
    document.documentElement.dataset.tema = "claro";
});
afterEach(() => {
    cleanup();
    configuracion.mapaGoogleClave = claveAnterior;
    delete document.documentElement.dataset.tema;
});

it("dibuja todas las obras y sus radios con las bibliotecas reales del SDK y abre su información", async () => {
    const sdk = sdkMapa();
    render(
        <MapaObras
            obras={[...obras, { idObra: 3, latitud: 100, longitud: 0 }]}
        />,
    );
    const mapa = screen.getByRole("region", { name: /Mapa de Google/ });
    await waitFor(() =>
        expect(within(mapa).getAllByRole("button")).toHaveLength(2),
    );
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    await waitFor(() =>
        expect(
            screen.queryByText("Cargando el mapa de Google…"),
        ).not.toBeInTheDocument(),
    );
    expect(sdk.marcadores.map((m) => m.opciones.position)).toEqual(
        obras.map((o) => ({ lat: o.latitud, lng: o.longitud })),
    );
    expect(sdk.circulos.map((c) => c.opciones.radius)).toEqual([100, 75]);
    expect(sdk.mapas[0].fitBounds.mock.calls[0][0].coordenadas).toHaveLength(2);
    await userEvent.click(
        within(mapa).getByRole("button", { name: "Obra Managua" }),
    );
    expect(sdk.ventana.open).toHaveBeenCalledWith({
        map: sdk.mapas[0],
        anchor: sdk.marcadores[0],
    });
    const contenido = sdk.ventana.setContent.mock.calls[0][0] as HTMLElement;
    expect(contenido).toHaveTextContent("Obra Managua");
    expect(contenido.querySelector("a")).toHaveAttribute(
        "href",
        expect.stringContaining("destination=12.136%2C-86.251"),
    );
});

it("mantiene los marcadores predeterminados si falla la personalización del pin", async () => {
    const sdk = sdkMapa({ fallarPin: true });
    render(<MapaObras obras={obras} />);
    const mapa = screen.getByRole("region", { name: /Mapa de Google/ });
    await waitFor(() =>
        expect(within(mapa).getAllByRole("button")).toHaveLength(2),
    );
    expect(sdk.marcadores.every((m) => m.map === sdk.mapas[0])).toBe(true);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
});

it("recrea el mapa con el tema resuelto y descarta marcadores de datos anteriores", async () => {
    const sdk = sdkMapa();
    const vista = render(<MapaObras obras={obras} />);
    await waitFor(() => expect(sdk.marcadores).toHaveLength(2));
    document.documentElement.dataset.tema = "oscuro";
    await waitFor(() => expect(sdk.mapas).toHaveLength(2));
    expect(sdk.mapas[1].opciones.colorScheme).toBe("DARK");
    expect(sdk.marcadores.slice(0, 2).every((m) => m.map === null)).toBe(true);
    vista.rerender(<MapaObras obras={[obras[1]]} />);
    await waitFor(() => expect(sdk.mapas).toHaveLength(3));
    const mapa = screen.getByRole("region", { name: /Mapa de Google/ });
    expect(within(mapa).getAllByRole("button")).toHaveLength(1);
    expect(
        within(mapa).getByRole("button", { name: "Obra Masaya" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    vista.unmount();
    expect(sdk.marcadores.every((m) => m.map === null)).toBe(true);
});

it("muestra el error solo cuando falla la carga y conserva la lista de obras", async () => {
    vi.spyOn(googleMaps, "cargarGoogleMaps").mockRejectedValue(
        new Error("Sin conexión"),
    );
    render(<MapaObras obras={obras} />);
    expect(await screen.findByRole("alert")).toHaveTextContent(
        "El mapa no está disponible",
    );
    expect(
        screen.getByRole("button", { name: /OB-01 Obra Managua/ }),
    ).toBeInTheDocument();
});
