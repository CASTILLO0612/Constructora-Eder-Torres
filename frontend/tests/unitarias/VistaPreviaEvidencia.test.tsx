import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { api, ErrorApi } from "../../src/api/cliente";
import { VistaPreviaEvidencia } from "../../src/components/VistaPreviaEvidencia";
import { prepararImagenEvidencia } from "../../src/features/Seguimiento/imagenEvidencia";

const contenidoPng = () =>
    new Blob([new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0])], {
        type: "application/octet-stream",
    });
const crearUrl = vi.fn(() => "blob:imagen-privada");
const liberarUrl = vi.fn();

beforeEach(() => {
    crearUrl.mockClear();
    liberarUrl.mockClear();
    vi.stubGlobal(
        "URL",
        class extends URL {
            static createObjectURL = crearUrl;
            static revokeObjectURL = liberarUrl;
        },
    );
});
afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});

it.each([
    ["PNG", [137, 80, 78, 71, 13, 10, 26, 10], "image/png"],
    ["JPEG", [255, 216, 255, 224], "image/jpeg"],
    ["WebP", [82, 73, 70, 70, 0, 0, 0, 0, 87, 69, 66, 80], "image/webp"],
] as const)(
    "visualiza %s por contenido aunque la API lo entregue como binario",
    async (_nombre, cabecera, tipo) => {
        const imagen = await prepararImagenEvidencia(
            new Blob([new Uint8Array(cabecera)], {
                type: "application/octet-stream",
            }),
        );
        expect(imagen.type).toBe(tipo);
    },
);

it("rechaza documentos activos aunque declaren ser una imagen", async () => {
    await expect(
        prepararImagenEvidencia(
            new Blob(
                [
                    "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>",
                ],
                { type: "image/png" },
            ),
        ),
    ).rejects.toThrow("JPEG, PNG y WebP");
});

it("solicita una imagen vinculada solo al abrirla y libera sus datos al ocultarla", async () => {
    const usuario = userEvent.setup();
    const consultar = vi.spyOn(api, "archivo").mockResolvedValue({
        contenido: contenidoPng(),
        nombre: "avance-obra.png",
    });
    render(<VistaPreviaEvidencia idEvidencia={42} />);
    expect(consultar).not.toHaveBeenCalled();
    await usuario.click(screen.getByRole("button", { name: "Ver imagen" }));
    expect(
        await screen.findByRole("img", { name: /avance-obra.png/ }),
    ).toHaveAttribute("src", "blob:imagen-privada");
    expect(consultar).toHaveBeenCalledWith(
        "/api/seguimiento/evidencias/42/archivo",
        expect.any(AbortSignal),
    );
    await usuario.click(screen.getByRole("button", { name: "Ampliar imagen" }));
    expect(
        screen.getByRole("button", { name: "Reducir imagen" }),
    ).toHaveAttribute("aria-pressed", "true");
    expect(consultar).toHaveBeenCalledTimes(1);
    await usuario.click(screen.getByRole("button", { name: "Ocultar imagen" }));
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
    expect(liberarUrl).toHaveBeenCalledWith("blob:imagen-privada");
    expect(consultar.mock.calls[0][1]?.aborted).toBe(true);
});

it("conserva la denegación de permisos y permite reintentar con la misma API", async () => {
    const usuario = userEvent.setup();
    const consultar = vi
        .spyOn(api, "archivo")
        .mockRejectedValueOnce(
            new ErrorApi(403, { detail: "No tienes acceso a esta evidencia." }),
        )
        .mockResolvedValueOnce({
            contenido: contenidoPng(),
            nombre: "evidencia.png",
        });
    const vista = render(<VistaPreviaEvidencia idEvidencia={43} automatica />);
    expect(await screen.findByRole("alert")).toHaveTextContent(
        "No tienes acceso",
    );
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
    expect(crearUrl).not.toHaveBeenCalled();
    await usuario.click(
        screen.getByRole("button", { name: "Reintentar imagen" }),
    );
    await screen.findByRole("img");
    expect(consultar).toHaveBeenCalledTimes(2);
    vista.unmount();
    expect(liberarUrl).toHaveBeenCalledTimes(1);
});

it("cancela la carga al cerrar y mantiene Skeleton mientras espera la respuesta", async () => {
    const consultar = vi.spyOn(api, "archivo").mockImplementation(
        (_ruta, signal) =>
            new Promise((_resolver, rechazar) => {
                signal?.addEventListener("abort", () =>
                    rechazar(new DOMException("Cancelada", "AbortError")),
                );
            }),
    );
    const vista = render(<VistaPreviaEvidencia idEvidencia={44} automatica />);
    await waitFor(() =>
        expect(screen.getByRole("status")).toHaveTextContent("Cargando imagen"),
    );
    const signal = consultar.mock.calls[0][1];
    vista.unmount();
    expect(signal?.aborted).toBe(true);
    expect(crearUrl).not.toHaveBeenCalled();
});
