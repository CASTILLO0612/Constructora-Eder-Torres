import { afterEach, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { api, ErrorApi } from "../../src/api/cliente";
import type { RespuestaSesionActiva } from "../../src/api/contratos";
import { ProveedorSesion, useSesion } from "../../src/app/Sesion";
import {
    guardarPerfil,
    listarOperaciones,
} from "../../src/offline/almacenamiento";
import { encolar } from "../../src/offline/sincronizacion";
import { configuracion } from "../../src/app/configuracion";

afterEach(() => {
    cleanup();
    vi.useRealTimers();
    vi.unstubAllGlobals();
});

function observarSesion() {
    function Observador() {
        const { perfil, sinVerificar } = useSesion();
        return (
            <p>
                {!perfil
                    ? "Sin sesión"
                    : sinVerificar
                      ? "Sesión local"
                      : "Sesión verificada"}
            </p>
        );
    }
    const consultas = new QueryClient({
        defaultOptions: { queries: { retry: false } },
    });
    const invalidar = vi.spyOn(consultas, "invalidateQueries");
    render(
        <QueryClientProvider client={consultas}>
            <ProveedorSesion>
                <Observador />
            </ProveedorSesion>
        </QueryClientProvider>,
    );
    return { consultas, invalidar };
}
const perfil: RespuestaSesionActiva = {
    idUsuario: 7,
    nombreUsuario: "persona.prueba",
    idTrabajador: null,
    roles: ["GERENTE_GENERAL"],
    permisos: ["sincronizacion.operar"],
    obrasAsignadas: [],
    tieneAlcanceGlobal: true,
    autenticadoEnUtc: new Date().toISOString(),
};
async function prepararPerfil() {
    await guardarPerfil({
        clave: "actual",
        perfil: { ...perfil, autenticadoEnUtc: new Date().toISOString() },
        verificadaEn: Date.now(),
        ultimoUso: Date.now(),
    });
    await encolar(7, "avances", "Captura conservada", { idActividad: 1 });
}

it("consulta datos aunque el navegador no implemente AbortSignal.any", async () => {
    vi.stubGlobal("AbortSignal", { timeout: AbortSignal.timeout });
    vi.stubGlobal(
        "fetch",
        vi.fn(
            async () =>
                new Response('{"cantidad":1}', {
                    headers: { "Content-Type": "application/json" },
                }),
        ),
    );
    await expect(
        api.leer("/api/obras", {}, new AbortController().signal),
    ).resolves.toEqual({ cantidad: 1 });
});

it("inicia sesión aunque el navegador no implemente AbortSignal.timeout", async () => {
    vi.stubGlobal("AbortSignal", { any: AbortSignal.any });
    vi.stubGlobal(
        "fetch",
        vi.fn(
            async (url) =>
                new Response(
                    JSON.stringify(
                        String(url).includes("antifalsificacion")
                            ? { token: "prueba", cabecera: "X-CET-CSRF" }
                            : { idUsuario: 7 },
                    ),
                    { headers: { "Content-Type": "application/json" } },
                ),
        ),
    );
    await expect(
        api.escribir("POST", "/api/sesion/iniciar", {
            nombreUsuario: "prueba",
            contrasena: "dato-sintetico",
        }),
    ).resolves.toEqual({ idUsuario: 7 });
});

it("mantiene el plazo de espera y libera una petición que no responde", async () => {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    vi.stubGlobal(
        "fetch",
        vi.fn(
            (_url, opciones: RequestInit) =>
                new Promise((_resolver, rechazar) => {
                    opciones.signal!.addEventListener(
                        "abort",
                        () =>
                            rechazar(
                                new DOMException("Cancelada", "AbortError"),
                            ),
                        { once: true },
                    );
                }),
        ),
    );
    const pendiente = expect(api.leer("/api/obras")).rejects.toMatchObject({
        name: "TimeoutError",
    });
    await vi.advanceTimersByTimeAsync(configuracion.esperaSolicitudMs + 1);
    await pendiente;
});

it("cancela la consulta cuando se abandona la vista sin perder esa señal", async () => {
    const origen = new AbortController();
    vi.stubGlobal(
        "fetch",
        vi.fn(
            (_url, opciones: RequestInit) =>
                new Promise((_resolver, rechazar) => {
                    opciones.signal!.addEventListener(
                        "abort",
                        () =>
                            rechazar(
                                new DOMException("Cancelada", "AbortError"),
                            ),
                        { once: true },
                    );
                }),
        ),
    );
    const pendiente = expect(
        api.leer("/api/obras", {}, origen.signal),
    ).rejects.toMatchObject({ name: "AbortError" });
    origen.abort();
    await pendiente;
});

it("recupera una sesión local y los datos sin depender de un evento online", async () => {
    await prepararPerfil();
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    let disponible = false;
    vi.spyOn(api, "leer").mockImplementation(async () => {
        if (!disponible) throw new TypeError("Failed to fetch");
        return perfil as never;
    });
    const { invalidar } = observarSesion();
    await screen.findByText("Sesión local");
    expect(navigator.onLine).toBe(true);
    disponible = true;
    await act(async () => {
        await vi.advanceTimersByTimeAsync(30_000);
    });
    await waitFor(() =>
        expect(screen.getByText("Sesión verificada")).toBeVisible(),
    );
    expect(invalidar).toHaveBeenCalled();
    expect((await listarOperaciones(7))[0].estado).toBe("PENDIENTE");
});

it("una sesión revocada exige ingresar de nuevo y conserva las capturas locales", async () => {
    await prepararPerfil();
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    let revocada = false;
    vi.spyOn(api, "leer").mockImplementation(async () => {
        throw revocada
            ? new ErrorApi(401, { codigo: "sesion_requerida" })
            : new TypeError("Failed to fetch");
    });
    observarSesion();
    await screen.findByText("Sesión local");
    revocada = true;
    await act(async () => {
        await vi.advanceTimersByTimeAsync(30_000);
    });
    await waitFor(() => expect(screen.getByText("Sin sesión")).toBeVisible());
    expect((await listarOperaciones(7))[0].estado).toBe("PENDIENTE");
});
