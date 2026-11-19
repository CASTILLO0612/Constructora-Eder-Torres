import { readFile, writeFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const raiz = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../..",
);
const origen =
    process.env.API_OPENAPI || path.join(raiz, "frontend/contrato-api.json");
const documento = /^https?:/.test(origen)
    ? await (await fetch(origen)).json()
    : JSON.parse(await readFile(origen, "utf8"));
const esquemas = documento.components.schemas;
const tipo = (esquema, nombre = "") => {
    if (esquema.$ref) return esquema.$ref.split("/").at(-1);
    if (esquema.enum)
        return esquema.enum.map((x) => JSON.stringify(x)).join(" | ");
    if (["int32", "int64", "double", "float"].includes(esquema.format))
        return Array.isArray(esquema.type) && esquema.type.includes("null")
            ? "number | null"
            : "number";
    if (
        esquema.format === "date-time" ||
        (!esquema.type && /En$|EnUtc$|Utc$/.test(nombre))
    )
        return "string | null";
    if (Array.isArray(esquema.type))
        return [
            ...new Set(
                esquema.type.map((t) => tipo({ ...esquema, type: t }, nombre)),
            ),
        ].join(" | ");
    if (esquema.type === "array") return `Array<${tipo(esquema.items || {})}>`;
    if (esquema.type === "object") {
        if (esquema.properties)
            return `{ ${Object.entries(esquema.properties)
                .map(
                    ([k, v]) =>
                        `${k}${esquema.required?.includes(k) ? "" : "?"}: ${tipo(v, k)}`,
                )
                .join("; ")} }`;
        return "Record<string, unknown>";
    }
    return (
        {
            integer: "number",
            number: "number",
            boolean: "boolean",
            string: "string",
            null: "null",
        }[esquema.type] || "unknown"
    );
};
const salida = path.join(raiz, "frontend/src/api");
await mkdir(salida, { recursive: true });
await writeFile(
    path.join(salida, "contratos.ts"),
    "// Generado desde el OpenAPI de la API. Ejecutar npm run contratos para actualizar.\n" +
        Object.entries(esquemas)
            .map(([n, v]) => `export type ${n} = ${tipo(v)};`)
            .join("\n") +
        "\n",
);
await writeFile(
    path.join(salida, "esquemas.json"),
    JSON.stringify(esquemas, null, 2) + "\n",
);
const archivoCatalogos = await readFile(
    path.join(raiz, "src/Domain/Reglas/CatalogosDominio.cs"),
    "utf8",
);
const catalogos = {};
for (const bloque of archivoCatalogos.split("public static class ").slice(1)) {
    const nombre = bloque.match(/^\w+/)[0];
    catalogos[nombre] = [
        ...bloque.matchAll(/public const string (\w+) = "([^"]+)";/g),
    ].map((m) => m[2]);
}
await writeFile(
    path.join(salida, "catalogos.ts"),
    "// Generado desde CatalogosDominio.cs; son códigos de contrato, no IDs.\nexport const catalogos = " +
        JSON.stringify(catalogos, null, 2) +
        " as const;\n",
);
const seguridad = await readFile(
    path.join(raiz, "src/Domain/Seguridad/CatalogoSeguridad.cs"),
    "utf8",
);
const permisos = Object.fromEntries(
    [
        ...seguridad.matchAll(
            /public const string (\w+) = "([^"\n]+\.[^"\n]+)";/g,
        ),
    ].map((m) => [m[1][0].toLowerCase() + m[1].slice(1), m[2]]),
);
await writeFile(
    path.join(salida, "permisos.ts"),
    "// Generado desde CatalogoSeguridad.cs.\nexport const permisos = " +
        JSON.stringify(permisos, null, 2) +
        " as const;\n",
);
await writeFile(
    path.join(salida, "operaciones.json"),
    JSON.stringify(
        Object.fromEntries(
            Object.entries(documento.paths).map(([ruta, metodos]) => [
                ruta,
                Object.fromEntries(
                    Object.entries(metodos)
                        .filter(([m]) =>
                            ["get", "post", "put", "patch", "delete"].includes(
                                m,
                            ),
                        )
                        .map(([m, v]) => [
                            m,
                            {
                                parametros: v.parameters || [],
                                solicitud: v.requestBody?.content?.[
                                    "application/json"
                                ]?.schema?.$ref
                                    ?.split("/")
                                    .at(-1),
                            },
                        ]),
                ),
            ]),
        ),
        null,
        2,
    ) + "\n",
);
console.log(`Contratos generados: ${Object.keys(esquemas).length} esquemas.`);
