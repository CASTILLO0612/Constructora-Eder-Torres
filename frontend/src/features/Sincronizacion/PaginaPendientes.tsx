import { lazy, Suspense, useCallback, useState } from "react";
import {
    RefreshCw,
    CircleCheck,
    Clock,
    TriangleAlert,
    ChevronDown,
    Pencil,
} from "lucide-react";
import {
    Encabezado,
    Boton,
    Estado,
    ErrorVisible,
    Vacio,
    Cargando,
} from "../../components/Interfaz";
import { useCola } from "../../hooks/usoCola";
import { useSesion } from "../../app/Sesion";
import { fecha } from "../../app/formatos";
import { reintentarOperacion, sincronizar } from "../../offline/sincronizacion";
import { nombreCampo } from "../../components/etiquetas";
import type { OperacionPendiente } from "../../offline/almacenamiento";
import { obtenerOperacion } from "../../offline/almacenamiento";
const Formulario = lazy(() =>
    import("../../components/FormularioOperacion").then((m) => ({
        default: m.FormularioOperacion,
    })),
);
export default function PaginaPendientes() {
    const { perfil } = useSesion();
    const {
        operaciones,
        pendientes,
        refrescar,
        error: errorLocal,
        cargando,
    } = useCola();
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const [vista, setVista] = useState("pendientes");
    const [editando, setEditando] = useState<OperacionPendiente | null>(null);
    const cerrar = useCallback(() => setEditando(null), []);
    const enviar = async () => {
        setOcupado(true);
        setError(undefined);
        try {
            await sincronizar(perfil!.idUsuario);
            await refrescar();
        } catch (e) {
            setError(e);
        } finally {
            setOcupado(false);
        }
    };
    const visibles = operaciones
        .filter((o) =>
            vista === "pendientes"
                ? o.estado !== "CONFIRMADA"
                : o.estado === "CONFIRMADA",
        )
        .reverse();
    return (
        <>
            <Encabezado
                titulo="Pendientes y sincronización"
                acciones={
                    <Boton
                        ocupado={ocupado}
                        onClick={enviar}
                        disabled={!navigator.onLine || !pendientes.length}
                    >
                        <RefreshCw size={17} />
                        Sincronizar ahora
                    </Boton>
                }
            />
            {cargando ? (
                <Cargando
                    variante="tarjetas"
                    texto="Cargando pendientes locales…"
                />
            ) : (
                <div className="metricas metricas-tres">
                    <div>
                        <span>
                            Por confirmar
                            <Clock size={18} />
                        </span>
                        <strong>{pendientes.length}</strong>
                        <small>Guardadas en este dispositivo</small>
                    </div>
                    <div>
                        <span>
                            Necesitan atención
                            <TriangleAlert size={18} />
                        </span>
                        <strong>
                            {
                                operaciones.filter(
                                    (o) => o.estado === "ATENCION",
                                ).length
                            }
                        </strong>
                        <small>Revisa el motivo antes de reenviar</small>
                    </div>
                    <div>
                        <span>
                            Confirmadas
                            <CircleCheck size={18} />
                        </span>
                        <strong>
                            {
                                operaciones.filter(
                                    (o) => o.estado === "CONFIRMADA",
                                ).length
                            }
                        </strong>
                        <small>Con acuse del servidor</small>
                    </div>
                </div>
            )}
            <nav className="pestanas" aria-label="Estado de sincronización">
                <button
                    className={vista === "pendientes" ? "activa" : ""}
                    onClick={() => setVista("pendientes")}
                >
                    Pendientes
                </button>
                <button
                    className={vista === "confirmadas" ? "activa" : ""}
                    onClick={() => setVista("confirmadas")}
                >
                    Confirmadas
                </button>
            </nav>
            {error !== undefined && <ErrorVisible error={error} />}
            {errorLocal !== undefined && <ErrorVisible error={errorLocal} />}
            <section className="panel-registros">
                {cargando ? (
                    <Cargando />
                ) : !visibles.length ? (
                    <Vacio
                        titulo={
                            vista === "pendientes"
                                ? "Todo está al día"
                                : "No hay confirmaciones en este dispositivo"
                        }
                        detalle="Las capturas aparecerán aquí con su estado individual."
                    />
                ) : (
                    <div className="lista-pendientes">
                        {visibles.map((o) => (
                            <article
                                key={o.idLocal}
                                className="operacion-local"
                            >
                                <div className="operacion-principal">
                                    <span className="icono-operacion">
                                        {o.estado === "CONFIRMADA" ? (
                                            <CircleCheck size={22} />
                                        ) : o.estado === "ATENCION" ? (
                                            <TriangleAlert size={22} />
                                        ) : (
                                            <Clock size={22} />
                                        )}
                                    </span>
                                    <div>
                                        <h3>{o.titulo}</h3>
                                        <p>
                                            {fecha(o.creadoEn, true)} ·{" "}
                                            {o.intentos} intentos
                                        </p>
                                    </div>
                                    <Estado valor={o.estado} />
                                </div>
                                {o.mensaje && (
                                    <p className="motivo-operacion">
                                        {o.mensaje}
                                    </p>
                                )}
                                <details className="detalle-operacion">
                                    <summary>
                                        Ver datos de la captura
                                        <ChevronDown size={15} />
                                    </summary>
                                    <dl>
                                        {Object.entries(o.datos)
                                            .filter(
                                                ([k]) =>
                                                    ![
                                                        "contenidoArchivo",
                                                        "esSincronizacionDiferida",
                                                    ].includes(k),
                                            )
                                            .map(([k, v]) => (
                                                <div key={k}>
                                                    <dt>{nombreCampo(k)}</dt>
                                                    <dd>
                                                        {typeof v === "object"
                                                            ? JSON.stringify(v)
                                                            : String(v ?? "—")}
                                                    </dd>
                                                </div>
                                            ))}
                                        {o.idServidor && (
                                            <div>
                                                <dt>Registro confirmado</dt>
                                                <dd>{o.idServidor}</dd>
                                            </div>
                                        )}
                                    </dl>
                                </details>
                                {o.estado !== "CONFIRMADA" &&
                                    o.estado !== "ENVIANDO" && (
                                        <div className="acciones-operacion">
                                            {o.estado === "ATENCION" &&
                                                o.esquema &&
                                                o.coleccion !==
                                                    "asistenciaCuadrilla" &&
                                                o.coleccion !==
                                                    "salidasCuadrilla" &&
                                                !/autoriz|conflicto|uuid|solapamiento/i.test(
                                                    o.codigo || "",
                                                ) && (
                                                    <Boton
                                                        variante="secundario"
                                                        onClick={() =>
                                                            void obtenerOperacion(
                                                                o.idLocal,
                                                            ).then(
                                                                (original) => {
                                                                    if (
                                                                        original &&
                                                                        original.idUsuario ===
                                                                            perfil?.idUsuario
                                                                    )
                                                                        setEditando(
                                                                            original,
                                                                        );
                                                                },
                                                            )
                                                        }
                                                    >
                                                        <Pencil size={15} />
                                                        Revisar datos
                                                    </Boton>
                                                )}
                                            <Boton
                                                variante="secundario"
                                                onClick={async () => {
                                                    await reintentarOperacion(
                                                        o,
                                                    );
                                                    await enviar();
                                                }}
                                                disabled={!navigator.onLine}
                                            >
                                                <RefreshCw size={15} />
                                                Reintentar misma captura
                                            </Boton>
                                        </div>
                                    )}
                            </article>
                        ))}
                    </div>
                )}
            </section>
            {editando?.esquema && (
                <Suspense fallback={<Cargando variante="formulario" />}>
                    <Formulario
                        accion={{
                            titulo: "Revisar captura pendiente",
                            esquema: editando.esquema,
                            metodo: "POST",
                            ruta: "",
                            permiso: "",
                            coleccion: editando.coleccion,
                        }}
                        fila={editando.datos}
                        operacionExistente={editando}
                        idObra={
                            Number(editando.datos.idObra) ||
                            Number(
                                editando.rutaConsulta?.match(
                                    /obras\/(\d+)/,
                                )?.[1],
                            ) ||
                            undefined
                        }
                        cerrar={cerrar}
                        alGuardar={() => void refrescar()}
                    />
                </Suspense>
            )}
        </>
    );
}
