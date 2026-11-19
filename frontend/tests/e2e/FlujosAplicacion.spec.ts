import { test, expect, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
const escenario = JSON.parse(process.env.CET_E2E_ESCENARIO || "{}");
test.describe.configure({ mode: "default" });

test("recupera la sesión y las consultas con red disponible tras una caída de la API", async ({
    browser,
}) => {
    const contexto = await browser.newContext({
        baseURL: process.env.CET_E2E_URL,
        viewport: { width: 390, height: 844 },
    });
    await contexto.addInitScript(() => {
        Object.defineProperty(AbortSignal, "any", {
            configurable: true,
            value: undefined,
        });
        Object.defineProperty(AbortSignal, "timeout", {
            configurable: true,
            value: undefined,
        });
    });
    const page = await contexto.newPage();
    try {
        await ingresar(page);
        await expect(
            page.getByRole("heading", { name: "Resumen general", exact: true }),
        ).toBeVisible();
        let interrumpido = true;
        await page.route("**/api/**", (ruta) =>
            interrumpido ? ruta.abort("connectionrefused") : ruta.continue(),
        );
        await page.reload();
        await expect(
            page.getByText("Conexión pendiente de verificar.", {
                exact: false,
            }),
        ).toBeVisible();
        expect(await page.evaluate(() => navigator.onLine)).toBe(true);
        interrumpido = false;
        await expect(
            page.getByText("Conexión pendiente de verificar.", {
                exact: false,
            }),
        ).toHaveCount(0, { timeout: 25000 });
        await expect(
            page.getByRole("heading", { name: "Resumen general", exact: true }),
        ).toBeVisible();
        await expect(page.getByRole("alert")).toHaveCount(0);
        await page.screenshot({
            path: "../artifacts/Frontend/conexion-movil-recuperada.png",
            fullPage: true,
        });
    } finally {
        await contexto.close();
    }
});
// Solo reintenta reinicios de conexión en verificaciones GET; nunca escrituras.
const consultar = (page: Page, ruta: string) =>
    page.request.get(ruta, { maxRetries: 2 });
const horaCaptura = () =>
    new Intl.DateTimeFormat("sv-SE", {
        timeZone: "America/Managua",
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
    })
        .format(new Date())
        .replace(" ", "T");
async function ingresar(page: Page, perfil = "gerencia") {
    if (!escenario.cuentas)
        throw new Error(
            "Ejecuta npm run test:e2e para crear el entorno PostgreSQL desechable.",
        );
    await page.goto("/");
    await page
        .getByLabel("Usuario", { exact: true })
        .fill(escenario.cuentas[perfil]);
    await page
        .getByLabel("Contraseña", { exact: true })
        .fill(escenario.contrasena);
    await page.getByRole("button", { name: "Entrar a la aplicación" }).click();
    await expect(page.locator("main#contenido")).toBeVisible();
    await expect(page).not.toHaveURL(/\/ingresar/);
}
async function seleccionarObra(page: Page) {
    await page
        .getByRole("combobox", { name: "Filtrar por obra" })
        .selectOption(String(escenario.idObra));
}
async function guardarDialogo(page: Page) {
    const dialogo = page.getByRole("dialog");
    const titulo = await dialogo.getByRole("heading").first().innerText();
    await dialogo
        .getByRole("button", { name: /Guardar cambios|Guardar captura/ })
        .click();
    await expect(
        page.getByRole("dialog", { name: titulo, exact: true }),
    ).not.toBeVisible();
    const detalle = page.getByRole("dialog").filter({
        has: page.getByText("Detalle del registro", { exact: true }),
    });
    if (await detalle.isVisible())
        await detalle
            .getByRole("button", { name: "Cerrar", exact: true })
            .click();
}
async function esperarConfirmacion(page: Page, coleccion: string) {
    await expect
        .poll(async () => {
            const r = await consultar(page, coleccion);
            return r.ok() ? (await r.json()).length : 0;
        })
        .toBeGreaterThan(0);
}

test("personal: crea un trabajador y lo asigna a una obra por nombre", async ({
    page,
}) => {
    await ingresar(page);
    await page.goto("/personal");
    await page
        .getByRole("button", { name: "Registrar trabajador", exact: true })
        .first()
        .click();
    let d = page.getByRole("dialog");
    await elegirPrimera(
        d.getByRole("combobox", { name: "Cargo", exact: true }),
    );
    await d.getByLabel(/^Cédula/).fill("FRONT-001");
    await d.getByLabel(/^Primer nombre/).fill("Mateo");
    await d.getByLabel(/^Primer apellido/).fill("Integracion");
    await guardarDialogo(page);
    await expect(
        page.getByRole("button", { name: "Mateo Integracion", exact: true }),
    ).toBeVisible();
    const trabajador = (
        await (await consultar(page, "/api/personal/trabajadores")).json()
    ).find((t: { cedula: string }) => t.cedula === "FRONT-001");
    await page.goto("/personal?vista=asignaciones");
    await seleccionarObra(page);
    await page
        .getByRole("button", { name: "Asignar trabajador", exact: true })
        .first()
        .click();
    d = page.getByRole("dialog");
    await d
        .getByRole("combobox", { name: "Trabajador", exact: true })
        .selectOption(String(trabajador.idTrabajador));
    await d.getByLabel(/^Tipo de contratación/).selectOption("FIJO");
    await guardarDialogo(page);
    await expect(
        page.getByRole("button", { name: "Mateo Integracion", exact: true }),
    ).toBeVisible();
});

async function elegirPrimera(control: ReturnType<Page["getByRole"]>) {
    await expect(control).toBeEnabled();
    await expect
        .poll(() => control.locator("option").count())
        .toBeGreaterThan(1);
    await control.selectOption(
        (await control.locator("option").nth(1).getAttribute("value"))!,
    );
}

test("Residente: prepara la obra y sincroniza un avance con evidencia capturados offline", async ({
    browser,
}) => {
    const c = await browser.newContext({ baseURL: process.env.CET_E2E_URL });
    const page = await c.newPage();
    await ingresar(page, "residente");
    await page.goto("/seguimiento?vista=avances");
    await seleccionarObra(page);
    await page
        .getByRole("button", { name: "Preparar obra sin conexión" })
        .click();
    await expect(
        page.getByText(
            "La obra y sus catálogos se prepararon para capturas sin conexión.",
            { exact: false },
        ),
    ).toBeVisible({ timeout: 30000 });
    await c.setOffline(true);
    await page
        .getByRole("button", { name: "Registrar avance", exact: true })
        .first()
        .click();
    let d = page.getByRole("dialog");
    await d
        .getByRole("combobox", { name: "Actividad", exact: true })
        .selectOption(String(escenario.idActividadOffline));
    await d.getByLabel(/^Cantidad ejecutada/).fill("3");
    await guardarDialogo(page);
    await page.getByRole("button", { name: "Evidencias", exact: true }).click();
    await page
        .getByRole("button", { name: "Añadir evidencia", exact: true })
        .first()
        .click();
    d = page.getByRole("dialog");
    const avancePendiente = d.getByRole("combobox", {
        name: "Avance",
        exact: true,
    });
    await expect
        .poll(() =>
            avancePendiente.locator('option[value^="pendiente:"]').count(),
        )
        .toBeGreaterThan(0);
    await avancePendiente.selectOption(
        (await avancePendiente
            .locator('option[value^="pendiente:"]')
            .first()
            .getAttribute("value"))!,
    );
    await d.locator("input[type=file]").setInputFiles({
        name: "offline-e2e.png",
        mimeType: "image/png",
        buffer: Buffer.from(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
            "base64",
        ),
    });
    await d.getByLabel(/^Fecha y hora de captura/).fill(horaCaptura());
    await d.getByLabel(/^Latitud/).fill("12");
    await d.getByLabel(/^Longitud/).fill("-86");
    await guardarDialogo(page);
    await c.setOffline(false);
    await expect
        .poll(
            async () => {
                const registros = await (
                    await consultar(
                        page,
                        `/api/seguimiento/obras/${escenario.idObra}/avances`,
                    )
                ).json();
                const a = registros.find(
                    (a: { idActividad: number }) =>
                        a.idActividad === escenario.idActividadOffline,
                );
                if (!a) return 0;
                const fotos = await (
                    await consultar(
                        page,
                        `/api/seguimiento/avances/${a.idAvance}/evidencias`,
                    )
                ).json();
                return fotos.length;
            },
            { timeout: 40000 },
        )
        .toBe(1);
    await c.close();
});

test("Higiene y Seguridad entrega y devuelve EPP y herramientas", async ({
    page,
}) => {
    await ingresar(page, "seguridad");
    await page.goto("/epp?vista=entregas");
    await seleccionarObra(page);
    await page
        .getByRole("button", { name: "Registrar entrega", exact: true })
        .first()
        .click();
    let d = page.getByRole("dialog");
    await elegirPrimera(
        d.getByRole("combobox", { name: "Equipo disponible", exact: true }),
    );
    await d
        .getByRole("combobox", {
            name: "Asignación de trabajador",
            exact: true,
        })
        .selectOption(String(escenario.idAsignacion));
    await guardarDialogo(page);
    await esperarConfirmacion(page, "/api/epp/entregas");
    await page.getByRole("button", { name: "Actualizar registros" }).click();
    await expect(page.locator("tbody tr")).toHaveCount(1);
    await page.locator("tbody tr").first().getByLabel("Más acciones").click();
    await page.getByRole("menuitem", { name: "Registrar devolución" }).click();
    d = page.getByRole("dialog");
    await d.getByLabel(/^Condición de devolución/).selectOption("BUENO");
    await guardarDialogo(page);
    await expect
        .poll(async () => {
            const r = await (await consultar(page, "/api/epp/entregas")).json();
            return r[0]?.estado;
        })
        .toBe("DEVUELTA");
    await page.goto("/herramientas?vista=asignaciones-herramientas");
    await seleccionarObra(page);
    await page
        .getByRole("button", { name: "Asignar herramienta", exact: true })
        .first()
        .click();
    d = page.getByRole("dialog");
    await elegirPrimera(
        d.getByRole("combobox", { name: "Herramienta", exact: true }),
    );
    await d
        .getByRole("combobox", {
            name: "Responsable asignado a obra",
            exact: true,
        })
        .selectOption(String(escenario.idAsignacion));
    await guardarDialogo(page);
    await esperarConfirmacion(page, "/api/herramientas/asignaciones");
    await page.getByRole("button", { name: "Actualizar registros" }).click();
    await expect(page.locator("tbody tr")).toHaveCount(1);
    await page.locator("tbody tr").first().getByLabel("Más acciones").click();
    await page
        .getByRole("menuitem", { name: "Devolución o incidencia" })
        .click();
    d = page.getByRole("dialog");
    await d.getByLabel(/^Tipo de incidencia/).selectOption("DEVOLUCION");
    await d.getByLabel(/^Descripción/).fill("Devolución verificada en obra");
    await guardarDialogo(page);
    await expect
        .poll(async () => {
            const r = await (
                await consultar(page, "/api/herramientas/asignaciones")
            ).json();
            return r[0]?.estado;
        })
        .toBe("DEVUELTA");
});

test("acceso real, errores de credenciales y diseño accesible", async ({
    page,
}) => {
    await page.goto("/");
    await page.getByLabel("Usuario", { exact: true }).fill("no.existe");
    await page
        .getByLabel("Contraseña", { exact: true })
        .fill("clave-incorrecta");
    await page.getByRole("button", { name: "Entrar a la aplicación" }).click();
    await expect(page.getByRole("alert")).toBeVisible();
    const accesibilidad = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
        .analyze();
    expect(accesibilidad.violations).toEqual([]);
    await ingresar(page);
    await expect(
        page.getByRole("heading", { name: "Resumen general" }),
    ).toBeVisible();
    await page.screenshot({
        path: "../artifacts/Frontend/resumen-escritorio.png",
        fullPage: true,
    });
    await page.goto("/ajustes");
    await page.getByRole("radio", { name: /^Oscuro/ }).check();
    await expect(page.locator("html")).toHaveAttribute("data-tema", "oscuro");
    await page.goto("/");
    await expect(
        page.getByRole("heading", { name: "Resumen general", exact: true }),
    ).toBeVisible();
    await page.screenshot({
        path: "../artifacts/Frontend/resumen-oscuro.png",
        fullPage: true,
    });
});
test("panel interactivo y ajustes conservan una navegación limpia en móvil", async ({
    page,
}) => {
    await ingresar(page);
    const barra = page.locator("header.barra-superior");
    await expect(barra.getByRole("button")).toHaveCount(1);
    await expect(
        barra.getByRole("button", { name: "Alternar menú principal" }),
    ).toBeVisible();
    await expect(barra.getByRole("combobox")).toHaveCount(0);
    const estados = page.getByRole("region", { name: "Estado de las obras" });
    await expect(estados.locator(".anillo-grafico svg")).toBeVisible();
    const activa = estados.getByRole("button", { name: /^Activa / });
    await activa.click();
    await expect(activa).toHaveAttribute("aria-pressed", "true");
    await expect(
        estados.getByRole("link", { name: "Explorar obras" }),
    ).toHaveAttribute("href", "/obras?estado=ACTIVA");
    await estados.getByRole("link", { name: "Explorar obras" }).click();
    await expect(
        page.getByRole("combobox", { name: "Filtrar por estado" }),
    ).toHaveValue("ACTIVA");
    await page.goto("/");
    const asistencia = page.getByRole("region", {
        name: "Asistencia del período",
    });
    await asistencia
        .getByRole("button", { name: "Horas", exact: true })
        .click();
    await expect(
        asistencia.getByRole("button", { name: "Horas", exact: true }),
    ).toHaveAttribute("aria-pressed", "true");
    await asistencia
        .getByRole("combobox", { name: "Período del gráfico de asistencia" })
        .selectOption("14");
    await expect(asistencia.locator(".dias-grafico button")).toHaveCount(14);
    const avance = page.getByRole("region", { name: "Avance por actividad" });
    await avance
        .getByRole("combobox", { name: "Obra del gráfico de avance" })
        .selectOption(String(escenario.idObra));
    await expect(avance.locator(".filas-barras button").first()).toBeVisible();
    await avance.locator(".filas-barras button").first().click();
    await expect(avance.locator(".detalle-barra")).toBeVisible();
    const panelAccesible = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
        .analyze();
    expect(panelAccesible.violations).toEqual([]);
    await page.setViewportSize({ width: 360, height: 800 });
    await page.reload();
    const accesoRapido = page.getByRole("navigation", {
        name: "Accesos rápidos",
    });
    await expect(
        accesoRapido.getByRole("link", { name: "Ajustes", exact: true }),
    ).toBeVisible();
    await expect(
        accesoRapido.getByRole("button", { name: "Menú", exact: true }),
    ).toHaveCount(0);
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= window.innerWidth + 1,
        ),
    ).toBe(true);
    await accesoRapido
        .getByRole("link", { name: "Ajustes", exact: true })
        .click();
    await page.getByRole("radio", { name: /^Oscuro/ }).check();
    await page.reload();
    await expect(page.locator("html")).toHaveAttribute("data-tema", "oscuro");
    await expect(page.getByRole("radio", { name: /^Oscuro/ })).toBeChecked();
    const accesibilidad = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
        .analyze();
    expect(accesibilidad.violations).toEqual([]);
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= window.innerWidth + 1,
        ),
    ).toBe(true);
});

