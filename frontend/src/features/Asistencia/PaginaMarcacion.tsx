import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
    Clock,
    MapPin,
    LocateFixed,
    ArrowRight,
    LogIn,
    LogOut,
    CircleCheck,
    History,
    WifiOff,
} from "lucide-react";
import { useSesion } from "../../app/Sesion";
import { useDatos } from "../../hooks/usoDatos";
import { useCola } from "../../hooks/usoCola";
import { api, ErrorApi } from "../../api/cliente";
import {
    Encabezado,
    Boton,
    Cargando,
    Vacio,
    Estado,
    ErrorVisible,
} from "../../components/Interfaz";
import { obtenerGps, validarLectura, type LecturaGps } from "./geocerca";
import { encolar, sincronizar } from "../../offline/sincronizacion";
import { fechaJornada, fecha, numero, texto } from "../../app/formatos";
import { configuracion } from "../../app/configuracion";
import { useAviso } from "../../app/Avisos";
import type {
    RespuestaAsignacionAsistencia,
    RespuestaAsistencia,
    RespuestaValidacionUbicacionAsistencia,
} from "../../api/contratos";

export default function PaginaMarcacion() {
    const { perfil } = useSesion();
    const aviso = useAviso();
    const asignaciones = useDatos<RespuestaAsignacionAsistencia[]>(
        "/api/asistencias/mis-asignaciones",
    );
    const [id, setId] = useState<number>();
    const [lectura, setLectura] = useState<LecturaGps | null>(null);
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const [ahora, setAhora] = useState(new Date());
    const { operaciones } = useCola();
    useEffect(() => {
        const reloj = window.setInterval(() => setAhora(new Date()), 1000);
        return () => clearInterval(reloj);
    }, []);
    useEffect(() => {
        if (asignaciones.datos?.length === 1)
            setId(asignaciones.datos[0].idAsignacion);
    }, [asignaciones.datos]);
    const asignacion = asignaciones.datos?.find((a) => a.idAsignacion === id);
    useEffect(() => {
        setLectura(null);
        setError(undefined);
    }, [id]);
    const abierta = useDatos<RespuestaAsistencia>(
        asignacion?.idAsistenciaAbierta
            ? `/api/asistencias/${asignacion.idAsistenciaAbierta}`
            : null,
    );
    const historial = useDatos<RespuestaAsistencia[]>(
        asignacion ? "/api/asistencias" : null,
        { idObra: asignacion?.idObra, pagina: 1, tamanoPagina: 10 },
    );
    const entradaLocal = [...operaciones]
        .reverse()
        .find(
            (o) =>
                o.coleccion === "asistencias" &&
                o.datos.idAsignacion === id &&
                o.estado !== "ATENCION" &&
                !operaciones.some(
                    (s) =>
                        s.coleccion === "salidasAsistencia" &&
                        s.datos.uuidRegistro === o.datos.uuidRegistro &&
                        s.estado === "CONFIRMADA",
                ) &&
                (o.estado !== "CONFIRMADA" ||
                    operaciones.some(
                        (s) =>
                            s.coleccion === "salidasAsistencia" &&
                            s.datos.uuidRegistro === o.datos.uuidRegistro &&
                            s.estado !== "CONFIRMADA",
                    ) ||
                    historial.datos?.some(
                        (h) =>
                            h.uuidRegistro === o.datos.uuidRegistro &&
                            h.estado === "ABIERTA",
                    )),
        );
    const uuidAbierta =
        abierta.datos?.uuidRegistro || entradaLocal?.datos.uuidRegistro;
    const salidaPendiente = operaciones.find(
        (o) =>
            o.coleccion === "salidasAsistencia" &&
            o.datos.uuidRegistro === uuidAbierta &&
            o.estado !== "CONFIRMADA",
    );
    const tieneEntrada = !!asignacion?.idAsistenciaAbierta || !!entradaLocal;
    const validacion =
        lectura &&
        asignacion &&
        asignacion.latitud != null &&
        asignacion.longitud != null &&
        asignacion.radioAsistenciaMetros != null
            ? validarLectura(
                  lectura,
                  asignacion.latitud,
                  asignacion.longitud,
                  asignacion.radioAsistenciaMetros,
              )
            : null;
    const puedeMarcar = !!validacion?.habilitada && error === undefined;
    const requiereReintento = error !== undefined || !!lectura;
    const verificar = async () => {
        const gps = await obtenerGps();
        setLectura(gps);
        if (
            !asignacion ||
            asignacion.latitud == null ||
            asignacion.longitud == null ||
            asignacion.radioAsistenciaMetros == null
        )
            throw new Error(
                "La obra no tiene una geocerca válida. Contacta a un responsable.",
            );
        const local = validarLectura(
            gps,
            asignacion.latitud,
            asignacion.longitud,
            asignacion.radioAsistenciaMetros,
        );
        if (!local.habilitada)
            throw new Error(
                local.distancia > asignacion.radioAsistenciaMetros
                    ? "Estás fuera del radio de la obra. Acércate para registrar asistencia."
                    : "La precisión GPS no permite asegurar que estás dentro del radio. Obtén otra lectura.",
            );
        if (asignacion.estadoObra !== "ACTIVA")
            throw new Error(
                "La obra no está activa para registrar asistencia.",
            );
        if (navigator.onLine) {
            try {
                const servidor =
                    await api.escribir<RespuestaValidacionUbicacionAsistencia>(
                        "POST",
                        "/api/asistencias/validar-ubicacion",
                        {
                            idAsignacion: asignacion.idAsignacion,
                            latitud: gps.latitud,
                            longitud: gps.longitud,
                            precisionGpsMetros: gps.precision,
                        },
                    );
                if (!servidor.marcacionHabilitada)
                    throw new Error(
                        "La ubicación no cumple la geocerca de la obra. Solicita otra lectura.",
                    );
            } catch (e) {
                if (e instanceof ErrorApi && e.estado < 500) throw e;
                if (!(e instanceof TypeError) && !(e instanceof ErrorApi))
                    throw e;
            }
        }
        return gps;
    };
    const obtener = async () => {
        setError(undefined);
        setOcupado(true);
        try {
            await verificar();
        } catch (e) {
            setError(e);
        } finally {
            setOcupado(false);
        }
    };
    const registrar = async () => {
        setError(undefined);
        setOcupado(true);
        try {
            const gps = await verificar();
            const capturadaEn = new Date().toISOString();
            if (tieneEntrada) {
                if (!uuidAbierta)
                    throw new Error(
                        "Carga la entrada registrada antes de guardar la salida.",
                    );
                await encolar(
                    perfil!.idUsuario,
                    "salidasAsistencia",
                    "Salida de asistencia",
                    {
                        idAsistencia: abierta.datos?.idAsistencia || null,
                        salidaEn: capturadaEn,
                        latitudSalida: gps.latitud,
                        longitudSalida: gps.longitud,
                        precisionGpsSalidaMetros: gps.precision,
                        uuidRegistro: uuidAbierta,
                    },
                );
            } else
                await encolar(
                    perfil!.idUsuario,
                    "asistencias",
                    "Entrada de asistencia",
                    {
                        idAsignacion: asignacion!.idAsignacion,
                        fechaJornada: fechaJornada(new Date(capturadaEn)),
                        entradaEn: capturadaEn,
                        latitudEntrada: gps.latitud,
                        longitudEntrada: gps.longitud,
                        precisionGpsEntradaMetros: gps.precision,
                    },
                );
            aviso(
                "Marcación guardada en este dispositivo. Consulta su confirmación en Pendientes.",
            );
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
                titulo={`Hola, ${perfil?.nombreUsuario}`}
                acciones={
                    <Link className="boton boton-secundario" to="/asistencia">
                        <History size={17} />
                        Mi asistencia
                    </Link>
                }
            />
            {asignaciones.isLoading ? (
                <Cargando variante="formulario" />
            ) : asignaciones.error ? (
                <ErrorVisible error={asignaciones.error} />
            ) : !asignaciones.datos?.length ? (
                <Vacio
                    titulo="No tienes una asignación vigente"
                    detalle="Consulta con tu Residente para revisar tu asignación a una obra."
                />
            ) : (
                <section
                    className="panel tarjeta-marcacion"
                    aria-labelledby="titulo-marcacion"
                >
                    <header className="cabecera-marcacion">
                        <div className="titulo-marcacion">
                            <Clock size={22} aria-hidden="true" />
                            <div>
                                <h2 id="titulo-marcacion">Mi jornada</h2>
                                <p className="fecha-jornada">
                                    {fecha(ahora.toISOString())}
                                </p>
                            </div>
                        </div>
                        <p className="hora-jornada">
                            {new Intl.DateTimeFormat("es-NI", {
                                timeZone: configuracion.zonaHoraria,
                                hour: "2-digit",
                                minute: "2-digit",
                                second: "2-digit",
                                hour12: false,
                            }).format(ahora)}
                        </p>
                    </header>
                    <div className="contenido-marcacion">
                        <div className="asignacion-marcacion">
                            <label className="campo">
                                Obra asignada
                                <select
                                    value={id || ""}
                                    onChange={(e) =>
                                        setId(
                                            Number(e.target.value) || undefined,
                                        )
                                    }
                                >
                                    <option value="">Selecciona tu obra</option>
                                    {asignaciones.datos.map((a) => (
                                        <option
                                            key={a.idAsignacion}
                                            value={a.idAsignacion}
                                        >
                                            {texto(a.nombreObra)} ·{" "}
                                            {a.codigoObra}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            {asignacion && (
                                <div className="ubicacion-obra">
                                    <MapPin size={17} aria-hidden="true" />
                                    <span>{texto(asignacion.direccion)}</span>
                                </div>
                            )}
                            <div className="estado-jornada">
                                <span>Tu jornada</span>
                                <strong>
                                    {salidaPendiente
                                        ? "Salida pendiente de confirmación"
                                        : tieneEntrada
                                          ? "Entrada registrada"
                                          : "Sin entrada abierta"}
                                </strong>
                            </div>
                        </div>
                        <div className="ubicacion-marcacion">
                            <div className="resumen-ubicacion" role="status">
                                {puedeMarcar ? (
                                    <CircleCheck size={22} aria-hidden="true" />
                                ) : (
                                    <LocateFixed size={22} aria-hidden="true" />
                                )}
                                <div>
                                    <h3>
                                        {puedeMarcar
                                            ? "Dentro del radio de la obra"
                                            : requiereReintento
                                              ? "Ubicación por verificar"
                                              : "Ubicación pendiente"}
                                    </h3>
                                    <p>
                                        {puedeMarcar
                                            ? "Puedes registrar tu asistencia."
                                            : !asignacion
                                              ? "Selecciona una obra para continuar."
                                              : asignacion.estadoObra !==
                                                  "ACTIVA"
                                                ? "La obra no está activa para marcar."
                                                : requiereReintento
                                                  ? "Revisa el aviso y solicita otra lectura."
                                                  : "Verifica tu GPS para habilitar la marcación."}
                                    </p>
                                </div>
                            </div>
                            {lectura && (
                                <dl className="medidas-ubicacion">
                                    <div>
                                        <dt>Distancia</dt>
                                        <dd>
                                            {numero(validacion?.distancia)} m
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>Precisión GPS</dt>
                                        <dd>± {numero(lectura.precision)} m</dd>
                                    </div>
                                    <div>
                                        <dt>Radio de la obra</dt>
                                        <dd>
                                            {numero(
                                                asignacion?.radioAsistenciaMetros,
                                            )}{" "}
                                            m
                                        </dd>
                                    </div>
                                </dl>
                            )}
                        </div>
                    </div>
                    <div className="accion-marcacion">
                        {error !== undefined && <ErrorVisible error={error} />}
                        {!navigator.onLine && (
                            <div className="mensaje">
                                <WifiOff size={18} />
                                Sin conexión. Tu marcación se guardará en este
                                dispositivo y quedará pendiente hasta recuperar
                                la conexión.
                            </div>
                        )}
                        <Boton
                            ocupado={ocupado}
                            onClick={puedeMarcar ? registrar : obtener}
                            disabled={
                                !asignacion ||
                                !!salidaPendiente ||
                                asignacion.estadoObra !== "ACTIVA" ||
                                (tieneEntrada && !uuidAbierta)
                            }
                            aria-describedby="ayuda-marcacion"
                            className="ancho-completo"
                        >
                            {!puedeMarcar ? (
                                <LocateFixed size={20} aria-hidden="true" />
                            ) : tieneEntrada ? (
                                <LogOut size={20} aria-hidden="true" />
                            ) : (
                                <LogIn size={20} aria-hidden="true" />
                            )}
                            {puedeMarcar
                                ? `Registrar ${tieneEntrada ? "salida" : "entrada"}`
                                : requiereReintento
                                  ? "Reintentar ubicación"
                                  : "Verificar ubicación"}
                        </Boton>
                        <p id="ayuda-marcacion" className="ayuda-marcacion">
                            El GPS se comprueba al verificar y al marcar, sin
                            rastreo continuo.
                        </p>
                    </div>
                </section>
            )}
            <section className="panel historial-reciente">
                <div className="titulo-panel">
                    <h2>Mis registros recientes</h2>
                    <Link to="/asistencia" className="enlace-accion">
                        Ver historial
                        <ArrowRight size={16} />
                    </Link>
                </div>
                {historial.datos?.length ? (
                    <div className="registros-personales">
                        {historial.datos.slice(0, 5).map((r) => (
                            <div key={r.idAsistencia}>
                                <span>
                                    <Clock size={18} />
                                    <strong>{fecha(r.fechaJornada)}</strong>
                                </span>
                                <span>
                                    {fecha(r.entradaEn, true)} →{" "}
                                    {r.salidaEn
                                        ? fecha(r.salidaEn, true)
                                        : "Salida pendiente"}
                                </span>
                                <Estado valor={r.estado} />
                            </div>
                        ))}
                    </div>
                ) : (
                    <p className="ayuda-campo">
                        Selecciona tu obra para consultar los registros
                        recientes.
                    </p>
                )}
            </section>
        </>
    );
}
