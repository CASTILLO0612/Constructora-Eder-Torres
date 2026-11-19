import { test, expect, type Page } from "@playwright/test";
import http from "node:http";

test("herramientas: filtro y enlaces mantienen la herramienta y la asignación del historial", async ({
    page,
}) => {
    await ingresar(page, "seguridad");
    await page.goto("/herramientas?vista=asignaciones-herramientas");
    await page
        .getByRole("combobox", { name: "Filtrar por estado", exact: true })
        .selectOption("DEVUELTA");
    const respuesta = await page.request.get(
        "/api/herramientas/asignaciones?estado=DEVUELTA",
    );
    const devueltas = await respuesta.json();
    expect(devueltas.length).toBeGreaterThan(0);
    await expect(page.locator("tbody tr")).toHaveCount(devueltas.length);
    await expect(
        page.locator("tbody").getByText("Activa", { exact: true }),
    ).toHaveCount(0);
    const fila = page.getByRole("row").filter({
        has: page.getByRole("button", {
            name: devueltas[0].codigoInternoHerramienta,
            exact: true,
        }),
    });
    await fila
        .getByRole("button", {
            name: devueltas[0].codigoInternoHerramienta,
            exact: true,
        })
        .click();
    await page
        .getByRole("dialog")
        .getByRole("link", { name: "Historial de la herramienta", exact: true })
        .click();
    await expect(page).toHaveURL(
        new RegExp("idHerramienta=" + devueltas[0].idHerramienta),
    );
    await page.locator("tbody .boton-icono").first().click();
    await page
        .getByRole("dialog")
        .getByRole("link", { name: "Detalle de la asignación", exact: true })
        .click();
    await expect(page).toHaveURL(/registro=/);
    await expect(
        page
            .getByRole("dialog")
            .getByText("Detalle del registro", { exact: true }),
    ).toBeVisible();
});