test("crea una obra desde el formulario y la recupera de la API", async ({
    page,
}) => {
    await ingresar(page);
    await page
        .getByRole("link", { name: "Obras", exact: true })
        .first()
        .click();
    await page.getByRole("button", { name: "Nueva obra", exact: true }).click();
    const d = page.getByRole("dialog");
    await d.getByLabel(/^Código/).fill("CET-E2E-FRONTEND");
    await d.getByLabel(/^Nombre/).fill("Edificio de integración");
    await d.getByLabel(/^Dirección/).fill("Managua, ubicación de pruebas");
    await d.getByLabel(/^Fin planificado/).fill("2027-12-31");
    await d.getByLabel(/^Latitud/).fill("12");
    await d.getByLabel(/^Longitud/).fill("-86");
    await d.getByLabel(/^Radio de asistencia/).fill("100");
    await guardarDialogo(page);
    await expect(
        page.getByRole("button", { name: "CET-E2E-FRONTEND" }),
    ).toBeVisible();
    const obras = await (
        await consultar(page, "/api/obras?tamanoPagina=100")
    ).json();
    expect(
        obras.some((o: { codigo: string }) => o.codigo === "CET-E2E-FRONTEND"),
    ).toBe(true);
});
test("trabajador: GPS conservador, entrada/salida, offline y reenvío sin duplicados", async ({
    browser,
}) => {
    test.setTimeout(90_000);
    test.setTimeout(90_000);
    const contexto = await browser.newContext({
        baseURL: process.env.CET_E2E_URL,
        permissions: ["geolocation"],
        geolocation: { latitude: 12, longitude: -86, accuracy: 200 },
        viewport: { width: 390, height: 844 },
        locale: "es-NI",
        timezoneId: "America/Managua",
    });
    const page = await contexto.newPage();
    await ingresar(page, "trabajador");
    await expect(page.getByRole("heading", { name: /Hola/ })).toBeVisible();
    const marcacion = page.getByRole("region", { name: "Mi jornada" });
    await expect(marcacion).toBeVisible();
    await expect(marcacion.getByRole("button")).toHaveCount(1);
    await expect(
        marcacion.getByText("Ubicación pendiente", { exact: true }),
    ).toBeVisible();
    for (const tema of ["claro", "oscuro"]) {
        await page.evaluate((valor) => {
            document.documentElement.dataset.tema = valor;
        }, tema);
        for (const ancho of [320, 390, 768, 1440]) {
            await page.setViewportSize({ width: ancho, height: 844 });
            await expect(marcacion.getByRole("button")).toBeInViewport();
            expect(
                await page.evaluate(
                    () =>
                        document.documentElement.scrollWidth <= innerWidth + 1,
                ),
            ).toBe(true);
            if (ancho === 320 || ancho === 1440) {
                await marcacion.screenshot({
                    path: `../artifacts/Frontend/marcacion-unificada-${tema}-${ancho}.png`,
                });
            }
        }
    }
    await page.setViewportSize({ width: 390, height: 844 });
    const accesibilidadMarcacion = await new AxeBuilder({ page })
        .include(".tarjeta-marcacion")
        .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
        .analyze();
    expect(accesibilidadMarcacion.violations).toEqual([]);
    await page.screenshot({
        path: "../artifacts/Frontend/marcacion-unificada-movil.png",
        fullPage: true,
    });
    await page
        .getByRole("button", { name: "Verificar ubicación", exact: true })
        .click();
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(
        page.getByRole("button", { name: "Registrar entrada" }),
    ).toHaveCount(0);
    await contexto.setGeolocation({
        latitude: 12,
        longitude: -86,
        accuracy: 5,
    });
    await expect(marcacion.getByRole("button")).toHaveCount(1);
    await page
        .getByRole("button", { name: "Reintentar ubicación", exact: true })
        .click();
    await expect(
        page.getByRole("button", { name: "Registrar entrada" }),
    ).toBeEnabled();
    await expect(
        marcacion.getByText("Dentro del radio de la obra", { exact: true }),
    ).toBeVisible();
    await expect(marcacion.getByRole("button")).toHaveCount(1);
    await page.getByRole("button", { name: "Registrar entrada" }).click();
    await expect
        .poll(
            async () => {
                const filas = await (
                    await consultar(page, "/api/asistencias")
                ).json();
                return filas.length;
            },
            { timeout: 40_000 },
        )
        .toBe(1);
    await expect(
        page.getByRole("button", { name: "Registrar salida" }),
    ).toBeEnabled();
    await page.waitForTimeout(1200);
    await page.getByRole("button", { name: "Registrar salida" }).click();
    await expect
        .poll(
            async () => {
                const filas = await (
                    await consultar(page, "/api/asistencias")
                ).json();
                return filas[0]?.estado;
            },
            { timeout: 40_000 },
        )
        .toBe("COMPLETA");
    await page.evaluate(() => navigator.serviceWorker.ready);
    await page.waitForTimeout(1200);
    await contexto.setOffline(true);
    await page.reload();
    await expect(page.getByRole("heading", { name: /Hola/ })).toBeVisible();
    await page
        .getByRole("button", { name: "Verificar ubicación", exact: true })
        .click();
    await page.getByRole("button", { name: "Registrar entrada" }).click();
    await expect(
        page.getByRole("button", { name: "Registrar salida" }),
    ).toBeEnabled();
    await page.waitForTimeout(1200);
    await page.getByRole("button", { name: "Registrar salida" }).click();
    await page.screenshot({
        path: "../artifacts/Frontend/asistencia-movil-offline.png",
        fullPage: true,
    });
    await contexto.setOffline(false);
    await page
        .getByRole("link", { name: "Pendientes", exact: true })
        .first()
        .click();
    await expect
        .poll(
            async () => {
                const filas = await (
                    await consultar(page, "/api/asistencias")
                ).json();
                return filas.filter(
                    (f: { estado: string }) => f.estado === "COMPLETA",
                ).length;
            },
            { timeout: 40_000 },
        )
        .toBe(2);
    await expect(
        page.getByRole("heading", { name: "Pendientes y sincronización" }),
    ).toBeVisible();
    await page
        .getByRole("button", { name: "Confirmadas", exact: true })
        .click();
    await expect(page.locator(".operacion-local")).toHaveCount(4);
    await expect(page.locator(".operacion-local .estado")).toHaveText([
        "Confirmada",
        "Confirmada",
        "Confirmada",
        "Confirmada",
    ]);
    await expect(page.locator(".operacion-local details")).toHaveCount(0);
    await expect(
        page.locator(".motivo-operacion, .pie-aplicacion"),
    ).toHaveCount(0);
    await expect(page.locator("main")).not.toContainText(
        /servidor|acuse|uuid|intentos|registro confirmado|\/api\//i,
    );
    for (const tema of ["claro", "oscuro"]) {
        await page.evaluate((valor) => {
            document.documentElement.dataset.tema = valor;
        }, tema);
        for (const ancho of [320, 390, 1440]) {
            await page.setViewportSize({ width: ancho, height: 844 });
            expect(
                await page.evaluate(
                    () =>
                        document.documentElement.scrollWidth <= innerWidth + 1,
                ),
            ).toBe(true);
            await page.screenshot({
                path: `../artifacts/Frontend/asistencia-confirmada-${tema}-${ancho}.png`,
                fullPage: true,
            });
        }
    }
    await page.goto("/personal");
    await expect(
        page.getByRole("heading", { name: "Acceso restringido" }),
    ).toBeVisible();
    await contexto.close();
});
test("avance y evidencia fotográfica conservan el vínculo y los metadatos", async ({
    page,
}) => {
    test.setTimeout(90_000);
    await ingresar(page);
    await page.goto("/seguimiento?vista=avances");
    await seleccionarObra(page);
    await page
        .getByRole("button", { name: "Registrar avance", exact: true })
        .first()
        .click();
    let d = page.getByRole("dialog");
    await d
        .getByRole("combobox", { name: "Actividad", exact: true })
        .selectOption({ label: "Actividad de pruebas · Planificada" });
    await d.getByLabel(/^Cantidad ejecutada/).fill("5");
    await guardarDialogo(page);
    await esperarConfirmacion(
        page,
        `/api/seguimiento/obras/${escenario.idObra}/avances`,
    );
    await page.goto("/seguimiento?vista=evidencias");
    await seleccionarObra(page);
    await page
        .getByRole("button", { name: "Añadir evidencia", exact: true })
        .first()
        .click();
    d = page.getByRole("dialog");
    const avance = await (
        await consultar(
            page,
            `/api/seguimiento/obras/${escenario.idObra}/avances`,
        )
    ).json();
    await d
        .getByRole("combobox", { name: "Avance", exact: true })
        .selectOption(String(avance[0].idAvance));
    const imagenPrueba = await page.evaluate(() => {
        const lienzo = document.createElement("canvas");
        lienzo.width = 960;
        lienzo.height = 540;
        const dibujo = lienzo.getContext("2d")!;
        dibujo.fillStyle = "#f2f2f2";
        dibujo.fillRect(0, 0, 960, 540);
        dibujo.fillStyle = "#222";
        dibujo.font = "32px sans-serif";
        dibujo.fillText("Evidencia de prueba", 64, 82);
        dibujo.fillRect(64, 150, 832, 280);
        dibujo.fillStyle = "#fafafa";
        for (let i = 0; i < 6; i++) dibujo.fillRect(96 + i * 130, 190, 90, 150);
        dibujo.fillStyle = "#555";
        dibujo.font = "22px sans-serif";
        dibujo.fillText(
            "Imagen sintética para verificar la vista previa",
            64,
            490,
        );
        return lienzo.toDataURL("image/png").split(",")[1];
    });
    await d.locator("input[type=file]").setInputFiles({
        name: "evidencia-e2e.png",
        mimeType: "image/png",
        buffer: Buffer.from(imagenPrueba, "base64"),
    });
    await d.getByLabel(/^Fecha y hora de captura/).fill(horaCaptura());
    await d.getByLabel(/^Latitud/).fill("12");
    await d.getByLabel(/^Longitud/).fill("-86");
    await guardarDialogo(page);
    await esperarConfirmacion(
        page,
        `/api/seguimiento/avances/${avance[0].idAvance}/evidencias`,
    );
    const evidencias = await (
        await consultar(
            page,
            `/api/seguimiento/avances/${avance[0].idAvance}/evidencias`,
        )
    ).json();
    const idEvidencia = Math.max(
        ...evidencias.map((e: { idEvidencia: number }) => e.idEvidencia),
    );
    let descargas = 0;
    page.on("download", () => descargas++);
    await page.goto(
        `/seguimiento?vista=evidencias&idObra=${escenario.idObra}&idAvance=${avance[0].idAvance}&registro=${idEvidencia}`,
    );
    d = page.getByRole("dialog");
    const visor = d.getByRole("region", { name: "Vista previa de evidencia" });
    const imagen = visor.getByRole("img");
    await expect(imagen).toBeVisible();
    await expect
        .poll(() =>
            imagen.evaluate(
                (elemento: HTMLImageElement) => elemento.naturalWidth,
            ),
        )
        .toBe(960);
    expect(descargas).toBe(0);
    const anchoNormal = await imagen.evaluate(
        (elemento) => elemento.getBoundingClientRect().width,
    );
    await visor.getByRole("button", { name: "Ampliar imagen" }).click();
    await expect(
        visor.getByRole("button", { name: "Reducir imagen" }),
    ).toHaveAttribute("aria-pressed", "true");
    expect(
        await imagen.evaluate(
            (elemento) => elemento.getBoundingClientRect().width,
        ),
    ).toBeGreaterThan(anchoNormal * 1.9);
    const imagenAmpliada = visor.getByRole("group", {
        name: "Imagen ampliada con desplazamiento",
    });
    await imagenAmpliada.focus();
    await imagenAmpliada.press("ArrowRight");
    await expect
        .poll(() => imagenAmpliada.evaluate((elemento) => elemento.scrollLeft))
        .toBeGreaterThan(0);
    await visor.getByRole("button", { name: "Reducir imagen" }).click();
    for (const tema of ["claro", "oscuro"]) {
        await page.evaluate((valor) => {
            document.documentElement.dataset.tema = valor;
        }, tema);
        for (const ancho of [320, 390, 1440]) {
            await page.setViewportSize({ width: ancho, height: 900 });
            expect(
                await d.evaluate(
                    (dialogo) => dialogo.scrollWidth <= dialogo.clientWidth + 1,
                ),
            ).toBe(true);
            if (ancho !== 320)
                await visor.screenshot({
                    path: `../artifacts/Frontend/evidencia-guardada-${tema}-${ancho}.png`,
                });
            if (ancho === 390) {
                await visor
                    .getByRole("button", { name: "Ampliar imagen" })
                    .click();
                expect(
                    await d.evaluate(
                        (dialogo) =>
                            dialogo.scrollWidth <= dialogo.clientWidth + 1,
                    ),
                ).toBe(true);
                await visor.screenshot({
                    path: `../artifacts/Frontend/evidencia-ampliada-${tema}-390.png`,
                });
                await visor
                    .getByRole("button", { name: "Reducir imagen" })
                    .click();
            }
        }
    }
    const accesibilidadImagen = await new AxeBuilder({ page })
        .include(".visor-evidencia")
        .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
        .analyze();
    expect(accesibilidadImagen.violations).toEqual([]);
    await page.emulateMedia({ media: "print" });
    await expect(
        visor.getByRole("button", { name: "Ampliar imagen" }),
    ).not.toBeVisible();
    await expect(imagen).toBeVisible();
    await page.emulateMedia({ media: "screen" });
    const descarga = page.waitForEvent("download");
    await d
        .getByRole("button", { name: "Descargar archivo", exact: true })
        .click();
    expect((await descarga).suggestedFilename()).toMatch(/\.png$/i);
    await page.goto(
        `/seguimiento?vista=avances&idObra=${escenario.idObra}&registro=${avance[0].idAvance}`,
    );
    d = page.getByRole("dialog");
    await d
        .getByRole("button", { name: "Ver imagen", exact: true })
        .first()
        .click();
    await expect(
        d
            .getByRole("region", { name: "Vista previa de evidencia" })
            .getByRole("img"),
    ).toBeVisible();
});
test("acta semanal reúne avances y se resuelve desde el rol autorizado", async ({
    page,
}) => {
    await ingresar(page);
    await page.goto("/actas");
    await seleccionarObra(page);
    await page
        .getByRole("button", { name: "Elaborar acta", exact: true })
        .first()
        .click();
    const d = page.getByRole("dialog");
    await d.getByLabel(/^Número de acta/).fill("ACTA-FRONTEND");
    await d.getByLabel(/^Fin del período/).fill(
        new Intl.DateTimeFormat("en-CA", {
            timeZone: "America/Managua",
        }).format(new Date()),
    );
    const avancesDelActa = d.getByRole("checkbox", {
        name: /Actividad de pruebas/,
    });
    await expect(avancesDelActa.first()).toBeVisible();
    for (const avance of await avancesDelActa.all()) await avance.check();
    await d.getByRole("checkbox", { name: "Enviar a revisión" }).check();
    await guardarDialogo(page);
    await expect(
        page.getByRole("button", { name: "ACTA-FRONTEND", exact: true }),
    ).toBeVisible();
    const fila = page.getByRole("row").filter({
        has: page.getByRole("button", {
            name: "ACTA-FRONTEND",
            exact: true,
        }),
    });
    await fila.getByLabel("Más acciones").click();
    await page.getByRole("menuitem", { name: "Resolver acta" }).click();
    await page
        .getByRole("dialog")
        .getByLabel(/^Decisión/)
        .selectOption("APROBADA");
    await guardarDialogo(page);
    await expect(fila.getByText("Aprobada", { exact: true })).toBeVisible();
    const actas = await (
        await consultar(
            page,
            `/api/seguimiento/obras/${escenario.idObra}/actas`,
        )
    ).json();
    const acta = actas.find(
        (a: { numeroActa: string }) => a.numeroActa === "ACTA-FRONTEND",
    );
    const detalle = await (
        await consultar(page, `/api/seguimiento/actas/${acta.idActa}`)
    ).json();
    expect(detalle.avancesConsolidados.length).toBeGreaterThan(0);
    expect(detalle.evidenciasConsolidadas.length).toBeGreaterThan(0);
    await fila
        .getByRole("button", { name: "ACTA-FRONTEND", exact: true })
        .click();
    const actaAbierta = page.getByRole("dialog");
    await actaAbierta
        .getByRole("button", { name: "Ver imagen", exact: true })
        .first()
        .click();
    await expect(
        actaAbierta
            .getByRole("region", { name: "Vista previa de evidencia" })
            .getByRole("img"),
    ).toBeVisible();
});
test("perfiles y módulos se adaptan en escritorio y teléfono", async ({
    browser,
}) => {
    test.setTimeout(120_000);
    const contexto = await browser.newContext({
        baseURL: process.env.CET_E2E_URL,
        viewport: { width: 1440, height: 1000 },
    });
    const page = await contexto.newPage();
    const errores: string[] = [];
    page.on("pageerror", (e) => errores.push(e.message));
    await ingresar(page);
    for (const [ruta, titulo] of [
        ["/personal", "Personal"],
        ["/epp", "Equipo de protección"],
        ["/herramientas", "Herramientas"],
        ["/reportes", "Reportes"],
        ["/accesos", "Usuarios y accesos"],
        ["/auditoria", "Auditoría"],
    ]) {
        await page.goto(ruta);
        await expect(
            page.getByRole("heading", { name: titulo, exact: true }),
        ).toBeVisible();
        await expect(page.getByRole("alert")).toHaveCount(0);
    }
    await page.setViewportSize({ width: 360, height: 800 });
    await page.goto("/obras");
    await expect(
        page.getByRole("heading", { name: "Obras", exact: true }),
    ).toBeVisible();
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= window.innerWidth + 1,
        ),
    ).toBe(true);
    await page.screenshot({
        path: "../artifacts/Frontend/obras-movil.png",
        fullPage: true,
    });
    const a = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
        .analyze();
    expect(a.violations).toEqual([]);
    expect(errores).toEqual([]);
    await contexto.close();
    for (const perfil of [
        "residente",
        "seguridad",
        "administrativo",
        "operaciones",
    ]) {
        const c = await browser.newContext({
            baseURL: process.env.CET_E2E_URL,
        });
        const p = await c.newPage();
        await ingresar(p, perfil);
        await expect(
            p.getByRole("heading", { name: "Resumen general" }),
        ).toBeVisible();
        if (perfil !== "operaciones")
            await expect(
                p.getByRole("link", {
                    name: "Usuarios y accesos",
                    exact: true,
                }),
            ).toHaveCount(0);
        await c.close();
    }
});

