import { useEffect, useMemo, useState } from "react";
import { Search, Plus } from "lucide-react";
import { useDatos } from "../hooks/usoDatos";
import { nombrePersona, etiqueta, fecha, texto } from "../app/formatos";
import { ErrorVisible, Boton, Cargando } from "./Interfaz";
import type { Fila } from "../features/Modulos/catalogo";
import { useCola } from "../hooks/usoCola";

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
    const [pagina, setPagina] = useState(1);
    const [filas, setFilas] = useState<Fila[]>([]);
    const [buscar, setBuscar] = useState("");
    const fuente = fuentePara(campo, idObra);
    const clave =
        fuente?.ruta + JSON.stringify(fuente?.parametros) + String(historial);
    const { operaciones } = useCola(campo === "idAvance");
    const consulta = useDatos<Fila[]>(fuente?.ruta || null, {
        ...fuente?.parametros,
        ...(historial ? { estado: undefined, soloVigentes: undefined } : {}),
        pagina,
        tamanoPagina: 100,
    });
    useEffect(() => {
        setPagina(1);
        setFilas([]);
        setBuscar("");
    }, [clave]);
    useEffect(() => {
        if (consulta.datos)
            setFilas((anteriores) => {
                const combinadas =
                    pagina === 1
                        ? consulta.datos!
                        : [...anteriores, ...consulta.datos!];
                return [
                    ...new Map(
                        combinadas.map((f) => [f[fuente!.id], f]),
                    ).values(),
                ];
            });
    }, [consulta.datos, fuente?.id, pagina]);
    const opciones = useMemo(() => {
        const actuales = filas.map((f) => ({
            valor: String(f[fuente?.id || "id"]),
            titulo: tituloReferencia(f),
            detalle: f.estado ? etiqueta(String(f.estado)) : "",
        }));
        if (campo === "idAvance")
            for (const o of operaciones.filter(
                (o) =>
                    o.coleccion === "avances" &&
                    o.estado !== "CONFIRMADA" &&
                    (!idObra ||
                        o.rutaConsulta ===
                            `/api/seguimiento/obras/${idObra}/avances`),
            ))
                actuales.push({
                    valor: `pendiente:${o.idLocal}`,
                    titulo: `Avance pendiente · ${fecha(o.datos.fechaAvance)}`,
                    detalle: "Se adjuntará al sincronizar el avance",
                });
        return actuales.filter((o) =>
            `${o.titulo} ${o.detalle}`
                .toLocaleLowerCase()
                .includes(buscar.toLocaleLowerCase()),
        );
    }, [filas, fuente?.id, campo, operaciones, idObra, buscar]);
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
            {(multiple || filas.length > 15) && (
                <div className="busqueda-selector">
                    <Search size={17} aria-hidden="true" />
                    <input
                        aria-label={`Buscar ${nombre}`}
                        placeholder="Buscar opciones…"
                        value={buscar}
                        onChange={(e) => setBuscar(e.target.value)}
                    />
                </div>
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
                                : "No hay opciones cargadas."}
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
                </small>
            )}
            {consulta.error && <ErrorVisible error={consulta.error} />}
            {consulta.datos?.length === 100 && (
                <Boton
                    type="button"
                    variante="texto"
                    onClick={() => setPagina((p) => p + 1)}
                >
                    <Plus size={14} />
                    Cargar más opciones
                </Boton>
            )}
        </div>
    );
}
