import { afterEach, expect, it, vi } from "vitest";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { useCola } from "../../src/hooks/usoCola";
import type { OperacionPendiente } from "../../src/offline/almacenamiento";

const datos = vi.hoisted(() => ({ usuario: 1, listar: vi.fn() }));
vi.mock("../../src/app/Sesion", () => ({
    useSesion: () => ({ perfil: { idUsuario: datos.usuario } }),
}));
vi.mock("../../src/offline/almacenamiento", async (importar) => {
    const original =
        await importar<typeof import("../../src/offline/almacenamiento")>();
    return { ...original, listarResumenes: datos.listar };
});
afterEach(() => {
    cleanup();
    datos.usuario = 1;
    datos.listar.mockReset();
});
const operacion = {
    idLocal: "captura-local",
    estado: "PENDIENTE",
} as OperacionPendiente;

it("distingue carga de vacío y descarta la respuesta tardía de otra cuenta", async () => {
    let terminarPrimera!: (operaciones: OperacionPendiente[]) => void;
    datos.listar
        .mockImplementationOnce(
            () =>
                new Promise<OperacionPendiente[]>((resolver) => {
                    terminarPrimera = resolver;
                }),
        )
        .mockResolvedValueOnce([]);
    const { result, rerender } = renderHook(() => useCola());
    expect(result.current.cargando).toBe(true);
    datos.usuario = 2;
    rerender();
    await waitFor(() => expect(result.current.cargando).toBe(false));
    await act(async () => terminarPrimera([operacion]));
    expect(result.current.operaciones).toEqual([]);
    expect(result.current.cargando).toBe(false);
    expect(datos.listar.mock.calls.map((c) => c[0])).toEqual([1, 2]);
});
it("mantiene los pendientes visibles durante una actualización y si falla la lectura", async () => {
    let rechazar!: (error: Error) => void;
    datos.listar.mockResolvedValueOnce([operacion]).mockImplementationOnce(
        () =>
            new Promise((_, rechazo) => {
                rechazar = rechazo;
            }),
    );
    const { result } = renderHook(() => useCola());
    await waitFor(() => expect(result.current.pendientes).toHaveLength(1));
    act(() => window.dispatchEvent(new Event("cet:cola-cambiada")));
    expect(result.current.cargando).toBe(false);
    expect(result.current.pendientes).toHaveLength(1);
    await act(async () =>
        rechazar(new Error("No se pudo leer el almacenamiento local.")),
    );
    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.pendientes).toHaveLength(1);
});