const escenario = JSON.parse(process.env.CET_E2E_ESCENARIO || "{}");
async function ingresar(page: Page, perfil = "gerencia") {
    await page.goto("/ingresar");
    await page
        .getByLabel("Usuario", { exact: true })
        .fill(escenario.cuentas[perfil]);
    await page
        .getByLabel("Contraseña", { exact: true })
        .fill(escenario.contrasena);
    await page.getByRole("button", { name: "Entrar a la aplicación" }).click();
    await expect(page.locator("main h1")).toBeVisible();
}
async function guardar(page: Page) {
    const d = page.getByRole("dialog");
    const nombre = await d.getByRole("heading").first().innerText();
    await d
        .getByRole("button", { name: /Guardar cambios|Guardar captura/ })
        .click();
    await expect(
        page.getByRole("dialog", { name: nombre, exact: true }),
    ).not.toBeVisible();
}
test("acta: crea borrador, edita desde detalle, revisa, rechaza, corrige y aprueba", async ({
    page,
}) => {
    await ingresar(page);
    const fechaActa = new Intl.DateTimeFormat("en-CA", {
        timeZone: "America/Managua",
    }).format(new Date());
    const tokenActa = await (
        await page.request.get("/api/sesion/antifalsificacion")
    ).json();
    const actividadActa = await page.request.post(
        "/api/seguimiento/actividades",
        {
            headers: { [tokenActa.cabecera]: tokenActa.token },
            data: {
                idObra: escenario.idObra,
                idAsignacionResponsable: escenario.idAsignacion,
                codigo: "ACTA-" + crypto.randomUUID().slice(0, 8),
                nombre: "Actividad del acta de prueba",
                unidadMedida: "m2",
                cantidadPlanificada: 100,
                fechaInicioPlan: fechaActa,
                fechaFinPlan: fechaActa,
            },
        },
    );
    expect(actividadActa.ok()).toBe(true);
    const avanceActa = await page.request.post("/api/seguimiento/avances", {
        headers: { [tokenActa.cabecera]: tokenActa.token },
        data: {
            idActividad: (await actividadActa.json()).idActividad,
            fechaAvance: fechaActa,
            cantidadEjecutada: 1,
        },
    });
    expect(avanceActa.ok()).toBe(true);
    await page.goto("/actas?idObra=" + escenario.idObra);
    await page
        .getByRole("button", { name: "Elaborar acta", exact: true })
        .first()
        .click();
    let d = page.getByRole("dialog");
    await d.getByLabel(/^Número de acta/).fill("ACTA-CICLO-UI");
    const hoy = fechaActa;
    await d.getByLabel(/^Inicio del período/).fill(hoy);
    await d.getByLabel(/^Fin del período/).fill(hoy);
    await guardar(page);
    d = page.getByRole("dialog");
    await expect(
        d.getByRole("button", { name: "Resolver acta", exact: true }),
    ).toHaveCount(0);
    await d.getByRole("button", { name: "Editar acta", exact: true }).click();
    d = page.getByRole("dialog", { name: "Editar acta", exact: true });
    await d
        .getByLabel(/^Observaciones/)
        .fill("Acta de validación del recorrido completo");
    await guardar(page);
    await page
        .getByRole("dialog")
        .getByRole("button", { name: "Enviar a revisión", exact: true })
        .click();
    await guardar(page);
    await page
        .getByRole("dialog")
        .getByRole("button", { name: "Resolver acta", exact: true })
        .click();
    await page
        .getByRole("dialog")
        .getByLabel(/^Decisión/)
        .selectOption("RECHAZADA");
    await page
        .getByRole("dialog")
        .getByLabel(/^Comentario/)
        .fill("Completar detalle semanal");
    await guardar(page);
    await page
        .getByRole("dialog")
        .getByRole("button", { name: "Editar acta", exact: true })
        .click();
    await page
        .getByRole("dialog")
        .getByLabel(/^Observaciones/)
        .fill("Observaciones corregidas");
    await guardar(page);
    await page
        .getByRole("dialog")
        .getByRole("button", { name: "Enviar a revisión", exact: true })
        .click();
    await guardar(page);
    await page
        .getByRole("dialog")
        .getByRole("button", { name: "Resolver acta", exact: true })
        .click();
    await page
        .getByRole("dialog")
        .getByLabel(/^Decisión/)
        .selectOption("APROBADA");
    await guardar(page);
    d = page.getByRole("dialog");
    await expect(
        d.getByRole("button", { name: "Editar acta", exact: true }),
    ).toHaveCount(0);
    await expect(
        d.getByText("Aprobada", { exact: true }).first(),
    ).toBeVisible();
    await d.screenshot({
        path: "../artifacts/ImplementacionDiagramas/acta-aprobada.png",
    });
});
test("actividad: detalle ofrece continuidad y exige justificar antes de encolar", async ({
    page,
}) => {
    await ingresar(page, "residente");
    await page.goto(
        "/seguimiento?vista=actividades&idObra=" + escenario.idObra,
    );
    const actividad = await (
        await page.request.get(
            "/api/seguimiento/actividades/" + escenario.idActividadOffline,
        )
    ).json();
    const fila = page.getByRole("row").filter({
        has: page.getByRole("button", {
            name: actividad.codigo,
            exact: true,
        }),
    });
    await fila
        .getByRole("button", { name: actividad.codigo, exact: true })
        .click();
    let d = page.getByRole("dialog");
    await expect(
        d.getByRole("link", { name: "Avances de la actividad", exact: true }),
    ).toHaveAttribute("href", /idActividad=/);
    await d
        .getByRole("button", { name: "Registrar avance", exact: true })
        .click();
    d = page.getByRole("dialog");
    await d
        .getByRole("checkbox", {
            name: "Trabajo incompleto o retrasado",
            exact: true,
        })
        .check();
    await expect(
        d.getByLabel("Observaciones", { exact: false }),
    ).toHaveAttribute("required", "");
    await d.getByLabel(/^Cantidad ejecutada/).fill("2");
    await d
        .getByRole("button", { name: "Guardar captura", exact: true })
        .click();
    await expect(d).toBeVisible();
    expect(
        await d
            .getByLabel(/^Observaciones/)
            .evaluate((e: HTMLTextAreaElement) => e.validity.valueMissing),
    ).toBe(true);
});
test("lista asistida agrupa por cuadrilla y especialidad y confirma salidas", async ({
    page,
    context,
}) => {
    await ingresar(page, "residente");
    await context.grantPermissions(["geolocation"]);
    await context.setGeolocation({ latitude: 12, longitude: -86, accuracy: 5 });
    const token = await (
        await page.request.get("/api/sesion/antifalsificacion")
    ).json();
    const asignaciones = await (
        await page.request.get(
            `/api/personal/obras/${escenario.idObra}/asignaciones`,
        )
    ).json();
    const asignacion = asignaciones.find(
        (a: { idTrabajador: number }) =>
            a.idTrabajador !== escenario.idTrabajador,
    );
    const entrada = new Date(Date.now() - 60_000);
    const alta = await page.request.post("/api/asistencias", {
        headers: { [token.cabecera]: token.token },
        data: {
            idAsignacion: asignacion.idAsignacion,
            fechaJornada: new Intl.DateTimeFormat("en-CA", {
                timeZone: "America/Managua",
            }).format(entrada),
            entradaEn: entrada.toISOString(),
            latitudEntrada: 12,
            longitudEntrada: -86,
            precisionGpsEntradaMetros: 5,
            uuidRegistro: crypto.randomUUID(),
        },
    });
    expect(alta.ok()).toBe(true);
    const asistencia = await alta.json();
    await page.goto("/asistencia/marcar");
    await page
        .getByRole("combobox", { name: "Obra", exact: true })
        .selectOption(String(escenario.idObra));
    await expect(page.locator(".grupo-asistido h2").first()).toBeVisible();
    await page.getByLabel("Marcación", { exact: true }).selectOption("salida");
    await expect(
        page.getByRole("button", {
            name: "Obtener GPS y registrar salidas",
            exact: true,
        }),
    ).toBeVisible();
    await page.locator(".fila-asistida input[type=checkbox]").first().check();
    await page
        .getByRole("button", {
            name: "Obtener GPS y registrar salidas",
            exact: true,
        })
        .click();
    await expect
        .poll(
            async () =>
                (
                    await (
                        await page.request.get(
                            `/api/asistencias/${asistencia.idAsistencia}`,
                        )
                    ).json()
                ).estado,
        )
        .toBe("COMPLETA");
    await page.setViewportSize({ width: 320, height: 640 });
    await expect
        .poll(() =>
            page.evaluate(
                () => document.documentElement.scrollWidth <= innerWidth + 1,
            ),
        )
        .toBe(true);
    const menu = page.getByRole("button", {
        name: "Alternar menú principal",
        exact: true,
    });
    if ((await menu.getAttribute("aria-expanded")) === "true")
        await page.keyboard.press("Escape");
    await expect
        .poll(() =>
            page
                .locator(".barra-lateral")
                .evaluate((e) => e.getBoundingClientRect().right <= 1),
        )
        .toBe(true);
    await page.screenshot({
        path: "../artifacts/ImplementacionDiagramas/asistencia-320.png",
        fullPage: true,
    });
});
test("hardening: bloquea scripts inline, destinos absolutos y URI malformadas sin interrumpir la PWA", async ({
    page,
    request,
}) => {
    const documento = await page.goto("/ingresar");
    const csp = documento!.headers()["content-security-policy"];
    expect(csp).toContain(
        "script-src 'self' https://maps.googleapis.com https://maps.gstatic.com",
    );
    expect(csp).not.toContain("unsafe-eval");
    expect(documento!.headers()["x-frame-options"]).toBe("DENY");
    expect(documento!.headers()["permissions-policy"]).toContain(
        "geolocation=(self)",
    );
    await page
        .addScriptTag({ content: "window.__scriptInyectado = true" })
        .catch(() => undefined);
    expect(
        await page.evaluate(
            () =>
                (window as unknown as Record<string, unknown>)
                    .__scriptInyectado,
        ),
    ).toBeUndefined();
    const base = new URL(process.env.CET_E2E_URL!);
    const estadoCrudo = (ruta: string) =>
        new Promise<number>((resolve, reject) => {
            const r = http.request(base, { path: ruta }, (respuesta) => {
                respuesta.resume();
                resolve(respuesta.statusCode!);
            });
            r.on("error", reject);
            r.end();
        });
    expect(await estadoCrudo("http://127.0.0.1:1/api/obras")).toBe(400);
    expect(await estadoCrudo("/%E0%A4%A")).toBe(400);
    expect((await request.get("/ingresar")).status()).toBe(200);
    const privada = await request.get("/api/obras");
    expect(privada.status()).toBe(401);
    expect(privada.headers()["cache-control"]).toContain("no-store");
});
