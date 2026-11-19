import { it, expect, vi } from "vitest";
import { api, olvidarToken } from "../../src/api/cliente";
import { modulos } from "../../src/features/Modulos/catalogo";
import operaciones from "../../src/api/operaciones.json";
import esquemas from "../../src/api/esquemas.json";
import { normalizarSolicitud } from "../../src/components/FormularioOperacion";
import documento from "../../contrato-api.json";

it("las columnas y las identidades declaradas existen en las respuestas reales", () => {
    const normalizar = (ruta: string) =>
        ruta.replace(/\{[^}]+\}/g, "{}").replace(/\/$/, "");
    const d = documento as unknown as {
        paths: Record<
            string,
            {
                get?: {
                    responses: Record<
                        string,
                        {
                            content?: Record<
                                string,
                                {
                                    schema: {
                                        $ref?: string;
                                        items?: { $ref?: string };
                                    };
                                }
                            >;
                        }
                    >;
                };
            }
        >;
        components: {
            schemas: Record<string, { properties?: Record<string, unknown> }>;
        };
    };
    for (const modulo of Object.values(modulos))
        for (const r of modulo.recursos) {
            const ruta = Object.keys(d.paths).find(
                (p) => normalizar(p) === normalizar(r.ruta),
            );
            const respuesta = Object.entries(
                d.paths[ruta!]?.get?.responses || {},
            ).find(([c]) => c.startsWith("2"))?.[1];
            const ref =
                respuesta?.content?.["application/json"]?.schema.items?.$ref;
            expect(ref, r.ruta).toBeTruthy();
            const propiedades =
                d.components.schemas[ref!.split("/").at(-1)!].properties || {};
            for (const columna of r.columnas)
                if (!["nombreCompleto", "progreso"].includes(columna))
                    expect(
                        columna in propiedades,
                        `${r.clave}.${columna}`,
                    ).toBe(true);
            expect(
                r.identidad in propiedades,
                `${r.clave}.${r.identidad}`,
            ).toBe(true);
        }
});

it("envía cookies y obtiene un token CSRF nuevo cuando cambia la identidad", async () => {
    let secuencia = 0;
    const solicitudes: { url: string; opciones?: RequestInit }[] = [];
    vi.stubGlobal(
        "fetch",
        vi.fn(async (url, opciones) => {
            solicitudes.push({ url: String(url), opciones });
            return new Response(
                JSON.stringify(
                    String(url).includes("antifalsificacion")
                        ? {
                              token: `token-${++secuencia}`,
                              cabecera: "X-CET-CSRF",
                          }
                        : { idUsuario: 2 },
                ),
                {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                },
            );
        }),
    );
    await api.escribir("POST", "/api/sesion/iniciar", {
        nombreUsuario: "persona",
        contrasena: "solo-prueba",
    });
    olvidarToken();
    await api.escribir("POST", "/api/obras", { nombre: "Obra" });
    expect(solicitudes[1].opciones?.credentials).toBe("include");
    expect(solicitudes[3].opciones?.headers).toMatchObject({
        "X-CET-CSRF": "token-2",
    });
    expect(localStorage.getItem("contrasena")).toBeNull();
    vi.unstubAllGlobals();
});
it("renueva CSRF una sola vez cuando el middleware rechaza antes de escribir", async () => {
    let escrituras = 0;
    vi.stubGlobal(
        "fetch",
        vi.fn(
            async (url) =>
                new Response(
                    JSON.stringify(
                        String(url).includes("antifalsificacion")
                            ? { token: "actual", cabecera: "X-CET-CSRF" }
                            : ++escrituras === 1
                              ? { codigo: "antifalsificacion_requerida" }
                              : { ok: true },
                    ),
                    {
                        status:
                            String(url).includes("antifalsificacion") ||
                            escrituras !== 1
                                ? 200
                                : 400,
                        headers: { "Content-Type": "application/json" },
                    },
                ),
        ),
    );
    await expect(api.escribir("POST", "/api/obras", {})).resolves.toEqual({
        ok: true,
    });
    expect(escrituras).toBe(2);
    vi.unstubAllGlobals();
});
it("los formularios envían únicamente propiedades del contrato y convierten la hora local a UTC", () => {
    expect(
        normalizarSolicitud("SolicitudDevolverEpp", {
            devueltoEn: "2026-11-01T08:30",
            condicionDevolucion: "BUENO",
            campoInventado: "x",
        }),
    ).toMatchObject({
        devueltoEn: "2026-11-01T14:30:00.000Z",
        condicionDevolucion: "BUENO",
    });
    expect(
        normalizarSolicitud("SolicitudCargo", {
            nombre: "Cargo",
            descripcion: "",
        }),
    ).toEqual({ nombre: "Cargo", descripcion: null });
});
it("todas las acciones declaradas corresponden a un método y esquema real de la API", () => {
    const normalizar = (ruta: string) =>
        ruta.replace(/\{[^}]+\}/g, "{}").replace(/\/$/, "");
    for (const modulo of Object.values(modulos))
        for (const recurso of modulo.recursos)
            for (const a of [recurso.crear, ...(recurso.acciones || [])].filter(
                Boolean,
            )) {
                const ruta = Object.keys(operaciones).find(
                    (r) => normalizar(r) === normalizar(a!.ruta),
                );
                expect(ruta, `${a!.titulo}: ${a!.ruta}`).toBeTruthy();
                const o = (
                    operaciones as Record<
                        string,
                        Record<string, { solicitud?: string }>
                    >
                )[ruta!][a!.metodo.toLowerCase()];
                expect(o).toBeTruthy();
                expect(o.solicitud).toBe(a!.esquema);
                expect(a!.esquema in esquemas).toBe(true);
            }
});