test("menú y filtros mantienen su estructura en pantallas estrechas y al girar", async ({
    browser,
}) => {
    test.setTimeout(120_000);
    const contexto = await browser.newContext({
        baseURL: process.env.CET_E2E_URL,
        viewport: { width: 320, height: 568 },
        reducedMotion: "reduce",
    });
    const page = await contexto.newPage();
    try {
        await ingresar(page);
        for (const ruta of [
            "/asistencia",
            "/reportes",
            "/auditoria",
            "/epp?vista=entregas",
        ]) {
            await page.goto(ruta);
            await expect(page.locator("main h1")).toBeVisible();
            const barra = page.locator(".barra-filtros");
            const grupos = await barra.evaluate((e) =>
                [...e.children].map((c) => {
                    const r = c.getBoundingClientRect();
                    return { x: r.x, ancho: r.width };
                }),
            );
            expect(grupos.length).toBeGreaterThan(1);
            for (const g of grupos) {
                expect(Math.abs(g.x - grupos[0].x)).toBeLessThan(1);
                expect(Math.abs(g.ancho - grupos[0].ancho)).toBeLessThan(1);
            }
            expect(
                await page.evaluate(
                    () =>
                        document.documentElement.scrollWidth <= innerWidth + 1,
                ),
            ).toBe(true);
        }
        await page
            .getByRole("button", { name: "Alternar menú principal" })
            .click();
        const lateral = page.getByRole("complementary", {
            name: "Navegación principal",
        });
        await expect(
            lateral.getByText("Control operativo", { exact: true }),
        ).toHaveCount(0);
        const salir = lateral.getByRole("button", {
            name: "Cerrar sesión",
            exact: true,
        });
        for (const [width, height] of [
            [320, 568],
            [360, 480],
            [667, 320],
            [390, 420],
        ]) {
            await page.setViewportSize({ width, height });
            await expect(salir).toBeVisible();
            const geometria = await salir.evaluate((e) => {
                const r = e.getBoundingClientRect();
                return {
                    x: r.x,
                    hasta: r.bottom,
                    ancho: r.width,
                    alto: r.height,
                    pulsable:
                        document
                            .elementFromPoint(
                                r.x + r.width / 2,
                                r.y + r.height / 2,
                            )
                            ?.closest("button") === e,
                };
            });
            expect(geometria.hasta).toBeLessThanOrEqual(height);
            expect(geometria.ancho).toBeGreaterThanOrEqual(44);
            expect(geometria.alto).toBeGreaterThanOrEqual(44);
            expect(geometria.pulsable).toBe(true);
            await lateral.locator("nav").evaluate((e) => {
                e.scrollTop = e.scrollHeight;
            });
            expect(
                (await salir.boundingBox())!.y +
                    (await salir.boundingBox())!.height,
            ).toBeLessThanOrEqual(height);
        }
        expect(await page.evaluate(() => document.body.style.overflow)).toBe(
            "hidden",
        );
        await page.keyboard.press("Escape");
        await expect(
            page.getByRole("button", { name: "Alternar menú principal" }),
        ).toHaveAttribute("aria-expanded", "false");
        expect(
            await page.evaluate(() => document.body.style.overflow),
        ).not.toBe("hidden");
        await page
            .getByRole("button", { name: "Alternar menú principal" })
            .click();
        await salir.click();
        await expect(page).toHaveURL(/\/ingresar/);
        await expect(page.getByLabel("Usuario", { exact: true })).toBeVisible();
        expect((await page.request.get("/api/sesion/actual")).status()).toBe(
            401,
        );
    } finally {
        await contexto.close();
    }
});

