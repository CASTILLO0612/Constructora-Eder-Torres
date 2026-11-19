import { lazy, Suspense, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
    ArrowUpRight,
    ArrowRight,
    Building2,
    Clock,
    ClipboardList,
    HardHat,
    MapPin,
    RefreshCw,
    CircleCheck,
    Users,
} from "lucide-react";
import { useSesion } from "../../app/Sesion";
import { useDatos } from "../../hooks/usoDatos";
import { useCola } from "../../hooks/usoCola";
import { permisos as p } from "../../api/permisos";
import {
    Encabezado,
    Estado,
    Vacio,
    Cargando,
    ErrorVisible,
    Boton,
} from "../../components/Interfaz";
import { fecha, etiqueta, texto } from "../../app/formatos";
import { useQueryClient } from "@tanstack/react-query";
import { PanelesAnaliticos, DisponibilidadEpp } from "./PanelesAnaliticos";
import type { Fila } from "../Modulos/catalogo";
const PaginaMarcacion = lazy(() => import("../Asistencia/PaginaMarcacion"));
const MapaObras = lazy(() => import("../Obras/MapaObras"));

export default function PaginaInicio() {
    const { perfil, puede } = useSesion();
    const { pendientes, cargando: cargandoCola } = useCola();
    const consultas = useQueryClient();
    const [estadoObra, setEstadoObra] = useState<string | null>(null);
    const [refrescando, setRefrescando] = useState(false);
    const [mostrarMapa, setMostrarMapa] = useState(false);
    const soloPropia =
        puede(p.consultarAsistenciaPropia) &&
        !puede(p.consultarObras, p.registrarAsistencia);
    const obras = useDatos<Fila[]>(
        puede(p.consultarObras) ? "/api/obras" : null,
        { pagina: 1, tamanoPagina: 100 },
    );
    const lista = useMemo(() => obras.datos || [], [obras.datos]);
    const visibles = useMemo(
        () =>
            estadoObra ? lista.filter((o) => o.estado === estadoObra) : lista,
        [lista, estadoObra],
    );
    const gerencia = perfil?.roles.some(
        (r) => r === "GERENTE_GENERAL" || r === "GERENTE_OPERACIONES",
    );
    if (soloPropia)
        return (
            <Suspense fallback={<Cargando />}>
                <PaginaMarcacion />
            </Suspense>
        );
    const accesos = [
        {
            titulo: "Revisar obras",
            detalle: "Estado y contexto de tus proyectos",
            ruta: "/obras",
            icono: Building2,
            permiso: p.consultarObras,
        },
        {
            titulo: "Registrar asistencia",
            detalle: "Marcación asistida del personal",
            ruta: "/asistencia/marcar",
            icono: Clock,
            permiso: p.registrarAsistencia,
        },
        {
            titulo: "Seguimiento de obra",
            detalle: "Actividades, avances y evidencias",
            ruta: "/seguimiento",
            icono: ClipboardList,
            permiso: p.registrarAvances,
        },
        {
            titulo: "Equipo de protección",
            detalle: "Entregas, existencias y reposiciones",
            ruta: "/epp",
            icono: HardHat,
            permiso: p.gestionarInventarioEpp,
        },
        {
            titulo: "Consolidado de asistencia",
            detalle: "Consulta jornadas y horas registradas",
            ruta: "/reportes",
            icono: Clock,
            permiso: p.consultarConsolidadoAsistencia,
        },
        {
            titulo: "Gestionar personal",
            detalle: "Trabajadores y asignaciones",
            ruta: "/personal",
            icono: Users,
            permiso: p.gestionarTrabajadores,
        },
    ].filter((a) => puede(a.permiso));
    return (
        <>
            <Encabezado
                titulo="Resumen general"
                acciones={
                    <>
                        <span className="fecha-encabezado">
                            {fecha(new Date().toISOString())}
                        </span>
                        <Boton
                            variante="secundario"
                            ocupado={refrescando}
                            onClick={async () => {
                                setRefrescando(true);
                                try {
                                    await consultas.invalidateQueries({
                                        queryKey: ["datos"],
                                    });
                                } finally {
                                    setRefrescando(false);
                                }
                            }}
                        >
                            {!refrescando && <RefreshCw size={17} />}Actualizar
                            panel
                        </Boton>
                    </>
                }
            />
            {obras.isLoading || cargandoCola ? (
                <Cargando variante="tarjetas" texto="Cargando resumen…" />
            ) : (
                <div className="metricas">
                    <button
                        type="button"
                        aria-pressed={estadoObra === null}
                        onClick={() => setEstadoObra(null)}
                    >
                        <span>
                            Obras en la vista
                            <Building2 size={17} />
                        </span>
                        <strong>{obras.datos ? lista.length : "—"}</strong>
                        <small>Dentro de tu alcance autorizado</small>
                    </button>
                    <button
                        type="button"
                        aria-pressed={estadoObra === "ACTIVA"}
                        onClick={() =>
                            setEstadoObra(
                                estadoObra === "ACTIVA" ? null : "ACTIVA",
                            )
                        }
                    >
                        <span>
                            En ejecución
                            <ArrowUpRight size={17} />
                        </span>
                        <strong>
                            {obras.datos
                                ? lista.filter((o) => o.estado === "ACTIVA")
                                      .length
                                : "—"}
                        </strong>
                        <small>Obras con estado activo</small>
                    </button>
                    <button
                        type="button"
                        aria-pressed={estadoObra === "PLANIFICADA"}
                        onClick={() =>
                            setEstadoObra(
                                estadoObra === "PLANIFICADA"
                                    ? null
                                    : "PLANIFICADA",
                            )
                        }
                    >
                        <span>
                            Planificadas
                            <ClipboardList size={17} />
                        </span>
                        <strong>
                            {obras.datos
                                ? lista.filter(
                                      (o) => o.estado === "PLANIFICADA",
                                  ).length
                                : "—"}
                        </strong>
                        <small>Preparación de próximos trabajos</small>
                    </button>
                    <Link to="/pendientes">
                        <span>
                            Capturas pendientes
                            <RefreshCw size={17} />
                        </span>
                        <strong>{pendientes.length}</strong>
                        <small>
                            {pendientes.length
                                ? "Capturas que esperan confirmación"
                                : "Tus capturas están al día"}
                        </small>
                    </Link>
                </div>
            )}
            <PanelesAnaliticos
                obras={lista}
                cargandoObras={obras.isLoading}
                errorObras={obras.error}
                estado={estadoObra}
                cambiarEstado={setEstadoObra}
            />
            <div className="rejilla-inicio">
                <section className="panel panel-obras">
                    <div className="titulo-panel">
                        <div>
                            <p className="ceja">SEGUIMIENTO ACTUAL</p>
                            <h2>
                                {estadoObra
                                    ? `Obras ${etiqueta(estadoObra).toLowerCase()}s`
                                    : "Tus obras"}
                            </h2>
                        </div>
                        {puede(p.consultarObras) && (
                            <Link to="/obras" className="enlace-accion">
                                Ver todas
                                <ArrowUpRight size={16} />
                            </Link>
                        )}
                    </div>
                    {obras.isLoading ? (
                        <Cargando filas={4} />
                    ) : obras.error ? (
                        <ErrorVisible error={obras.error} />
                    ) : !visibles.length ? (
                        <Vacio
                            titulo="Tu operación empieza aquí"
                            detalle="Las obras que tengas autorizadas aparecerán en este espacio."
                            accion={
                                puede(p.gestionarObras) && (
                                    <Link
                                        to="/obras"
                                        className="boton boton-secundario"
                                    >
                                        Registrar una obra
                                        <ArrowRight size={16} />
                                    </Link>
                                )
                            }
                        />
                    ) : (
                        <div className="obras-recientes">
                            {visibles.slice(0, 5).map((o) => (
                                <Link
                                    key={String(o.idObra)}
                                    to={
                                        puede(p.gestionarActividades)
                                            ? `/seguimiento?vista=actividades&idObra=${o.idObra}`
                                            : `/obras?estado=${o.estado}`
                                    }
                                    className="obra-resumen"
                                >
                                    <span className="icono-obra">
                                        <Building2 size={21} />
                                    </span>
                                    <div>
                                        <small>{String(o.codigo)}</small>
                                        <strong>{texto(o.nombre)}</strong>
                                        <span>
                                            <MapPin size={12} />
                                            {texto(o.direccion)}
                                        </span>
                                    </div>
                                    <Estado valor={o.estado} />
                                    <ArrowUpRight size={17} />
                                </Link>
                            ))}
                        </div>
                    )}
                </section>
                <section className="panel panel-accesos">
                    <div className="titulo-panel">
                        <div>
                            <p className="ceja">CONTINÚA TU TRABAJO</p>
                            <h2>Accesos frecuentes</h2>
                        </div>
                    </div>
                    <div className="accesos-frecuentes">
                        {accesos.map((a) => (
                            <Link key={a.ruta} to={a.ruta}>
                                <a.icono size={20} />
                                <span>
                                    <strong>{a.titulo}</strong>
                                    <small>{a.detalle}</small>
                                </span>
                                <ArrowRight size={16} />
                            </Link>
                        ))}
                    </div>
                </section>
            </div>
            {gerencia && lista.length > 0 && (
                <section className="panel panel-mapa">
                    <div className="titulo-panel">
                        <div>
                            <p className="ceja">VISIÓN GERENCIAL</p>
                            <h2>El alcance de tus obras</h2>
                        </div>
                        <Boton
                            variante="secundario"
                            aria-expanded={mostrarMapa}
                            onClick={() => setMostrarMapa((v) => !v)}
                        >
                            <MapPin size={17} />
                            {mostrarMapa ? "Ocultar mapa" : "Mostrar mapa"}
                        </Boton>
                    </div>
                    {mostrarMapa && (
                        <Suspense fallback={<Cargando variante="mapa" />}>
                            <MapaObras obras={visibles} />
                        </Suspense>
                    )}
                </section>
            )}
            {puede(p.gestionarInventarioEpp) &&
                puede(p.consultarConsolidadoAsistencia) && (
                    <DisponibilidadEpp />
                )}
            <div className="nota-operativa">
                <CircleCheck size={17} />
                <span>
                    Los datos corresponden a tu alcance autorizado. Las capturas
                    locales se confirman al sincronizar.
                </span>
            </div>
        </>
    );
}
