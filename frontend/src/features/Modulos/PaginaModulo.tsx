import { TablaDatos } from "../../components/TablaDatos";
import Detalle from "../../components/DetalleRegistro";
import { lazy, Suspense, useCallback, useEffect, useId, useState } from "react";
import { useParams, useSearchParams, Link } from "react-router-dom";
import {
    Search,
    Plus,
    RefreshCw,
    List,
    Map as MapIcon,
    MapPin,
} from "lucide-react";
import {
    modulos,
    rutaConDatos,
    type Accion,
    type Fila,
    type Recurso,
} from "./catalogo";
import { useSesion } from "../../app/Sesion";
import { useDatos } from "../../hooks/usoDatos";
import { nombresAreas, nombreCampo } from "../../components/etiquetas";
import { nombrePersona, fecha, etiqueta } from "../../app/formatos";
import {
    Boton,
    Encabezado,
    Cargando,
    Vacio,
    ErrorVisible,
    Paginacion,
} from "../../components/Interfaz";
import { SelectorReferencia } from "../../components/SelectorReferencia";
import { permisos as p } from "../../api/permisos";
import { catalogos } from "../../api/catalogos";
import { MenuAcciones } from "../../components/MenuAcciones";
import { PrepararObra } from "../../components/PrepararObra";
import { api } from "../../api/cliente";

const Formulario = lazy(() =>
    import("../../components/FormularioOperacion").then((m) => ({
        default: m.FormularioOperacion,
    })),
);
const MapaObras = lazy(() => import("../Obras/MapaObras"));
const SalidaAsistida = lazy(() => import("../Asistencia/SalidaAsistida"));
function estadosFiltro(recurso: Recurso): readonly string[] {
    if (recurso.clave === "obras") return catalogos.EstadosObra;
    if (recurso.clave === "trabajadores") return catalogos.EstadosTrabajador;
    if (recurso.clave === "usuarios") return catalogos.EstadosUsuario;
    if (recurso.clave === "correcciones")
        return catalogos.EstadosCorreccionAsistencia;
    if (recurso.clave === "existencias") return catalogos.EstadosEpp;
    if (recurso.clave === "inventario") return catalogos.EstadosHerramienta;
    if (recurso.clave === "reposiciones") return catalogos.EstadosReposicionEpp;
    if (recurso.clave === "asignaciones-herramientas")
        return catalogos.EstadosAsignacionHerramienta;
    if (recurso.clave === "entregas") return catalogos.EstadosEntregaEpp;
    return [];
}

