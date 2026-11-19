import { test, expect, type Page } from "@playwright/test";
import { permisos } from "../../src/api/permisos";

async function preparar(page: Page) {
    const consultas: URL[] = [];
    await page.route("**/api/**", async (ruta) => {
        const url = new URL(ruta.request().url());
        let datos: unknown = [];
        if (url.pathname === "/api/sesion/actual")
            datos = {
                idUsuario: 985,
                nombreUsuario: "cuenta.busquedas",
                roles: ["GERENTE_GENERAL"],
                permisos: Object.values(permisos),
                obrasAsignadas: [],
                tieneAlcanceGlobal: true,
                autenticadoEnUtc: new Date().toISOString(),
            };
        if (url.pathname === "/api/obras") {
            consultas.push(url);
            const buscar = url.searchParams.get("buscar");
            if (buscar === "vieja")
                await new Promise((resolver) => setTimeout(resolver, 900));
            datos = buscar
                ? [
                      {
                          idObra: 141,
                          codigo: "CET-141",
                          nombre:
                              buscar === "vieja"
                                  ? "Coincidencia anterior"
                                  : "Edificio Zafiro",
                          estado: "ACTIVA",
                          direccion: "Managua",
                          latitud: 12.14,
                          longitud: -86.25,
                          radioAsistenciaMetros: 100,
                      },
                  ]
                : Array.from(
                      {
                          length:
                              Number(url.searchParams.get("tamanoPagina")) ||
                              25,
                      },
                      (_, i) => ({
                          idObra: i + 1,
                          codigo: `CET-${i + 1}`,
                          nombre: `Obra ${i + 1}`,
                          estado: "ACTIVA",
                          direccion: "Managua",
                      }),
                  );
        }
        await ruta.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify(datos),
        });
    });
    return consultas;
}

test("busca con espera breve, vuelve al primer resultado y descarta respuestas anteriores", async ({
    page,
}) => {
    const consultas = await preparar(page);
    await page.goto("/obras");
    await page.keyboard.press("Escape");
    await expect(
        page.getByRole("heading", { name: "Obras", exact: true }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Siguiente", exact: true }).click();
    await expect
        .poll(() => consultas.at(-1)?.searchParams.get("pagina"))
        .toBe("2");
    const buscar = page.getByRole("textbox", {
        name: "Buscar registros",
        exact: true,
    });
    await buscar.fill("za");
    await expect(
        page.getByText("Escribe al menos 3 caracteres para buscar."),
    ).toBeVisible();
    const antes = consultas.length;
    await page.waitForTimeout(400);
    expect(consultas.length).toBe(antes);
    await buscar.fill("vieja");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.get("buscar"))
        .toBe("vieja");
    await buscar.fill("zafiro");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.get("buscar"))
        .toBe("zafiro");
    expect(consultas.at(-1)?.searchParams.get("pagina")).toBe("1");
    await expect(
        page.getByText("Edificio Zafiro", { exact: true }).first(),
    ).toBeVisible();
    await page.waitForTimeout(1000);
    await expect(
        page.getByText("Coincidencia anterior", { exact: true }),
    ).toHaveCount(0);
    await page
        .getByRole("combobox", { name: "Filtrar por estado" })
        .selectOption("ACTIVA");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.get("estado"))
        .toBe("ACTIVA");
    expect(consultas.at(-1)?.searchParams.get("buscar")).toBe("zafiro");
    for (const tema of ["claro", "oscuro"])
        for (const width of [320, 1440]) {
            await page.setViewportSize({ width, height: 900 });
            await page.evaluate(
                (tema) => (document.documentElement.dataset.tema = tema),
                tema,
            );
            await page.keyboard.press("Escape");
            await expect(buscar).toBeVisible();
            expect(
                await page.evaluate(
                    () =>
                        document.documentElement.scrollWidth <= innerWidth + 1,
                ),
            ).toBe(true);
            await page.screenshot({
                path: `../artifacts/Frontend/busquedas-${tema}-${width}.png`,
                fullPage: true,
            });
        }
    await page
        .getByRole("button", { name: "Limpiar búsqueda: registros" })
        .click();
    await expect(buscar).toHaveValue("");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.has("buscar"))
        .toBe(false);
});

test("los desplegables encuentran una obra fuera de las opciones cargadas y conservan su selección", async ({
    page,
}) => {
    const consultas = await preparar(page);
    await page.goto("/asistencia");
    await page.keyboard.press("Escape");
    const buscar = page.getByRole("textbox", {
        name: "Buscar obra",
        exact: true,
    });
    await expect(buscar).toBeVisible();
    await buscar.fill("zafiro");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.get("buscar"))
        .toBe("zafiro");
    expect(consultas.at(-1)?.searchParams.get("tamanoPagina")).toBe("50");
    const selector = page.getByRole("combobox", {
        name: "Filtrar por obra",
        exact: true,
    });
    await expect(
        selector.getByRole("option", { name: "Edificio Zafiro", exact: false }),
    ).toHaveCount(1);
    await selector.selectOption("141");
    await page.getByRole("button", { name: "Limpiar búsqueda: obra" }).click();
    await expect(selector).toHaveValue("141");
    await expect(
        selector.getByRole("option", { name: "Edificio Zafiro", exact: false }),
    ).toHaveCount(1);
});
