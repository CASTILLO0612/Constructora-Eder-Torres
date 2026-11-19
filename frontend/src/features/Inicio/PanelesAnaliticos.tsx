import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
    ArrowUpRight,
    CalendarDays,
    HardHat,
    SlidersHorizontal,
} from "lucide-react";
import { useSesion } from "../../app/Sesion";
import { useDatos } from "../../hooks/usoDatos";
import { permisos as p } from "../../api/permisos";
import { catalogos } from "../../api/catalogos";
import { Cargando, ErrorVisible, Vacio } from "../../components/Interfaz";
import {
    BarrasComparativas,
    GraficoDistribucion,
    GraficoEvolucion,
} from "../../components/Graficos";
import {
    fechaJornada,
    etiqueta,
    fecha,
    numero,
    texto,
} from "../../app/formatos";
import { agruparAsistencia, desplazarFecha } from "./datosGraficos";
import type { Fila } from "../Modulos/catalogo";
import type {
    RespuestaActividad,
    RespuestaAsistencia,
    ResumenExistenciaTipoEpp,
} from "../../api/contratos";

export function DistribucionObras({
    obras,
    seleccion,
    cambiar,
    cargando,
    error,
}: {
    obras: Fila[];
    seleccion: string | null;
    cambiar: (valor: string | null) => void;
    cargando: boolean;
    error?: unknown;
}) {
    return (
        <section
            className="panel panel-grafico"
            aria-labelledby="titulo-estados-obras"
        >
            <div className="titulo-panel">
                <div>
                    <p className="ceja">PORTAFOLIO</p>
                    <h2 id="titulo-estados-obras">Estado de las obras</h2>
                </div>
                <SlidersHorizontal size={19} />
            </div>
            {cargando ? (
                <Cargando variante="grafico" />
            ) : error ? (
                <ErrorVisible error={error} />
            ) : obras.length ? (
                <GraficoDistribucion
                    grupos={catalogos.EstadosObra.map((e) => ({
                        codigo: e,
                        nombre: etiqueta(e),
                        valor: obras.filter((o) => o.estado === e).length,
                    }))}
                    seleccion={seleccion}
                    cambiar={cambiar}
                />
            ) : (
                <Vacio
                    titulo="Tu portafolio, en una vista"
                    detalle="El gráfico se construirá con las obras que tengas autorizadas."
                />
            )}
            {!cargando && !error && (
                <div className="pie-grafico">
                    <span>
                        {obras.length} obras consultadas
                        {obras.length >= 100 ? " · Vista limitada a 100" : ""}
                    </span>
                    <Link
                        className="enlace-accion"
                        to={seleccion ? `/obras?estado=${seleccion}` : "/obras"}
                    >
                        Explorar obras
                        <ArrowUpRight size={16} />
                    </Link>
                </div>
            )}
        </section>
    );
}

export function EvolucionAsistencia() {
    const [dias, setDias] = useState(7);
    const [medida, setMedida] = useState<"marcaciones" | "horas">(
        "marcaciones",
    );
    const hasta = fechaJornada(),
        desde = desplazarFecha(hasta, 1 - dias);
    const consulta = useDatos<RespuestaAsistencia[]>("/api/asistencias", {
        fechaDesde: desde,
        fechaHasta: hasta,
        pagina: 1,
        tamanoPagina: 100,
    });
    const puntos = useMemo(
        () => agruparAsistencia(consulta.datos || [], hasta, dias),
        [consulta.datos, hasta, dias],
    );
    const total = puntos.reduce((s, v) => s + v[medida], 0);
    return (
        <section
            className="panel panel-grafico"
            aria-labelledby="titulo-evolucion-asistencia"
        >
            <div className="titulo-panel">
                <div>
                    <p className="ceja">ACTIVIDAD DEL PERSONAL</p>
                    <h2 id="titulo-evolucion-asistencia">
                        Asistencia del período
                    </h2>
                </div>
                <label className="periodo-panel">
                    <span className="visualmente-oculto">
                        Período del gráfico de asistencia
                    </span>
                    <CalendarDays size={16} />
                    <select
                        value={dias}
                        onChange={(e) => setDias(Number(e.target.value))}
                    >
                        <option value={7}>7 días</option>
                        <option value={14}>14 días</option>
                    </select>
                </label>
            </div>
            <div className="resumen-grafico">
                <strong>
                    {consulta.datos ? (
                        numero(total, medida === "horas" ? 1 : 0)
                    ) : (
                        <span
                            className="esqueleto-bloque esqueleto-valor"
                            aria-hidden="true"
                        />
                    )}
                </strong>
                <div
                    className="control-segmentado"
                    aria-label="Medida del gráfico"
                >
                    {(["marcaciones", "horas"] as const).map((v) => (
                        <button
                            key={v}
                            type="button"
                            aria-pressed={medida === v}
                            onClick={() => setMedida(v)}
                        >
                            {v === "horas" ? "Horas" : "Registros"}
                        </button>
                    ))}
                </div>
            </div>
            {consulta.isLoading ? (
                <Cargando variante="grafico" />
            ) : consulta.error ? (
                <ErrorVisible error={consulta.error} />
            ) : (
                <GraficoEvolucion puntos={puntos} medida={medida} />
            )}
            <div className="pie-grafico">
                <span>
                    {consulta.cache
                        ? `Consulta guardada · ${fecha(consulta.guardadoEn, true)}`
                        : `${fecha(desde)} — ${fecha(hasta)}`}
                    {(consulta.datos?.length || 0) >= 100
                        ? " · Primeros 100 registros de la consulta"
                        : ""}
                    {medida === "horas"
                        ? " · Jornadas cerradas no anuladas"
                        : ""}
                </span>
                <Link
                    className="enlace-accion"
                    to={`/asistencia?fechaDesde=${desde}&fechaHasta=${hasta}`}
                >
                    Ver registros
                    <ArrowUpRight size={16} />
                </Link>
            </div>
        </section>
    );
}