for (const [dispositivo, ancho] of [
    ["escritorio", 1440],
    ["teléfono", 390],
] as const) {
    test(`encabezados, búsqueda y Skeleton con respuestas demoradas en ${dispositivo}`, async ({
        browser,
    }) => {
        test.setTimeout(90_000);
        const contexto = await browser.newContext({
            baseURL: process.env.CET_E2E_URL,
            viewport: { width: ancho, height: 900 },
            reducedMotion: "reduce",
        });
        const page = await contexto.newPage();
        let liberarTrabajadores = () => {};
        let liberarRoles = () => {};
        try {
            await ingresar(page);
            await page.goto("/reportes");
            await expect(
                page.getByRole("heading", { name: "Reportes", exact: true }),
            ).toBeVisible();
            await expect(
                page.locator(
                    ".encabezado-pagina .ceja,.encabezado-pagina .descripcion-pagina",
                ),
            ).toHaveCount(0);
            let resolverTrabajadores!: () => void;
            const esperaTrabajadores = new Promise<void>((resolver) => {
                resolverTrabajadores = resolver;
            });
            liberarTrabajadores = resolverTrabajadores;
            await page.route(
                /\/api\/personal\/trabajadores\?/,
                async (ruta) => {
                    await esperaTrabajadores;
                    await ruta.continue();
                },
            );
            await page.goto("/personal");
            const carga = page.locator(".panel-registros .esqueleto-carga");
            await expect(carga).toBeVisible();
            await expect(carga).toHaveAttribute("aria-busy", "true");
            await expect(
                page.locator(".panel-registros .estado-vacio"),
            ).toHaveCount(0);
            expect(
                await carga
                    .locator(".esqueleto-estructura")
                    .evaluate((e) => getComputedStyle(e).animationName),
            ).toBe("none");
            await page.screenshot({
                path: `../artifacts/Frontend/skeleton-${ancho}.png`,
                fullPage: true,
            });
            liberarTrabajadores();
            await expect(carga).toHaveCount(0);
            await expect(page.locator("tbody tr").first()).toBeVisible();
            await page.unroute(/\/api\/personal\/trabajadores\?/);

            let resolverRoles!: () => void;
            const esperaRoles = new Promise<void>((resolver) => {
                resolverRoles = resolver;
            });
            liberarRoles = resolverRoles;
            await page.route(/\/api\/roles\?/, async (ruta) => {
                await esperaRoles;
                await ruta.continue();
            });
            await page.goto("/accesos");
            await page
                .getByRole("button", { name: "Crear usuario", exact: true })
                .first()
                .click();
            const dialogo = page.getByRole("dialog");
            await expect(
                dialogo.locator(".carga-opciones").last(),
            ).toBeVisible();
            liberarRoles();
            const buscar = dialogo.getByRole("textbox", {
                name: "Buscar Roles iniciales",
                exact: true,
            });
            await expect(
                dialogo.getByRole("checkbox", { name: /INGENIERO_RESIDENTE/ }),
            ).toBeVisible();
            const espacios = await buscar.evaluate((e) => {
                const icono = e
                    .parentElement!.querySelector("svg")!
                    .getBoundingClientRect();
                const campo = e.getBoundingClientRect();
                return {
                    iconoHasta: icono.right,
                    campoDesde: campo.left,
                    alto: campo.height,
                };
            });
            expect(espacios.campoDesde).toBeGreaterThan(espacios.iconoHasta);
            expect(espacios.alto).toBeGreaterThanOrEqual(44);
            await buscar.fill("residente");
            await expect(
                dialogo.locator(".opciones-multiples .opcion-check"),
            ).toHaveCount(1);
            await dialogo
                .getByRole("checkbox", { name: /INGENIERO_RESIDENTE/ })
                .check();
            await buscar.fill("");
            await expect(
                dialogo.getByRole("checkbox", { name: /INGENIERO_RESIDENTE/ }),
            ).toBeChecked();
            await page.screenshot({
                path: `../artifacts/Frontend/busqueda-${ancho}.png`,
                fullPage: true,
            });
            await dialogo
                .getByRole("button", { name: "Cancelar", exact: true })
                .click();

            await page.goto("/ajustes");
            await page.getByRole("radio", { name: /^Oscuro/ }).check();
            await expect(page.locator("html")).toHaveAttribute(
                "data-tema",
                "oscuro",
            );
            const indicadores = await page
                .locator(".indicador-apariencia")
                .evaluateAll((elementos) =>
                    elementos.map((e) => ({
                        ancho: e.getBoundingClientRect().width,
                        alto: e.getBoundingClientRect().height,
                    })),
                );
            expect(indicadores).toEqual(Array(3).fill({ ancho: 20, alto: 20 }));
            expect(
                await page
                    .getByRole("radio", { name: /^Oscuro/ })
                    .evaluate((e) => getComputedStyle(e).opacity),
            ).toBe("0");
            await page.screenshot({
                path: `../artifacts/Frontend/apariencia-${ancho}.png`,
                fullPage: true,
            });
            expect(
                await page.evaluate(
                    () =>
                        document.documentElement.scrollWidth <= innerWidth + 1,
                ),
            ).toBe(true);
            const accesibilidad = await new AxeBuilder({ page })
                .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
                .analyze();
            expect(accesibilidad.violations).toEqual([]);
            await page.route(/\/api\/personal\/cargos\?/, (ruta) =>
                ruta.fulfill({
                    status: 503,
                    contentType: "application/problem+json",
                    body: JSON.stringify({
                        title: "Consulta no disponible",
                        detail: "Servicio temporalmente no disponible.",
                    }),
                }),
            );
            await page.goto("/personal?vista=cargos");
            await expect(
                page.getByRole("alert").filter({
                    hasText: "Servicio temporalmente no disponible.",
                }),
            ).toBeVisible();
            await expect(
                page.locator(".panel-registros .esqueleto-carga"),
            ).toHaveCount(0);
        } finally {
            liberarTrabajadores();
            liberarRoles();
            await contexto.close();
        }
    });
}