export default function PaginaModulo({
    nombre: propNombre,
}: {
    nombre?: string;
}) {
    const parametros = useParams();
    const nombre = propNombre || parametros.modulo || "";
    const modulo = modulos[nombre];
    const { puede, perfil } = useSesion();
    const [busqueda, setBusqueda] = useSearchParams();
    const idFiltros = useId();
    const disponibles =
        modulo?.recursos.filter((r) => puede(...r.permisos)) || [];
    const recurso =
        disponibles.find((r) => r.clave === busqueda.get("vista")) ||
        disponibles[0];
    const [filtros, setFiltros] = useState<Fila>({});
    const [buscar, setBuscar] = useState("");
    const [pagina, setPagina] = useState(1);
    const [obra, setObra] = useState<number | undefined>();
    const [detalle, setDetalle] = useState<Fila | null>(null);
    const [formulario, setFormulario] = useState<{
        accion: Accion;
        fila?: Fila;
    } | null>(null);
    const [mapa, setMapa] = useState(false);
    const [salida, setSalida] = useState<Fila | null>(null);
    const [errorAccion, setErrorAccion] = useState<unknown>();
    const [cargandoAccion, setCargandoAccion] = useState(false);
    const [volverDetalle, setVolverDetalle] = useState<Fila | null>(null);
    const registro = busqueda.get("registro");
    const crearSolicitado = busqueda.get("crear");
    const trabajadorSolicitado = busqueda.get("idTrabajador");
    const puedeCrear = !!recurso?.crear && puede(recurso.crear.permiso);
    useEffect(() => {
        if (crearSolicitado === recurso?.clave && recurso.crear && puedeCrear)
            setFormulario({
                accion: recurso.crear,
                fila:
                    Number(trabajadorSolicitado) > 0
                        ? { idTrabajador: Number(trabajadorSolicitado) }
                        : {},
            });
    }, [crearSolicitado, trabajadorSolicitado, recurso?.clave, puedeCrear]);
    useEffect(() => {
        setDetalle(
            registro && Number(registro) > 0 && recurso?.detalle
                ? { [recurso.identidad]: Number(registro) }
                : null,
        );
    }, [registro, recurso?.clave, nombre]);
    useEffect(() => {
        const parametros: Fila = {};
        for (const campo of recurso?.filtros || []) {
            const valor = busqueda.get(campo);
            if (
                valor &&
                campo !== "idObra" &&
                (campo !== "estado" || estadosFiltro(recurso).includes(valor))
            )
                parametros[campo] = valor;
        }
        if (busqueda.has("idObra")) {
            const id = Number(busqueda.get("idObra"));
            setObra(Number.isInteger(id) && id > 0 ? id : undefined);
        }
        setFiltros(parametros);
        setPagina(1);
        setBuscar("");
        setMapa(false);
    }, [nombre, recurso?.clave, busqueda]);
    const contexto = { idObra: obra, ...filtros };
    const ruta = recurso ? rutaConDatos(recurso.ruta, contexto) : null;
    const filtroParams = {
        ...filtros,
        ...(recurso?.filtros?.includes("idObra") ? { idObra: obra } : {}),
        pagina,
        tamanoPagina: 25,
    };
    const consulta = useDatos<Fila[]>(ruta, filtroParams);
    const mapaConsulta = useDatos<Fila[]>(
        mapa && nombre === "obras" ? "/api/obras" : null,
        { estado: filtros.estado, pagina: 1, tamanoPagina: 100 },
    );
    const cerrarFormulario = useCallback(() => {
        setFormulario(null);
        if (busqueda.has("crear"))
            setBusqueda(
                (actual) => {
                    const siguiente = new URLSearchParams(actual);
                    siguiente.delete("crear");
                    siguiente.delete("idTrabajador");
                    return siguiente;
                },
                { replace: true },
            );
    }, [busqueda, setBusqueda]);
    const cerrarDetalle = useCallback(() => {
        setDetalle(null);
        if (busqueda.has("registro"))
            setBusqueda(
                (actual) => {
                    const siguiente = new URLSearchParams(actual);
                    siguiente.delete("registro");
                    return siguiente;
                },
                { replace: true },
            );
    }, [busqueda, setBusqueda]);
    const abrirAccion = async (accion: Accion, fila: Fila) => {
        setErrorAccion(undefined);
        setCargandoAccion(true);
        try {
            const rutaDetalle = recurso?.detalle
                ? rutaConDatos(recurso.detalle, fila)
                : null;
            const completa =
                accion.requiereDetalle && rutaDetalle
                    ? await api.leer<Fila>(rutaDetalle)
                    : fila;
            setVolverDetalle(detalle);
            setDetalle(null);
            setFormulario({ accion, fila: completa });
        } catch (e) {
            setErrorAccion(e);
        } finally {
            setCargandoAccion(false);
        }
    };
    const cerrarSalida = useCallback(() => setSalida(null), []);
    if (!modulo)
        return (
            <Vacio
                titulo="Página no encontrada"
                detalle="El módulo solicitado no existe."
                accion={
                    <Link className="boton boton-primario" to="/">
                        Volver al resumen
                    </Link>
                }
            />
        );
    if (!recurso)
        return (
            <Vacio
                titulo="Acceso restringido"
                detalle="Tu perfil no tiene permisos para consultar este módulo."
            />
        );
    const filas = (consulta.datos || []).filter(
        (f) =>
            !buscar ||
            Object.values(f).some((v) =>
                String(v ?? "")
                    .toLocaleLowerCase()
                    .includes(buscar.toLocaleLowerCase()),
            ) ||
            nombrePersona(f)
                .toLocaleLowerCase()
                .includes(buscar.toLocaleLowerCase()),
    );
    const necesitaObra =
        recurso.requiereObra || recurso.filtros?.includes("idObra");
    const gerencia = perfil?.roles.some(
        (r) => r === "GERENTE_GENERAL" || r === "GERENTE_OPERACIONES",
    );
    const crear =
        recurso.crear && puede(recurso.crear.permiso)
            ? recurso.crear
            : undefined;
    const accionesFila = (fila: Fila) => {
        const acciones =
            recurso.acciones?.filter(
                (a) => puede(a.permiso) && (!a.visible || a.visible(fila)),
            ) || [];
        const cerrarAsistencia =
            recurso.clave === "registros" &&
            fila.estado === "ABIERTA" &&
            puede(p.registrarAsistencia);
        const opciones: { titulo: string; ejecutar: () => void }[] =
            acciones.map((a) => ({
                titulo: a.titulo,
                ejecutar: () => void abrirAccion(a, fila),
            }));
        if (cerrarAsistencia)
            opciones.unshift({
                titulo: "Registrar salida",
                ejecutar: () => setSalida(fila),
            });
        return opciones.length ? <MenuAcciones opciones={opciones} /> : null;
    };
    return (
        <>
            <Encabezado
                titulo={modulo.titulo}
                acciones={
                    <>
                        {obra && puede(p.operarSincronizacion) && (
                            <PrepararObra idObra={obra} />
                        )}
                        {nombre === "asistencia" &&
                            puede(p.registrarAsistencia) && (
                                <Link
                                    className="boton boton-secundario"
                                    to="/asistencia/marcar"
                                >
                                    <MapPin size={17} />
                                    Marcación asistida
                                </Link>
                            )}
                        {crear && (
                            <Boton
                                onClick={() => setFormulario({ accion: crear })}
                            >
                                <Plus size={18} />
                                {crear.titulo}
                            </Boton>
                        )}
                    </>
                }
            />
            <nav className="pestanas" aria-label={`Vistas de ${modulo.titulo}`}>
                {disponibles.map((r) => (
                    <button
                        key={r.clave}
                        className={r.clave === recurso.clave ? "activa" : ""}
                        onClick={() =>
                            setBusqueda((actual) => {
                                const siguiente = new URLSearchParams();
                                siguiente.set("vista", r.clave);
                                if (actual.has("idObra"))
                                    siguiente.set(
                                        "idObra",
                                        actual.get("idObra")!,
                                    );
                                else if (obra)
                                    siguiente.set("idObra", String(obra));
                                return siguiente;
                            })
                        }
                    >
                        {r.titulo}
                    </button>
                ))}
            </nav>
            <section className="panel-registros">
                {errorAccion !== undefined && (
                    <ErrorVisible error={errorAccion} />
                )}
                {cargandoAccion && <Cargando variante="detalle" />}
                <div className="barra-filtros">
                    <div className="filtro filtro-busqueda">
                        <label
                            className="etiqueta-filtro"
                            htmlFor={`${idFiltros}-buscar`}
                        >
                            Buscar
                        </label>
                        <div className="buscar-tabla">
                            <Search size={18} />
                            <input
                                id={`${idFiltros}-buscar`}
                                placeholder="Buscar en esta página…"
                                aria-label="Buscar en los registros cargados"
                                value={buscar}
                                onChange={(e) => setBuscar(e.target.value)}
                            />
                        </div>
                    </div>
                    {necesitaObra && (
                        <div className="filtro-referencia">
                            <label
                                className="etiqueta-filtro"
                                htmlFor={`${idFiltros}-obra`}
                            >
                                Obra
                            </label>
                            <SelectorReferencia
                                idControl={`${idFiltros}-obra`}
                                campo="idObra"
                                valor={obra}
                                cambiar={(v) => {
                                    setObra(Number(v) || undefined);
                                    setPagina(1);
                                }}
                                etiquetaVisible="Filtrar por obra"
                            />
                        </div>
                    )}
                    {recurso.filtros
                        ?.filter((f) => f !== "idObra")
                        .map((f) => (
                            <div className="filtro" key={f}>
                                {(f.startsWith("id") ||
                                    f === "estado" ||
                                    f === "entidad") && (
                                    <label
                                        className="etiqueta-filtro"
                                        htmlFor={`${idFiltros}-${f}`}
                                    >
                                        {nombreCampo(f)}
                                    </label>
                                )}
                                {f.startsWith("id") ? (
                                    <SelectorReferencia
                                        idControl={`${idFiltros}-${f}`}
                                        campo={f}
                                        historial
                                        valor={filtros[f]}
                                        cambiar={(v) => {
                                            setFiltros((a) => ({
                                                ...a,
                                                [f]: v,
                                            }));
                                            setPagina(1);
                                        }}
                                        idObra={obra}
                                        etiquetaVisible={nombreCampo(f)}
                                    />
                                ) : f === "entidad" ? (
                                    <select
                                        id={`${idFiltros}-${f}`}
                                        aria-label="Filtrar por área"
                                        value={String(filtros[f] || "")}
                                        onChange={(e) => {
                                            setFiltros((a) => ({
                                                ...a,
                                                [f]: e.target.value,
                                            }));
                                            setPagina(1);
                                        }}
                                    >
                                        <option value="">
                                            Todas las áreas
                                        </option>
                                        {Object.entries(nombresAreas)
                                            .filter(
                                                ([valor]) =>
                                                    ![
                                                        "registros_asistencia",
                                                        "incidencias_obra",
                                                    ].includes(valor),
                                            )
                                            .map(([valor, nombre]) => (
                                                <option
                                                    key={valor}
                                                    value={valor}
                                                >
                                                    {nombre}
                                                </option>
                                            ))}
                                    </select>
                                ) : f === "estado" ? (
                                    <select
                                        id={`${idFiltros}-${f}`}
                                        aria-label="Filtrar por estado"
                                        value={String(filtros[f] || "")}
                                        onChange={(e) => {
                                            setFiltros((a) => ({
                                                ...a,
                                                [f]: e.target.value,
                                            }));
                                            setPagina(1);
                                        }}
                                    >
                                        <option value="">
                                            Todos los estados
                                        </option>
                                        {estadosFiltro(recurso).map((v) => (
                                            <option key={v} value={v}>
                                                {etiqueta(v)}
                                            </option>
                                        ))}
                                    </select>
                                ) : (
                                    <label className="filtro-fecha">
                                        <span>{nombreCampo(f)}</span>
                                        <input
                                            type={
                                                f.includes("Utc")
                                                    ? "datetime-local"
                                                    : f.startsWith("fecha")
                                                      ? "date"
                                                      : "text"
                                            }
                                            value={String(filtros[f] || "")}
                                            onChange={(e) => {
                                                setFiltros((a) => ({
                                                    ...a,
                                                    [f]: e.target.value,
                                                }));
                                                setPagina(1);
                                            }}
                                        />
                                    </label>
                                )}
                            </div>
                        ))}
                    <div className="filtros-final">
                        {nombre === "obras" && gerencia && (
                            <div className="selector-vista">
                                <button
                                    className={!mapa ? "seleccionada" : ""}
                                    aria-label="Vista de lista"
                                    onClick={() => setMapa(false)}
                                >
                                    <List size={18} />
                                </button>
                                <button
                                    className={mapa ? "seleccionada" : ""}
                                    aria-label="Mapa gerencial"
                                    onClick={() => setMapa(true)}
                                >
                                    <MapIcon size={18} />
                                </button>
                            </div>
                        )}
                        <button
                            className="boton boton-secundario"
                            aria-label="Actualizar registros"
                            title="Actualizar"
                            onClick={() => {
                                void consulta.refetch();
                                if (mapa) void mapaConsulta.refetch();
                            }}
                        >
                            <RefreshCw
                                size={17}
                                className={consulta.isFetching ? "girar" : ""}
                            />
                            Actualizar
                        </button>
                    </div>
                </div>
                {consulta.cache && (
                    <p className="aviso-cache">
                        Consulta guardada en este dispositivo ·{" "}
                        {fecha(consulta.guardadoEn, true)}
                    </p>
                )}
                {mapa ? (
                    mapaConsulta.isLoading ? (
                        <Cargando variante="mapa" />
                    ) : mapaConsulta.error ? (
                        <ErrorVisible error={mapaConsulta.error} />
                    ) : (
                        <Suspense fallback={<Cargando variante="mapa" />}>
                            <MapaObras
                                obras={mapaConsulta.datos || []}
                                seleccionar={setDetalle}
                            />
                        </Suspense>
                    )
                ) : !ruta ? (
                    <Vacio
                        titulo="Selecciona el contexto de consulta"
                        detalle="Elige una obra o el registro solicitado para consultar sus datos."
                    />
                ) : consulta.isLoading ? (
                    <Cargando variante="tabla" />
                ) : consulta.error ? (
                    <ErrorVisible error={consulta.error} />
                ) : !filas.length ? (
                    <Vacio
                        titulo={
                            buscar
                                ? "No se encontraron coincidencias"
                                : "Todavía no hay registros"
                        }
                        detalle={
                            buscar
                                ? "Prueba otra búsqueda en esta página."
                                : recurso.descripcion
                        }
                        accion={
                            crear && (
                                <Boton
                                    variante="secundario"
                                    onClick={() =>
                                        setFormulario({ accion: crear })
                                    }
                                >
                                    <Plus size={16} />
                                    {crear.titulo}
                                </Boton>
                            )
                        }
                    />
                ) : (
                    <>
                        <TablaDatos
                            filas={filas}
                            columnas={recurso.columnas}
                            identidad={recurso.identidad}
                            ver={setDetalle}
                            acciones={accionesFila}
                        />
                        <Paginacion
                            pagina={pagina}
                            siguiente={(consulta.datos?.length || 0) === 25}
                            cambiar={setPagina}
                            cantidad={filas.length}
                        />
                    </>
                )}
            </section>
            {detalle && (
                <Detalle
                    fila={detalle}
                    recurso={recurso}
                    cerrar={cerrarDetalle}
                    ejecutar={(a, fila) => void abrirAccion(a, fila)}
                />
            )}
            <Suspense fallback={<Cargando variante="formulario" />}>
                {formulario && (
                    <Formulario
                        accion={formulario.accion}
                        fila={formulario.fila}
                        idObra={obra}
                        cerrar={cerrarFormulario}
                        alGuardar={(guardado) => {
                            void consulta.refetch();
                            if (guardado?.[recurso.identidad])
                                setDetalle(guardado);
                            else if (volverDetalle) setDetalle(volverDetalle);
                            setVolverDetalle(null);
                        }}
                    />
                )}{" "}
                {salida && (
                    <SalidaAsistida fila={salida} cerrar={cerrarSalida} />
                )}
            </Suspense>
        </>
    );
}