export function AvanceActividades({
    obras,
    cargandoObras,
    errorObras,
}: {
    obras: Fila[];
    cargandoObras: boolean;
    errorObras?: unknown;
}) {
    const [idObra, setIdObra] = useState("");
    const [orden, setOrden] = useState("pendientes");
    useEffect(() => {
        if (obras.length && !obras.some((o) => String(o.idObra) === idObra))
            setIdObra(
                String(
                    (obras.find((o) => o.estado === "ACTIVA") || obras[0])
                        .idObra,
                ),
            );
    }, [obras, idObra]);
    const consulta = useDatos<RespuestaActividad[]>(
        idObra ? `/api/seguimiento/obras/${idObra}/actividades` : null,
        { pagina: 1, tamanoPagina: 100 },
    );
    const actividades = useMemo(
        () =>
            [...(consulta.datos || [])].sort((a, b) =>
                orden === "nombre"
                    ? a.nombre.localeCompare(b.nombre)
                    : (a.cantidadEjecutadaAcumulada / a.cantidadPlanificada -
                          b.cantidadEjecutadaAcumulada /
                              b.cantidadPlanificada) *
                      (orden === "pendientes" ? 1 : -1),
            ),
        [consulta.datos, orden],
    );
    return (
        <section
            className="panel panel-grafico panel-avances"
            aria-labelledby="titulo-avance-actividades"
        >
            <div className="titulo-panel">
                <div>
                    <p className="ceja">EJECUCIÓN FÍSICA</p>
                    <h2 id="titulo-avance-actividades">Avance por actividad</h2>
                    <p className="detalle-panel">
                        Compara lo ejecutado con la cantidad planificada.
                    </p>
                </div>
                <div className="controles-grafico">
                    <label>
                        <span>Obra</span>
                        <select
                            aria-label="Obra del gráfico de avance"
                            value={idObra}
                            onChange={(e) => setIdObra(e.target.value)}
                        >
                            <option value="">Seleccionar obra</option>
                            {obras.map((o) => (
                                <option
                                    key={String(o.idObra)}
                                    value={String(o.idObra)}
                                >
                                    {texto(o.nombre)}
                                </option>
                            ))}
                        </select>
                    </label>
                    <label>
                        <span>Orden</span>
                        <select
                            aria-label="Orden del gráfico de avance"
                            value={orden}
                            onChange={(e) => setOrden(e.target.value)}
                        >
                            <option value="pendientes">Menor avance</option>
                            <option value="avance">Mayor avance</option>
                            <option value="nombre">Nombre</option>
                        </select>
                    </label>
                </div>
            </div>
            {consulta.isLoading ||
            cargandoObras ||
            (obras.length > 0 && !idObra) ? (
                <Cargando variante="grafico" />
            ) : errorObras ? (
                <ErrorVisible error={errorObras} />
            ) : consulta.error ? (
                <ErrorVisible error={consulta.error} />
            ) : !actividades.length ? (
                <Vacio
                    titulo="Actividades por planificar"
                    detalle="Al registrar actividades y avances podrás comparar su ejecución aquí."
                />
            ) : (
                <BarrasComparativas
                    unidad="porcentaje"
                    filas={actividades.slice(0, 6).map((a) => ({
                        id: String(a.idActividad),
                        nombre: a.nombre,
                        valor: a.cantidadEjecutadaAcumulada,
                        maximo: a.cantidadPlanificada,
                        detalle: `${numero(a.cantidadEjecutadaAcumulada)} de ${numero(a.cantidadPlanificada)} ${a.unidadMedida} · ${etiqueta(a.estado)}`,
                    }))}
                />
            )}
            <div className="pie-grafico">
                <span>
                    {Math.min(6, actividades.length)} de {actividades.length}{" "}
                    actividades consultadas
                </span>
                <Link
                    className="enlace-accion"
                    to={`/seguimiento?vista=actividades&idObra=${idObra}`}
                >
                    Abrir seguimiento
                    <ArrowUpRight size={16} />
                </Link>
            </div>
        </section>
    );
}

