import { useId, useState } from "react";
import { MapPin, LocateFixed, Users, Check } from "lucide-react";
import { Link } from "react-router-dom";
import {
    Encabezado,
    Boton,
    ErrorVisible,
    Cargando,
    Vacio,
} from "../../components/Interfaz";
import { SelectorReferencia } from "../../components/SelectorReferencia";
import { useDatos } from "../../hooks/usoDatos";
import { useSesion } from "../../app/Sesion";
import { useAviso } from "../../app/Avisos";
import { fechaJornada } from "../../app/formatos";
import { obtenerGps, validarLectura } from "./geocerca";
import { encolar, sincronizar } from "../../offline/sincronizacion";
import type {
    RespuestaObra,
    RespuestaAsignacionTrabajadorObra,
    RespuestaTrabajadorCuadrilla,
    RespuestaAsistencia,
} from "../../api/contratos";

export default function PaginaAsistida() {
    const { perfil } = useSesion();
    const aviso = useAviso();
    const idFiltros = useId();
    const [idObra, setIdObra] = useState<number>();
    const [tipo, setTipo] = useState<"entrada" | "salida">("entrada");
    const [seleccion, setSeleccion] = useState<number[]>([]);
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const [buscar, setBuscar] = useState("");
    const obra = useDatos<RespuestaObra>(
        idObra ? `/api/obras/${idObra}` : null,
    );
    const asignaciones = useDatos<RespuestaAsignacionTrabajadorObra[]>(
        idObra ? `/api/personal/obras/${idObra}/asignaciones` : null,
        { soloVigentes: true, pagina: 1, tamanoPagina: 100 },
        true,
    );
    const miembros = useDatos<RespuestaTrabajadorCuadrilla[]>(
        idObra ? `/api/personal/obras/${idObra}/miembros-cuadrilla` : null,
        { pagina: 1, tamanoPagina: 100 },
        true,
    );
    const abiertas = useDatos<RespuestaAsistencia[]>(
        idObra ? "/api/asistencias" : null,
        { idObra, estado: "ABIERTA", pagina: 1, tamanoPagina: 100 },
        true,
    );
    const disponibles =
        tipo === "entrada"
            ? asignaciones.datos || []
            : (abiertas.datos || []).map((a) => ({
                  idAsignacion: a.idAsignacion,
                  nombreCompletoTrabajador: a.nombreCompletoTrabajador,
                  cedulaTrabajador: "",
                  nombreEspecialidad: asignaciones.datos?.find(
                      (x) => x.idAsignacion === a.idAsignacion,
                  )?.nombreEspecialidad,
                  tipoContratacion: "Sin especialidad",
              }));
    const lista = disponibles.filter((a) =>
        a.nombreCompletoTrabajador
            .toLocaleLowerCase()
            .includes(buscar.toLocaleLowerCase()),
    );
    const grupos = new Map<string, typeof lista>();
    for (const fila of lista) {
        const hoy = fechaJornada();
        const cuadrillas = (miembros.datos || [])
            .filter(
                (m) =>
                    m.idAsignacion === fila.idAsignacion &&
                    m.fechaInicio <= hoy &&
                    (!m.fechaFin || m.fechaFin >= hoy),
            )
            .map((m) => m.nombreCuadrilla);
        const grupo = `${[...new Set(cuadrillas)].sort().join(" / ") || "Sin cuadrilla"} · ${fila.nombreEspecialidad || "Sin especialidad"}`;
        grupos.set(grupo, [...(grupos.get(grupo) || []), fila]);
    }
    const marcar = async () => {
        setOcupado(true);
        setError(undefined);
        try {
            if (!seleccion.length)
                throw new Error("Selecciona al menos un trabajador.");
            const o = obra.datos;
            if (
                !o ||
                o.latitud == null ||
                o.longitud == null ||
                o.radioAsistenciaMetros == null
            )
                throw new Error(
                    "Carga una obra con geocerca válida antes de marcar.",
                );
            if (o.estado !== "ACTIVA")
                throw new Error(
                    "La obra debe estar activa para registrar asistencia.",
                );
            const gps = await obtenerGps();
            if (
                !validarLectura(
                    gps,
                    o.latitud,
                    o.longitud,
                    o.radioAsistenciaMetros,
                ).habilitada
            )
                throw new Error(
                    "La ubicación del dispositivo no cumple el radio de la obra considerando su precisión.",
                );
            const instante = new Date().toISOString();
            if (tipo === "salida") {
                const ids = seleccion.map(
                    (id) =>
                        abiertas.datos?.find((a) => a.idAsignacion === id)
                            ?.idAsistencia,
                );
                if (ids.some((id) => !id))
                    throw new Error(
                        "Actualiza la lista: una asistencia seleccionada ya no está disponible.",
                    );
                await encolar(
                    perfil!.idUsuario,
                    "salidasCuadrilla",
                    "Lista asistida de salidas",
                    {
                        idObra,
                        salidaEn: instante,
                        idsAsistencias: ids,
                        latitudLectura: gps.latitud,
                        longitudLectura: gps.longitud,
                        precisionGpsMetros: gps.precision,
                    },
                    "SolicitudRegistrarSalidasCuadrilla",
                );
            } else {
                await encolar(
                    perfil!.idUsuario,
                    "asistenciaCuadrilla",
                    "Lista asistida de entradas",
                    {
                        idObra,
                        fechaJornada: fechaJornada(new Date(instante)),
                        latitudLectura: gps.latitud,
                        longitudLectura: gps.longitud,
                        precisionGpsMetros: gps.precision,
                        marcaciones: seleccion.map((idAsignacion) => ({
                            idAsignacion,
                            entradaEn: instante,
                            uuidRegistro: crypto.randomUUID(),
                        })),
                    },
                    "SolicitudRegistrarAsistenciaCuadrilla",
                );
            }
            aviso(
                "Lista guardada. Consulta la confirmación de las asistencias en Pendientes.",
            );
            setSeleccion([]);
            void sincronizar(perfil!.idUsuario).catch(() => undefined);
        } catch (e) {
            setError(e);
        } finally {
            setOcupado(false);
        }
    };
    return (
        <>
            <Encabezado
                titulo="Marcación asistida"
                acciones={
                    <Link className="boton boton-secundario" to="/asistencia">
                        Ver registros
                    </Link>
                }
            />
            <div className="panel panel-asistencia">
                <div className="barra-filtros">
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
                            valor={idObra}
                            cambiar={(v) => {
                                setIdObra(Number(v) || undefined);
                                setSeleccion([]);
                            }}
                            etiquetaVisible="Obra"
                        />
                    </div>
                    <div className="filtro">
                        <label
                            className="etiqueta-filtro"
                            htmlFor={`${idFiltros}-tipo`}
                        >
                            Marcación
                        </label>
                        <select
                            id={`${idFiltros}-tipo`}
                            value={tipo}
                            onChange={(e) => {
                                setTipo(e.target.value as typeof tipo);
                                setSeleccion([]);
                            }}
                        >
                            <option value="entrada">Entradas</option>
                            <option value="salida">Salidas</option>
                        </select>
                    </div>
                    <div className="filtro">
                        <label
                            className="etiqueta-filtro"
                            htmlFor={`${idFiltros}-buscar`}
                        >
                            Trabajador
                        </label>
                        <div className="buscar-tabla">
                            <Users size={18} />
                            <input
                                id={`${idFiltros}-buscar`}
                                placeholder="Buscar trabajador…"
                                aria-label="Buscar trabajador"
                                value={buscar}
                                onChange={(e) => setBuscar(e.target.value)}
                            />
                        </div>
                    </div>
                </div>
                <div className="mensaje">
                    <MapPin size={18} />
                    La ubicación corresponde al dispositivo del Residente. No
                    acredita individualmente la ubicación de cada persona de
                    esta lista.
                </div>
                {error !== undefined && <ErrorVisible error={error} />}{" "}
                {!idObra ? (
                    <Vacio
                        titulo="Selecciona una obra"
                        detalle="Se mostrarán sus asignaciones vigentes."
                    />
                ) : asignaciones.isLoading ||
                  miembros.isLoading ||
                  abiertas.isLoading ? (
                    <Cargando />
                ) : asignaciones.error || miembros.error || abiertas.error ? (
                    <ErrorVisible
                        error={
                            asignaciones.error ||
                            miembros.error ||
                            abiertas.error
                        }
                    />
                ) : lista.length === 0 ? (
                    <Vacio
                        titulo={
                            tipo === "salida"
                                ? "No hay asistencias abiertas"
                                : "No hay trabajadores para mostrar"
                        }
                        detalle="Revisa la obra y la búsqueda."
                    />
                ) : (
                    <div className="lista-asistida">
                        {[...grupos.entries()]
                            .sort(([a], [b]) => a.localeCompare(b, "es"))
                            .map(([nombre, filas]) => (
                                <section
                                    className="grupo-asistido"
                                    key={nombre}
                                    aria-label={nombre}
                                >
                                    <h2>{nombre}</h2>
                                    {filas.map((a) => (
                                        <label
                                            className="fila-asistida"
                                            key={a.idAsignacion}
                                        >
                                            <input
                                                type="checkbox"
                                                checked={seleccion.includes(
                                                    a.idAsignacion,
                                                )}
                                                onChange={(e) =>
                                                    setSeleccion(
                                                        e.target.checked
                                                            ? [
                                                                  ...seleccion,
                                                                  a.idAsignacion,
                                                              ]
                                                            : seleccion.filter(
                                                                  (x) =>
                                                                      x !==
                                                                      a.idAsignacion,
                                                              ),
                                                    )
                                                }
                                            />
                                            <span>
                                                <strong>
                                                    {a.nombreCompletoTrabajador}
                                                </strong>
                                                <small>
                                                    {a.cedulaTrabajador} ·{" "}
                                                    {a.nombreEspecialidad ||
                                                        a.tipoContratacion}
                                                </small>
                                            </span>
                                            <Check size={18} />
                                        </label>
                                    ))}
                                </section>
                            ))}
                    </div>
                )}
                <div className="pie-formulario">
                    <span>{seleccion.length} trabajadores seleccionados</span>
                    <Boton
                        ocupado={ocupado}
                        disabled={
                            !seleccion.length ||
                            asignaciones.isLoading ||
                            miembros.isLoading ||
                            abiertas.isLoading ||
                            !!(
                                asignaciones.error ||
                                miembros.error ||
                                abiertas.error
                            )
                        }
                        onClick={marcar}
                    >
                        <LocateFixed size={17} />
                        Obtener GPS y registrar{" "}
                        {tipo === "salida" ? "salidas" : "entradas"}
                    </Boton>
                </div>
            </div>
        </>
    );
}
