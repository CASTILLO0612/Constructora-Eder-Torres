import { test, expect } from "@playwright/test";
import { permisos } from "../../src/api/permisos";

test("auditoría permite consultar fechas locales y bloquea un rango invertido antes del envío", async ({
    page,
}) => {
    const consultas: URL[] = [];
    await page.route("**/api/**", async (ruta) => {
        const url = new URL(ruta.request().url());
        let datos: unknown = [];
        if (url.pathname === "/api/sesion/actual")
            datos = {
                idUsuario: 987,
                nombreUsuario: "cuenta.fechas",
                roles: ["GERENTE_GENERAL"],
                permisos: Object.values(permisos),
                obrasAsignadas: [],
                tieneAlcanceGlobal: true,
                autenticadoEnUtc: new Date().toISOString(),
            };
        if (url.pathname === "/api/auditorias") {
            consultas.push(url);
            const desde = url.searchParams.get("desdeUtc");
            const hasta = url.searchParams.get("hastaUtc");
            expect(!desde || desde.endsWith("Z")).toBe(true);
            expect(!hasta || hasta.endsWith("Z")).toBe(true);
            expect(
                !desde || !hasta || Date.parse(hasta) >= Date.parse(desde),
            ).toBe(true);
        }
        await ruta.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify(datos),
        });
    });
    await page.goto("/auditoria");
    await expect(
        page.getByRole("heading", { name: "Auditoría", exact: true }),
    ).toBeVisible();
    const desde = page.getByLabel("Desde", { exact: true });
    const hasta = page.getByLabel("Hasta", { exact: true });
    await desde.fill("2026-11-19T00:00");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.get("desdeUtc"))
        .toBe("2026-11-19T06:00:00.000Z");
    await hasta.fill("2026-11-19T23:59");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.get("hastaUtc"))
        .toBe("2026-11-20T05:59:59.999Z");
    await expect(page.getByRole("alert")).toHaveCount(0);
    const enviadas = consultas.length;
    await hasta.fill("2026-11-18T23:59");
    await expect(page.getByRole("alert")).toContainText(
        "La fecha «Hasta» debe ser igual o posterior a «Desde».",
    );
    await expect(hasta).toHaveAttribute("aria-invalid", "true");
    await expect(
        page.getByRole("button", { name: "Actualizar registros" }),
    ).toBeDisabled();
    expect(consultas).toHaveLength(enviadas);
    await hasta.fill("2026-11-19T23:59");
    await expect(page.getByRole("alert")).toHaveCount(0);
    await desde.fill("");
    await expect
        .poll(() => consultas.at(-1)?.searchParams.has("desdeUtc"))
        .toBe(false);
    await hasta.fill("");
    await expect(hasta).toHaveValue("");
    await expect(page.getByRole("alert")).toHaveCount(0);
    for (const ancho of [320, 1440]) {
        await page.setViewportSize({ width: ancho, height: 900 });
        await page.keyboard.press("Escape");
        await desde.fill("2026-11-19T00:00");
        await hasta.fill("2026-11-19T23:59");
        await expect(page.getByRole("alert")).toHaveCount(0);
        expect(
            await page.evaluate(
                () => document.documentElement.scrollWidth <= innerWidth + 1,
            ),
        ).toBe(true);
        await page.screenshot({
            path: `../artifacts/Frontend/auditoria-fechas-${ancho}.png`,
            fullPage: true,
        });
    }
});