export function DisponibilidadEpp() {
    const consulta = useDatos<ResumenExistenciaTipoEpp[]>(
        "/api/epp/existencias/resumen",
        { pagina: 1, tamanoPagina: 100 },
    );
    const [vista, setVista] = useState<"totalDisponibles" | "totalEntregados">(
        "totalDisponibles",
    );
    const filas = consulta.datos || [];
    const maximo = Math.max(1, ...filas.map((f) => f[vista]));
    return (
        <section
            className="panel panel-grafico"
            aria-labelledby="titulo-disponibilidad-epp"
        >
            <div className="titulo-panel">
                <div>
                    <p className="ceja">PROTECCIÓN DEL PERSONAL</p>
                    <h2 id="titulo-disponibilidad-epp">EPP por tipo</h2>
                </div>
                <HardHat size={20} />
            </div>
            <div className="control-segmentado selector-inventario">
                {(["totalDisponibles", "totalEntregados"] as const).map((v) => (
                    <button
                        type="button"
                        key={v}
                        aria-pressed={vista === v}
                        onClick={() => setVista(v)}
                    >
                        {v === "totalDisponibles" ? "Disponible" : "Entregado"}
                    </button>
                ))}
            </div>
            {consulta.isLoading ? (
                <Cargando variante="grafico" />
            ) : consulta.error ? (
                <ErrorVisible error={consulta.error} />
            ) : !filas.length ? (
                <Vacio
                    titulo="Inventario por registrar"
                    detalle="Los equipos aparecerán agrupados por su tipo."
                />
            ) : (
                <BarrasComparativas
                    unidad="unidades"
                    filas={filas.slice(0, 6).map((f) => ({
                        id: String(f.idTipoEpp),
                        nombre: f.nombreTipoEpp,
                        valor: f[vista],
                        maximo,
                        detalle: `${f.totalDisponibles} disponibles · ${f.totalEntregados} entregados${f.bajoExistenciaMinima ? " · Bajo el mínimo configurado" : ""}`,
                    }))}
                />
            )}
            <div className="pie-grafico">
                <span>Unidades físicas consultadas</span>
                <Link className="enlace-accion" to="/epp?vista=existencias">
                    Ver inventario
                    <ArrowUpRight size={16} />
                </Link>
            </div>
        </section>
    );
}

export function PanelesAnaliticos({
    obras,
    cargandoObras,
    errorObras,
    estado,
    cambiarEstado,
}: {
    obras: Fila[];
    cargandoObras: boolean;
    errorObras?: unknown;
    estado: string | null;
    cambiarEstado: (valor: string | null) => void;
}) {
    const { puede } = useSesion();
    return (
        <>
            <div className="rejilla-analitica">
                {puede(p.consultarObras) && (
                    <DistribucionObras
                        obras={obras}
                        cargando={cargandoObras}
                        error={errorObras}
                        seleccion={estado}
                        cambiar={cambiarEstado}
                    />
                )}{" "}
                {puede(p.consultarConsolidadoAsistencia) && (
                    <EvolucionAsistencia />
                )}
                {puede(p.gestionarInventarioEpp) &&
                    !puede(p.consultarConsolidadoAsistencia) && (
                        <DisponibilidadEpp />
                    )}
            </div>
            {puede(p.gestionarActividades) && (
                <AvanceActividades
                    obras={obras}
                    cargandoObras={cargandoObras}
                    errorObras={errorObras}
                />
            )}
        </>
    );
}
