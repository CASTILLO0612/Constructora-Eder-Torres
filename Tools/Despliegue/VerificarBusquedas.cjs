const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { chromium } = require(path.resolve("frontend/node_modules/playwright"));

const origen = process.env.CET_ORIGEN;
const accesos = process.env.CET_ACCESOS;
const carpeta = process.env.CET_RESULTADO;
if (!origen || new URL(origen).protocol !== "https:" || !accesos || !carpeta)
    throw new Error(
        "Configure CET_ORIGEN HTTPS, CET_ACCESOS y CET_RESULTADO fuera del repositorio.",
    );
const dist = path.resolve("frontend/dist");
const limites = JSON.parse(
    fs.readFileSync("frontend/contrato-api.json", "utf8"),
)["x-limites-busqueda"];
const informe = {
    archivosComprobados: 0,
    busquedasComprobadas: 0,
    vistasComprobadas: 0,
};
class ErrorVerificacion extends Error {}
const comprobar = (condicion, mensaje) => {
    if (!condicion) throw new ErrorVerificacion(mensaje);
};
const archivos = (dir) =>
    fs
        .readdirSync(dir, { withFileTypes: true })
        .flatMap((entrada) =>
            entrada.isDirectory()
                ? archivos(path.join(dir, entrada.name))
                : [path.join(dir, entrada.name)],
        );

