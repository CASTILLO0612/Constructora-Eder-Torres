import http from "node:http";
import https from "node:https";
import { readFile, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { cabecerasSeguridad } from "./CabecerasSeguridad.mjs";

const raiz = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../dist",
);
const destino = new URL(process.env.API_DESTINO || "http://localhost:5180");
const puerto = Number(process.env.PUERTO_WEB || "4173");
const esperaApiMs = Number(process.env.PROXY_ESPERA_API_MS || "30000");
if (
    !["http:", "https:"].includes(destino.protocol) ||
    destino.username ||
    destino.password ||
    !Number.isFinite(esperaApiMs) ||
    esperaApiMs <= 0
)
    throw new Error(
        "Configura un destino HTTP/HTTPS sin credenciales en la URL y un tiempo de espera positivo.",
    );
const tipos = {
    ".html": "text/html; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".json": "application/json",
    ".webmanifest": "application/manifest+json",
    ".svg": "image/svg+xml",
    ".png": "image/png",
    ".woff2": "font/woff2",
};
// Servidor local de integración y pruebas. Publicar con el proxy/HTTPS del hospedaje elegido.
const servidor = http.createServer(async (peticion, respuesta) => {
    for (const [nombre, valor] of Object.entries(cabecerasSeguridad))
        respuesta.setHeader(nombre, valor);
    // Solo se admite un destino relativo: un request-target absoluto no puede convertir este servidor en proxy abierto.
    if (!peticion.url?.startsWith("/") || peticion.url.startsWith("//")) {
        respuesta.writeHead(400);
        respuesta.end();
        return;
    }
    let url;
    let rutaDecodificada;
    try {
        url = new URL(peticion.url, "http://localhost");
        rutaDecodificada = decodeURIComponent(url.pathname);
    } catch {
        respuesta.writeHead(400);
        respuesta.end();
        return;
    }
    if (
        url.pathname.startsWith("/api/") ||
        url.pathname.startsWith("/salud/")
    ) {
        const transporte = destino.protocol === "https:" ? https : http;
        const proxificada = transporte.request(
            new URL(url.pathname + url.search, destino),
            {
                method: peticion.method,
                headers: Object.fromEntries(
                    Object.entries({
                        ...peticion.headers,
                        host: destino.host,
                    }).filter(
                        ([nombre]) =>
                            ![
                                "forwarded",
                                "x-forwarded-for",
                                "x-forwarded-host",
                                "x-forwarded-proto",
                                "proxy-authorization",
                                "proxy-connection",
                            ].includes(nombre),
                    ),
                ),
            },
            (r) => {
                respuesta.writeHead(r.statusCode, r.headers);
                r.pipe(respuesta);
            },
        );
        proxificada.on("error", () => {
            if (respuesta.headersSent || respuesta.destroyed) return;
            respuesta.writeHead(503, {
                "content-type": "application/problem+json",
                "cache-control": "no-store",
            });
            respuesta.end(
                JSON.stringify({
                    title: "No se pudo completar la operación.",
                    detail: "No se pudo conectar. Revisa tu conexión e inténtalo nuevamente.",
                }),
            );
        });
        proxificada.setTimeout(esperaApiMs, () => proxificada.destroy());
        peticion.on("aborted", () => proxificada.destroy());
        peticion.pipe(proxificada);
        return;
    }
    if (!["GET", "HEAD"].includes(peticion.method)) {
        respuesta.setHeader("allow", "GET, HEAD");
        respuesta.writeHead(405);
        respuesta.end();
        return;
    }
    let archivo = path.resolve(raiz, "." + rutaDecodificada);
    if (!archivo.startsWith(raiz + path.sep) && archivo !== raiz) {
        respuesta.writeHead(403);
        respuesta.end();
        return;
    }
    try {
        if (!(await stat(archivo).catch(() => null))?.isFile()) {
            if (path.extname(url.pathname)) {
                respuesta.writeHead(404);
                respuesta.end();
                return;
            }
            archivo = path.join(raiz, "index.html");
        }
        const contenido = await readFile(archivo);
        respuesta.writeHead(200, {
            "content-type":
                tipos[path.extname(archivo)] || "application/octet-stream",
            "cache-control": archivo.includes(`${path.sep}assets${path.sep}`)
                ? "public,max-age=31536000,immutable"
                : "no-cache",
            "x-content-type-options": "nosniff",
        });
        respuesta.end(peticion.method === "HEAD" ? undefined : contenido);
    } catch {
        respuesta.writeHead(500);
        respuesta.end(
            "No se encontró la compilación del frontend. Ejecuta npm run build.",
        );
    }
});
servidor.listen(puerto, "127.0.0.1", () =>
    process.stdout.write(`Interfaz disponible en http://localhost:${puerto}\n`),
);
for (const evento of ["SIGTERM", "SIGINT"])
    process.on(evento, () => servidor.close(() => process.exit(0)));
