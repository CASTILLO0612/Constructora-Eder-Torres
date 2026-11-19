import { useEffect, useMemo, useRef, useState } from "react";
import { Plus } from "lucide-react";
import { useDatos } from "../hooks/usoDatos";
import { nombrePersona, etiqueta, fecha, texto } from "../app/formatos";
import { ErrorVisible, Boton, Cargando } from "./Interfaz";
import type { Fila } from "../features/Modulos/catalogo";
import { useCola } from "../hooks/usoCola";
import { useBusqueda, coincideBusqueda } from "../hooks/usoBusqueda";
import { Busqueda } from "./Busqueda";

type Fuente = { ruta: string; id: string; parametros?: Fila };
export function fuentePara(campo: string, idObra?: number): Fuente | null {
    const comunes: Record<string, Fuente> = {
        idObra: { ruta: "/api/obras", id: "idObra" },
        idTrabajador: {
            ruta: "/api/personal/trabajadores",
            id: "idTrabajador",
        },
        idCargo: { ruta: "/api/personal/cargos", id: "idCargo" },
        idEspecialidad: {
            ruta: "/api/personal/especialidades",
            id: "idEspecialidad",
        },
        idCuadrilla: { ruta: "/api/personal/cuadrillas", id: "idCuadrilla" },
        idTipoEpp: { ruta: "/api/epp/tipos", id: "idTipoEpp" },
        idEpp: {
            ruta: "/api/epp/existencias",
            id: "idEpp",
            parametros: { estado: "DISPONIBLE" },
        },
        idEppSustituto: {
            ruta: "/api/epp/existencias",
            id: "idEpp",
            parametros: { estado: "DISPONIBLE" },
        },
        idCategoria: {
            ruta: "/api/herramientas/categorias",
            id: "idCategoria",
        },
        idTipoHerramienta: {
            ruta: "/api/herramientas/tipos",
            id: "idTipoHerramienta",
        },
        idHerramienta: {
            ruta: "/api/herramientas",
            id: "idHerramienta",
            parametros: { estado: "DISPONIBLE" },
        },
        idRol: { ruta: "/api/roles", id: "idRol" },
        idsRolesIniciales: { ruta: "/api/roles", id: "idRol" },
        codigosPermiso: { ruta: "/api/permisos", id: "codigo" },
        idUsuario: { ruta: "/api/usuarios", id: "idUsuario" },
        idAsistencia: {
            ruta: "/api/asistencias",
            id: "idAsistencia",
            parametros: { idObra },
        },
        idEntregaOrigen: {
            ruta: "/api/epp/entregas",
            id: "idEntregaEpp",
            parametros: { idObra, estado: "ACTIVA" },
        },
        idEntregaEpp: {
            ruta: "/api/epp/entregas",
            id: "idEntregaEpp",
            parametros: { idObra },
        },
        idAsignacionHerramienta: {
            ruta: "/api/herramientas/asignaciones",
            id: "idAsignacionHerramienta",
            parametros: { idObra, estado: "ACTIVA" },
        },
    };
    if (comunes[campo]) return comunes[campo];
    if (!idObra) return null;
    if (
        [
            "idAsignacion",
            "idAsignacionTrabajadorObra",
            "idAsignacionResponsable",
        ].includes(campo)
    )
        return {
            ruta: `/api/personal/obras/${idObra}/asignaciones`,
            id: "idAsignacion",
            parametros: { soloVigentes: true },
        };
    if (campo === "idCuadrillaObra")
        return {
            ruta: `/api/personal/obras/${idObra}/cuadrillas`,
            id: "idCuadrillaObra",
        };
    if (["idActividad", "idsActividadesAfectadas"].includes(campo))
        return {
            ruta: `/api/seguimiento/obras/${idObra}/actividades`,
            id: "idActividad",
        };
    if (["idAvance", "idsAvances"].includes(campo))
        return {
            ruta: `/api/seguimiento/obras/${idObra}/avances`,
            id: "idAvance",
        };
    if (
        [
            "idIncidenciaVinculada",
            "idIncidenciaExistente",
            "idsIncidencias",
        ].includes(campo)
    )
        return {
            ruta: `/api/seguimiento/obras/${idObra}/incidencias`,
            id: "idIncidencia",
        };
    return null;
}
export function tituloReferencia(f: Fila) {
    if (f.nombreCompletoTrabajador)
        return `${f.nombreCompletoTrabajador}${f.codigoObra ? ` · ${f.codigoObra}` : ""}${f.nombreTipoEpp ? ` · ${f.nombreTipoEpp}` : ""}${f.codigoInternoEpp ? ` · ${f.codigoInternoEpp}` : ""}${f.codigoInternoHerramienta ? ` · ${f.codigoInternoHerramienta}` : ""}${f.fechaJornada ? ` · ${fecha(f.entradaEn || f.fechaJornada, !!f.entradaEn)}` : ""}`;
    if (f.primerNombre) return nombrePersona(f);
    if (f.nombreActividad)
        return `${f.nombreActividad} · ${fecha(f.fechaAvance)} · ${f.cantidadEjecutada}`;
    return texto(
        f.nombre ||
            f.nombreUsuario ||
            f.nombreCuadrilla ||
            f.codigoInterno ||
            f.codigoHerramienta ||
            f.descripcion ||
            f.tipoEvento ||
            "Registro",
    );
}
export function SelectorReferencia({
    campo,
    valor,
    cambiar,
    idObra,
    multiple = false,
    obligatorio = false,
    etiquetaVisible,
    idControl,
    historial = false,
}: {
    campo: string;
    valor: unknown;
    cambiar: (valor: unknown) => void;
    idObra?: number;
    multiple?: boolean;
    obligatorio?: boolean;
    etiquetaVisible?: string;
    idControl?: string;
    historial?: boolean;
}) {
    const [paginacion, setPaginacion] = useState({ clave: "", pagina: 1 });
    const [cargadas, setCargadas] = useState<{ clave: string; filas: Fila[] }>({
        clave: "",
        filas: [],
    });
    const [buscar, setBuscar] = useState("");
    const fuente = fuentePara(campo, idObra);
    const clave =
        fuente?.ruta + JSON.stringify(fuente?.parametros) + String(historial);
    const busqueda = useBusqueda(buscar, clave);
    const claveConsulta = `${clave}|${busqueda.consulta}`;
    const pagina = paginacion.clave === claveConsulta ? paginacion.pagina : 1;
    const vistas = useRef({ clave, filas: new Map<string, Fila>() });
    if (vistas.current.clave !== clave)
        vistas.current = { clave, filas: new Map() };
    const { operaciones } = useCola(campo === "idAvance");
    const consulta = useDatos<Fila[]>(fuente?.ruta || null, {
        ...fuente?.parametros,
        ...(historial ? { estado: undefined, soloVigentes: undefined } : {}),
        pagina,
        tamanoPagina: 50,
        buscar: busqueda.consulta || undefined,
    });
    const filas =
        cargadas.clave === claveConsulta
            ? cargadas.filas
            : consulta.datos || [];
    useEffect(() => {
        setBuscar("");
    }, [clave]);
    useEffect(() => {
        if (consulta.datos) {
            const seleccion = new Set(
                Array.isArray(valor)
                    ? valor.map(String)
                    : [String(valor ?? "")],
            );
            const recibidos = new Set(
                consulta.datos.map((fila) => String(fila[fuente!.id])),
            );
            for (const id of vistas.current.filas.keys())
                if (!seleccion.has(id) && !recibidos.has(id))
                    vistas.current.filas.delete(id);
            for (const fila of consulta.datos)
                vistas.current.filas.set(String(fila[fuente!.id]), fila);
            setCargadas((anteriores) => {
                const combinadas =
                    pagina === 1
                        ? consulta.datos!
                        : [
                              ...(anteriores.clave === claveConsulta
                                  ? anteriores.filas
                                  : []),
                              ...consulta.datos!,
                          ];
                return {
                    clave: claveConsulta,
                    filas: [
                        ...new Map(
                            combinadas.map((f) => [f[fuente!.id], f]),
                        ).values(),
                    ],
                };
            });
        }
    }, [consulta.datos, fuente?.id, pagina, claveConsulta, valor]);
    const opciones = useMemo(() => {
        const seleccion = Array.isArray(valor)
            ? valor.map(String)
            : [String(valor ?? "")];
        const registros = [...filas];
        for (const id of seleccion) {
            const fila = vistas.current.filas.get(id);
            if (
                fila &&
                !registros.some((f) => String(f[fuente?.id || "id"]) === id)
            )
                registros.push(fila);
        }
        const actuales = registros.map((f) => ({
            valor: String(f[fuente?.id || "id"]),
            titulo: tituloReferencia(f),
            detalle: f.estado ? etiqueta(String(f.estado)) : "",
        }));
        if (campo === "idAvance")
            for (const o of operaciones.filter(
                (o) =>
                    o.coleccion === "avances" &&
                    o.estado !== "CONFIRMADA" &&
                    coincideBusqueda(
                        `Avance pendiente ${fecha(o.datos.fechaAvance)}`,
                        buscar,
                    ) &&
                    (!idObra ||
                        o.rutaConsulta ===
                            `/api/seguimiento/obras/${idObra}/avances`),
            ))
                actuales.push({
                    valor: `pendiente:${o.idLocal}`,
                    titulo: `Avance pendiente · ${fecha(o.datos.fechaAvance)}`,
                    detalle: "Se adjuntará al sincronizar el avance",
                });
        return actuales;
    }, [filas, fuente?.id, campo, operaciones, idObra, buscar, valor]);
    if (!fuente)
        return (
            <p className="ayuda-campo">
                Selecciona una obra para consultar{" "}
                {etiquetaVisible?.toLowerCase() || "las opciones"}.
            </p>
        );
    const seleccionados = Array.isArray(valor) ? valor.map(String) : [];
    const nombre = etiquetaVisible || campo;
    return (
        <div className="selector-referencia">
            {(multiple ||
                filas.length > 15 ||
                buscar ||
                vistas.current.filas.size > 15) && (
                <Busqueda
                    opciones
                    nombre={`Buscar ${nombre.replace(/^Filtrar por\s+/i, "")}`}
                    valor={buscar}
                    cambiar={setBuscar}
                    ayuda={busqueda.ayuda}
                    esperando={busqueda.esperando}
                />
            )}
            {consulta.isLoading && !filas.length ? (
                <Cargando variante="opciones" texto="Cargando opciones…" />
            ) : multiple ? (
                <div className="opciones-multiples">
                    {opciones.map((o) => (
                        <label key={o.valor} className="opcion-check">
                            <input
                                type="checkbox"
                                checked={seleccionados.includes(o.valor)}
                                onChange={(e) =>
                                    cambiar(
                                        e.target.checked
                                            ? [...seleccionados, o.valor]
                                            : seleccionados.filter(
                                                  (x) => x !== o.valor,
                                              ),
                                    )
                                }
                            />
                            <span>
                                {o.titulo}
                                <small>{o.detalle}</small>
                            </span>
                        </label>
                    ))}
                    {!opciones.length && (
                        <p className="ayuda-campo">
                            {consulta.isLoading
                                ? "Cargando opciones…"
                                : buscar
                                  ? "No se encontraron opciones."
                                  : "No hay opciones disponibles."}
                        </p>
                    )}
                </div>
            ) : (
                <select
                    id={idControl}
                    aria-label={nombre}
                    value={String(valor ?? "")}
                    required={obligatorio}
                    onChange={(e) => cambiar(e.target.value)}
                    disabled={consulta.isLoading}
                >
                    <option value="">
                        {consulta.isLoading ? "Cargando…" : "Seleccionar…"}
                    </option>
                    {!!valor &&
                        !opciones.some((o) => o.valor === String(valor)) && (
                            <option value={String(valor)}>
                                Selección actual
                            </option>
                        )}
                    {opciones.map((o) => (
                        <option key={o.valor} value={o.valor}>
                            {o.titulo}
                            {o.detalle ? ` · ${o.detalle}` : ""}
                        </option>
                    ))}
                </select>
            )}
            {consulta.cache && (
                <small className="ayuda-campo">
                    Opciones guardadas en este dispositivo.
                    {!!busqueda.consulta &&
                        " Búsqueda limitada a las opciones guardadas."}
                </small>
            )}
            {consulta.error && <ErrorVisible error={consulta.error} />}
            {!multiple &&
                !consulta.isLoading &&
                !!busqueda.consulta &&
                !filas.length && (
                    <p className="ayuda-campo" role="status">
                        No se encontraron opciones. Prueba otra búsqueda.
                    </p>
                )}
            {consulta.datos?.length === 50 && (
                <Boton
                    type="button"
                    variante="texto"
                    disabled={consulta.isFetching || busqueda.esperando}
                    onClick={() =>
                        setPaginacion({
                            clave: claveConsulta,
                            pagina: pagina + 1,
                        })
                    }
                >
                    <Plus size={14} />
                    Cargar más opciones
                </Boton>
            )}
        </div>
    );
}