(async () => {
    const navegador = await chromium.launch({
        channel: "msedge",
        headless: true,
    });
    const contexto = await navegador.newContext({
        viewport: { width: 1440, height: 1000 },
        locale: "es-NI",
        timezoneId: "America/Managua",
    });
    try {
        for (const archivo of archivos(dist)) {
            const respuesta = await contexto.request.get(
                new URL(
                    path.relative(dist, archivo).replaceAll(path.sep, "/"),
                    origen + "/",
                ).href,
            );
            comprobar(
                respuesta.ok(),
                "Un recurso publicado no está disponible.",
            );
            const hash = (datos) =>
                crypto.createHash("sha256").update(datos).digest("hex");
            comprobar(
                hash(await respuesta.body()) === hash(fs.readFileSync(archivo)),
                "Un recurso difiere de la compilación validada.",
            );
            informe.archivosComprobados++;
        }
        comprobar(
            (await contexto.request.get(origen + "/salud/lista")).status() ===
                200,
            "La aplicación no está disponible.",
        );
        const cuenta = JSON.parse(
            fs.readFileSync(accesos, "utf8").replace(/^\uFEFF/, ""),
        ).find((c) => c.Rol === "GERENTE_GENERAL");
        comprobar(!!cuenta, "Falta una cuenta autorizada de verificación.");
        const pagina = await contexto.newPage();
        await pagina.goto(origen + "/ingresar");
        await pagina
            .getByLabel("Usuario", { exact: true })
            .fill(cuenta.Usuario);
        await pagina
            .getByLabel("Contraseña", { exact: true })
            .fill(cuenta.Contrasena);
        cuenta.Contrasena = undefined;
        await pagina
            .getByRole("button", { name: "Entrar a la aplicación" })
            .click();
        await pagina.waitForURL((url) => url.pathname !== "/ingresar");
        const obrasRespuesta = await contexto.request.get(
            origen + "/api/obras?tamanoPagina=100",
        );
        comprobar(
            obrasRespuesta.status() === 200,
            "No se pudieron consultar las obras autorizadas.",
        );
        const obras = await obrasRespuesta.json();
        const muestra =
            obras.find((o) => o.codigo?.length >= limites.longitudMinima) ||
            obras.find((o) => o.nombre?.length >= limites.longitudMinima);
        comprobar(
            !!muestra,
            "No hay una obra con la que comprobar la búsqueda.",
        );
        const termino =
            muestra.codigo?.length >= limites.longitudMinima
                ? muestra.codigo
                : muestra.nombre
                      .split(/\s+/)
                      .find((p) => p.length >= limites.longitudMinima);
        await pagina.goto(origen + "/obras");
        await pagina.keyboard.press("Escape");
        const respuestaBusqueda = pagina.waitForResponse((r) => {
            const url = new URL(r.url());
            return (
                url.pathname === "/api/obras" &&
                url.searchParams.get("buscar") === termino &&
                url.searchParams.get("tamanoPagina") === "25"
            );
        });
        const buscar = pagina.getByRole("textbox", {
            name: "Buscar registros",
            exact: true,
        });
        await buscar.fill(termino);
        const encontrada = await respuestaBusqueda;
        comprobar(
            encontrada.status() === 200,
            "La búsqueda publicada no respondió correctamente.",
        );
        const coincidencias = await encontrada.json();
        comprobar(
            coincidencias.some((o) => o.idObra === muestra.idObra) &&
                coincidencias.length <= 25,
            "La búsqueda no conserva sus resultados paginados.",
        );
        await pagina.locator("tbody tr").first().waitFor();
        informe.busquedasComprobadas++;
        for (const tema of ["claro", "oscuro"])
            for (const width of [320, 1440]) {
                await pagina.setViewportSize({ width, height: 900 });
                await pagina.evaluate(
                    (tema) => (document.documentElement.dataset.tema = tema),
                    tema,
                );
                await pagina.keyboard.press("Escape");
                await pagina.waitForTimeout(250);
                comprobar(
                    await buscar.isVisible(),
                    "El buscador no es visible en una de las pantallas.",
                );
                comprobar(
                    await pagina.evaluate(
                        () =>
                            document.documentElement.scrollWidth <=
                            innerWidth + 1,
                    ),
                    "Hay desbordamiento horizontal.",
                );
                await pagina.screenshot({
                    path: path.join(
                        carpeta,
                        `busquedas-publicadas-${tema}-${width}.png`,
                    ),
                    fullPage: true,
                });
                informe.vistasComprobadas++;
            }
        await pagina
            .getByRole("button", {
                name: "Limpiar búsqueda: registros",
                exact: true,
            })
            .click();
        await buscar.fill(termino.slice(0, limites.longitudMinima - 1));
        await pagina
            .getByText(
                `Escribe al menos ${limites.longitudMinima} caracteres para buscar.`,
            )
            .waitFor();
        comprobar(
            (
                await contexto.request.get(
                    origen +
                        "/api/obras?buscar=" +
                        "a".repeat(limites.longitudMinima - 1),
                )
            ).status() === 400,
            "No se está aplicando el límite de búsqueda.",
        );
        const personas = await contexto.request.get(
            origen + "/api/personal/trabajadores?tamanoPagina=100",
        );
        comprobar(
            personas.status() === 200,
            "No se pudo comprobar el catálogo de personal.",
        );
        const persona = (await personas.json()).find(
            (p) => p.cedula?.length >= limites.longitudMinima,
        );
        comprobar(
            !!persona,
            "No hay un trabajador con el que comprobar la búsqueda.",
        );
        const filtradas = await contexto.request.get(
            origen +
                "/api/personal/trabajadores?tamanoPagina=25&buscar=" +
                encodeURIComponent(persona.cedula),
        );
        comprobar(
            filtradas.status() === 200 &&
                (await filtradas.json()).some(
                    (p) => p.idTrabajador === persona.idTrabajador,
                ),
            "No se encontró el trabajador por su cédula.",
        );
        informe.busquedasComprobadas++;
        const auditoria = await contexto.request.get(
            origen +
                "/api/auditorias?buscar=" +
                encodeURIComponent(cuenta.Usuario) +
                "&tamanoPagina=1",
        );
        comprobar(
            auditoria.status() === 200 && (await auditoria.json()).length <= 1,
            "La consulta de auditoría no está paginada.",
        );
        informe.busquedasComprobadas++;
        await pagina.goto(origen + "/ajustes");
        await pagina
            .locator("main")
            .getByRole("button", { name: "Cerrar sesión", exact: true })
            .click();
        await pagina.waitForURL(/\/ingresar/);
        informe.sesionCerrada = true;
        informe.resultado = "Aprobado";
        informe.comprobadoUtc = new Date().toISOString();
        fs.writeFileSync(
            path.join(carpeta, "verificacion-busquedas-publicadas.json"),
            JSON.stringify(informe, null, 2),
        );
        process.stdout.write(JSON.stringify(informe) + "\n");
    } finally {
        await contexto.close();
        await navegador.close();
    }
})().catch((error) => {
    process.stderr.write(
        (error instanceof ErrorVerificacion
            ? error.message
            : "No se completó la verificación del navegador.") + "\n",
    );
    process.exitCode = 1;
});
