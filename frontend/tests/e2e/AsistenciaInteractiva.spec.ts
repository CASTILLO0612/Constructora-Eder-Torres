import { test, expect } from "@playwright/test";
import { permisos } from "../../src/api/permisos";
import { desplazarFecha } from "../../src/features/Inicio/datosGraficos";

test.use({ hasTouch: true });
const fecha = (dia: string) =>
    new Intl.DateTimeFormat("es-NI", {
        timeZone: "America/Managua",
        day: "2-digit",
        month: "short",
        year: "numeric",
    }).format(new Date(`${dia}T12:00:00Z`));

test("el resumen diario cambia al tocar fechas y puntos, usar el teclado y alternar horas", async ({
    page,
}) => {
    const hoy = new Intl.DateTimeFormat("en-CA", {
        timeZone: "America/Managua",
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
    }).format(new Date());
    const ayer = desplazarFecha(hoy, -1);
    const registros = [
        ...Array.from({ length: 151 }, (_, idAsistencia) => ({
            idAsistencia,
            fechaJornada: hoy,
            entradaEn: `${hoy}T14:00:00Z`,
            salidaEn: `${hoy}T15:00:00Z`,
            estado: "COMPLETA",
        })),
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
    await page.route("**/api/**", async (ruta) => {
        const url = new URL(ruta.request().url());
        let datos: unknown = [];
        if (url.pathname === "/api/sesion/actual")
            datos = {
                idUsuario: 987,
                nombreUsuario: "cuenta.grafico",
                roles: ["GERENTE_GENERAL"],
                permisos: Object.values(permisos),
                obrasAsignadas: [],
                tieneAlcanceGlobal: true,
                autenticadoEnUtc: new Date().toISOString(),
            };
        if (url.pathname === "/api/asistencias") {
            const inicio =
                (Number(url.searchParams.get("pagina") || 1) - 1) * 100;
            datos = registros.slice(inicio, inicio + 100);
        }
        await ruta.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify(datos),
        });
    });
    await page.goto("/");
    const panel = page.getByRole("region", { name: "Asistencia del período" });
    const total = panel.locator(".total-asistencia");
    const cabecera = page.locator("header.barra-superior");
    const menu = cabecera.getByRole("button", {
        name: "Alternar menú principal",
    });
    await expect(total).toHaveText("151");
    for (const tema of ["claro", "oscuro"]) {
        await page.evaluate((valor) => {
            document.documentElement.dataset.tema = valor;
        }, tema);
        for (const ancho of [320, 1440]) {
            await page.setViewportSize({ width: ancho, height: 1000 });
            if (ancho <= 700) await page.keyboard.press("Escape");
            await panel
                .getByRole("button", { name: "Registros", exact: true })
                .click();
            await panel
                .getByRole("button", {
                    name: `${fecha(ayer)}: 2 registros`,
                    exact: true,
                })
                .tap();
            await expect(total).toHaveText("2");
            await expect(
                panel.getByRole("link", { name: "Ver registros" }),
            ).toHaveAttribute(
                "href",
                `/asistencia?fechaDesde=${ayer}&fechaHasta=${ayer}`,
            );
            await panel.evaluate((elemento) =>
                window.scrollTo(
                    0,
                    window.scrollY + elemento.getBoundingClientRect().top - 80,
                ),
            );
            await panel.screenshot({
                path: `../artifacts/Frontend/asistencia-diaria-${tema}-${ancho}.png`,
            });
            await page.evaluate(() =>
                window.scrollTo(0, document.body.scrollHeight),
            );
            expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(
                100,
            );
            await expect(cabecera).toBeInViewport();
            await expect(menu).toBeInViewport();
            expect(
                Math.abs((await cabecera.boundingBox())!.y),
            ).toBeLessThanOrEqual(1);
            await menu.click();
            await expect(menu).toHaveAttribute("aria-expanded", "true");
            await page.keyboard.press("Escape");
            await expect(menu).toHaveAttribute("aria-expanded", "false");
            await page.screenshot({
                path: `../artifacts/Frontend/cabecera-fija-${tema}-${ancho}.png`,
            });
            await panel
                .getByRole("button", { name: "Horas", exact: true })
                .click();
            await expect(total).toHaveText("5");
            const sinRegistros = panel.getByRole("button", {
                name: `${fecha(desplazarFecha(hoy, -2))}: 0 h`,
                exact: true,
            });
            await sinRegistros.focus();
            await page.keyboard.press("Enter");
            await expect(total).toHaveText("0");
            const svg = panel.locator(".evolucion-grafico svg");
            await svg.scrollIntoViewIfNeeded();
            const limites = (await svg.boundingBox())!;
            await page.touchscreen.tap(
                limites.x + (limites.width * 616) / 640,
                limites.y + limites.height / 2,
            );
            await expect(total).toHaveText("151");
        }
    }
    await panel
        .getByRole("button", { name: `${fecha(ayer)}: 5 h`, exact: true })
        .click();
    await panel
        .getByRole("combobox", { name: "Período del gráfico de asistencia" })
        .selectOption("14");
    await expect(total).toHaveText("5");
    await expect(
        panel.getByRole("button", { name: `${fecha(ayer)}: 5 h`, exact: true }),
    ).toHaveAttribute("aria-pressed", "true");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
    ).toBe(true);
});
