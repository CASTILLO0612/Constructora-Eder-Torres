import { afterEach, expect, it, vi } from "vitest";
import {
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EvolucionAsistencia } from "../../src/features/Inicio/PanelesAnaliticos";
import { api } from "../../src/api/cliente";
import { fecha, fechaJornada } from "../../src/app/formatos";
import { desplazarFecha } from "../../src/features/Inicio/datosGraficos";

vi.mock("../../src/app/Sesion", () => ({
    useSesion: () => ({ perfil: { idUsuario: 7 } }),
}));
afterEach(cleanup);

const hoy = fechaJornada();
const ayer = desplazarFecha(hoy, -1);
const filas = [
    {
        fechaJornada: hoy,
        entradaEn: `${hoy}T14:00:00Z`,
        salidaEn: `${hoy}T15:00:00Z`,
        estado: "COMPLETA",
    },
    {
        fechaJornada: ayer,
        entradaEn: `${ayer}T14:00:00Z`,
        salidaEn: `${ayer}T17:00:00Z`,
        estado: "COMPLETA",
    },
    {
        fechaJornada: ayer,
        entradaEn: `${ayer}T20:00:00Z`,
        salidaEn: `${ayer}T22:00:00Z`,
        estado: "COMPLETA",
    },
];
function panel() {
    const consultas = new QueryClient({
        defaultOptions: { queries: { retry: false } },
    });
    return render(
        <QueryClientProvider client={consultas}>
            <MemoryRouter>
                <EvolucionAsistencia />
            </MemoryRouter>
        </QueryClientProvider>,
    );
}

it("actualiza el número principal, la fecha y el acceso al historial al elegir un día", async () => {
    vi.spyOn(api, "leer").mockResolvedValue(filas as never);
    const { container } = panel();
    const total = container.querySelector(".total-asistencia")!;
    await waitFor(() => expect(total).toHaveTextContent(/^1$/));
    fireEvent.click(
        screen.getByRole("button", { name: `${fecha(ayer)}: 2 registros` }),
    );
    expect(total).toHaveTextContent(/^2$/);
    expect(total).toHaveAttribute("aria-label", `Registros del ${fecha(ayer)}`);
    expect(screen.getByRole("link", { name: "Ver registros" })).toHaveAttribute(
        "href",
        `/asistencia?fechaDesde=${ayer}&fechaHasta=${ayer}`,
    );
    fireEvent.click(screen.getByRole("button", { name: "Horas", exact: true }));
    expect(total).toHaveTextContent(/^5$/);
    fireEvent.focus(screen.getByRole("button", { name: `${fecha(hoy)}: 1 h` }));
    expect(total).toHaveTextContent(/^1$/);
    const vacio = desplazarFecha(hoy, -2);
    fireEvent.click(
        screen.getByRole("button", { name: `${fecha(vacio)}: 0 h` }),
    );
    expect(total).toHaveTextContent(/^0$/);
});

it("mantiene la selección por fecha al ampliar el período y vuelve al día actual si queda fuera", async () => {
    vi.spyOn(api, "leer").mockResolvedValue(filas as never);
    const { container } = panel();
    await screen.findByRole("button", { name: `${fecha(ayer)}: 2 registros` });
    fireEvent.click(
        screen.getByRole("button", { name: `${fecha(ayer)}: 2 registros` }),
    );
    const periodo = screen.getByRole("combobox", {
        name: "Período del gráfico de asistencia",
    });
    fireEvent.change(periodo, { target: { value: "14" } });
    await waitFor(() =>
        expect(container.querySelectorAll(".dias-grafico button")).toHaveLength(
            14,
        ),
    );
    expect(
        screen.getByRole("button", { name: `${fecha(ayer)}: 2 registros` }),
    ).toHaveAttribute("aria-pressed", "true");
    const anterior = desplazarFecha(hoy, -13);
    fireEvent.click(
        screen.getByRole("button", { name: `${fecha(anterior)}: 0 registros` }),
    );
    fireEvent.change(periodo, { target: { value: "7" } });
    await waitFor(() =>
        expect(container.querySelector(".total-asistencia")).toHaveTextContent(
            /^1$/,
        ),
    );
    expect(
        screen.getByRole("button", { name: `${fecha(hoy)}: 1 registros` }),
    ).toHaveAttribute("aria-pressed", "true");
});

it("cuenta las asistencias de todas las páginas para que el total diario no quede limitado a cien", async () => {
    const registros = Array.from({ length: 151 }, (_, idAsistencia) => ({
        ...filas[0],
        idAsistencia,
    }));
    const leer = vi
        .spyOn(api, "leer")
        .mockImplementation(async (_ruta, parametros) => {
            const desde = (Number(parametros?.pagina || 1) - 1) * 100;
            return registros.slice(desde, desde + 100) as never;
        });
    const { container } = panel();
    await waitFor(() =>
        expect(container.querySelector(".total-asistencia")).toHaveTextContent(
            /^151$/,
        ),
    );
    expect(
        screen.getByRole("button", { name: `${fecha(hoy)}: 151 registros` }),
    ).toBeVisible();
    expect(leer).toHaveBeenCalledTimes(2);
    expect(container.textContent).not.toContain("Primeros 100 registros");
});
