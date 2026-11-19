import { useCallback, useId, useMemo, useState, type FormEvent } from "react";
import { Save, Plus, Trash2 } from "lucide-react";
import esquemasJson from "../api/esquemas.json";
import { catalogos } from "../api/catalogos";
import { api, ErrorApi } from "../api/cliente";
import { nombreCampo } from "./etiquetas";
import { Modal, Boton, ErrorVisible } from "./Interfaz";
import { SelectorReferencia } from "./SelectorReferencia";
import { CapturaArchivo } from "./CapturaArchivo";
import { UbicacionObra } from "./UbicacionObra";
import { useSesion } from "../app/Sesion";
import { useAviso } from "../app/Avisos";
import { etiqueta, fechaJornada, horaLocal, localAUtc } from "../app/formatos";
import { encolar, sincronizar } from "../offline/sincronizacion";
import { useQueryClient } from "@tanstack/react-query";
import { useDatos } from "../hooks/usoDatos";
import {
    estadosActividadPermitidos,
    exigeJustificacion,
    campoJustificacion,
} from "../features/Seguimiento/reglasActividad";
import {
    rutaConDatos,
    type Accion,
    type Fila,
} from "../features/Modulos/catalogo";
import {
    guardarOperacion,
    type OperacionPendiente,
} from "../offline/almacenamiento";

export type Esquema = {
    type?: string | string[];
    format?: string;
    $ref?: string;
    properties?: Record<string, Esquema>;
    required?: string[];
    items?: Esquema;
    default?: unknown;
};
export const esquemas = esquemasJson as unknown as Record<string, Esquema>;
const internos = new Set([
    "uuidRegistro",
    "uuidMovimiento",
    "esSincronizacionDiferida",
    "rutaFirma",
    "firmadoEn",
    "rutaConstancia",
    "contenidoArchivo",
    "nombreArchivo",
]);
const esNumero = (s: Esquema) =>
    s.format === "int32" ||
    s.format === "int64" ||
    s.format === "double" ||
    s.type === "number" ||
    s.type === "integer";
const esFechaHora = (s: Esquema, n: string) =>
    s.format === "date-time" ||
    (!s.type && !s.$ref && /En$|EnUtc$|Utc$/.test(n));
const permiteNulo = (s: Esquema) =>
    Array.isArray(s.type) && s.type.includes("null");
const tipos = (s: Esquema) => (Array.isArray(s.type) ? s.type : [s.type]);
function opciones(
    nombre: string,
    esquema: string,
    fila: Fila,
): readonly string[] | undefined {
    if (nombre === "decision") return catalogos.DecisionesActa;
    if (nombre === "estadoInicial")
        return esquema.includes("Obra")
            ? ["PLANIFICADA", "ACTIVA"]
            : ["PLANIFICADA"];
    if (nombre === "tipoContratacion") return catalogos.TiposContratacion;
    if (nombre === "nivel") return catalogos.NivelesEspecialidad;
    if (nombre === "tipoEvidencia") return catalogos.TiposEvidencia;
    if (nombre === "tipoIncidencia")
        return esquema.includes("Herramienta")
            ? catalogos.TiposIncidenciaHerramienta
            : catalogos.TiposIncidenciaObra;
    if (nombre === "motivo" && esquema.includes("Reposicion"))
        return catalogos.MotivosReposicionEpp;
    if (nombre.startsWith("condicion"))
        return esquema.includes("Herramienta")
            ? catalogos.CondicionesHerramienta
            : catalogos.CondicionesEpp;
    if (nombre === "estadoActividad")
        return ["EN_PROCESO", "RETRASADA", "COMPLETA"];
    if (nombre === "estado") {
        if (esquema === "SolicitudAtenderIncidenciaHerramienta")
            return fila.estado === "EN_PROCESO"
                ? ["RESUELTA"]
                : ["EN_PROCESO", "RESUELTA"];
        if (esquema.includes("EstadoObra"))
            return (
                (
                    {
                        PLANIFICADA: ["PLANIFICADA", "ACTIVA"],
                        ACTIVA: ["ACTIVA", "SUSPENDIDA", "FINALIZADA"],
                        SUSPENDIDA: ["SUSPENDIDA", "ACTIVA", "FINALIZADA"],
                        FINALIZADA: ["FINALIZADA"],
                    } as Record<string, string[]>
                )[String(fila.estado)] || catalogos.EstadosObra
            );
        if (esquema.includes("Trabajador")) return catalogos.EstadosTrabajador;
        if (esquema.includes("Usuario")) return catalogos.EstadosUsuario;
        if (esquema.includes("FinalizarAsignacion"))
            return ["FINALIZADA", "ANULADA"];
        if (esquema.includes("Avance")) return ["REGISTRADO", "BORRADOR"];
        if (esquema.includes("Incidencia")) return ["ABIERTA"];
        if (esquema.includes("Actividad"))
            return estadosActividadPermitidos(fila);
    }
    return undefined;
}
export function normalizarSolicitud(nombre: string, datos: Fila): Fila {
    const esquema = esquemas[nombre];
    const resultado: Fila = {};
    for (const [campo, s] of Object.entries(esquema.properties || {})) {
        let valor = datos[campo];
        if (valor === "" || valor === undefined || valor === null) {
            if (permiteNulo(s) || esquema.required?.includes(campo))
                resultado[campo] = null;
            continue;
        }
        if (tipos(s).includes("array")) {
            const elementos = Array.isArray(valor) ? valor : [];
            if (s.items?.$ref)
                valor = elementos.map((x) =>
                    normalizarSolicitud(
                        s.items!.$ref!.split("/").at(-1)!,
                        x as Fila,
                    ),
                );
            else if (s.items && esNumero(s.items))
                valor = elementos.map(Number);
        } else if (esFechaHora(s, campo))
            valor = String(valor).endsWith("Z")
                ? valor
                : localAUtc(String(valor));
        else if (esNumero(s))
            valor = String(valor).startsWith("pendiente:") ? 0 : Number(valor);
        resultado[campo] = valor;
    }
    return resultado;
}

function Campos({
    nombre,
    datos,
    cambiar,
    idObra,
    errores = {},
    fila = {},
    estadoActividad,
}: {
    nombre: string;
    datos: Fila;
    cambiar: (campos: Fila) => void;
    idObra?: number;
    errores?: Record<string, string[]>;
    fila?: Fila;
    estadoActividad?: string;
}) {
    const esquema = esquemas[nombre];
    const identificadorCampos = useId();
    return (
        <div className="rejilla-formulario">
            {Object.entries(esquema.properties || {})
                .filter(([campo]) => !internos.has(campo))
                .map(([campo, s]) => {
                    const opcionesCampo = opciones(campo, nombre, {
                        ...fila,
                        cantidadPlanificada:
                            datos.cantidadPlanificada ??
                            fila.cantidadPlanificada,
                    });
                    const multi = tipos(s).includes("array");
                    const obligatorio =
                        (campo === campoJustificacion(nombre) &&
                            exigeJustificacion(
                                nombre,
                                datos,
                                estadoActividad,
                            )) ||
                        (esquema.required?.includes(campo) &&
                            !permiteNulo(s)) ||
                        ([
                            "latitud",
                            "longitud",
                            "radioAsistenciaMetros",
                        ].includes(campo) &&
                            nombre.includes("Obra")) ||
                        (nombre === "SolicitudRegistrarEvidencia" &&
                            datos.tipoEvidencia === "FOTOGRAFIA" &&
                            ["capturadaEn", "latitud", "longitud"].includes(
                                campo,
                            ));
                    const fallo =
                        errores[campo] ||
                        errores[campo[0].toUpperCase() + campo.slice(1)];
                    const nombreVisible = nombreCampo(campo);
                    const actualizar = (valor: unknown) =>
                        cambiar({ [campo]: valor });
                    if (multi && s.items?.$ref) {
                        const hijo = s.items.$ref.split("/").at(-1)!;
                        const lista = Array.isArray(datos[campo])
                            ? (datos[campo] as Fila[])
                            : [];
                        return (
                            <fieldset
                                key={campo}
                                className="campo-grupo ancho-formulario"
                            >
                                <legend>{nombreVisible}</legend>
                                {lista.map((item, i) => (
                                    <div className="fila-repetida" key={i}>
                                        <Campos
                                            nombre={hijo}
                                            datos={item}
                                            idObra={idObra}
                                            cambiar={(campos) =>
                                                actualizar(
                                                    lista.map((f, n) =>
                                                        n === i
                                                            ? {
                                                                  ...f,
                                                                  ...campos,
                                                              }
                                                            : f,
                                                    ),
                                                )
                                            }
                                        />
                                        <Boton
                                            type="button"
                                            variante="texto"
                                            aria-label={`Quitar ${nombreVisible} ${i + 1}`}
                                            onClick={() =>
                                                actualizar(
                                                    lista.filter(
                                                        (_, n) => n !== i,
                                                    ),
                                                )
                                            }
                                        >
                                            <Trash2 size={16} />
                                            Quitar
                                        </Boton>
                                    </div>
                                ))}
                                <Boton
                                    type="button"
                                    variante="secundario"
                                    onClick={() => actualizar([...lista, {}])}
                                >
                                    <Plus size={16} />
                                    Añadir {nombreVisible.toLowerCase()}
                                </Boton>
                            </fieldset>
                        );
                    }
                    const referencia =
                        campo.startsWith("id") &&
                        !["uuidRegistro", "uuidMovimiento"].includes(campo);
                    const largo =
                        multi ||
                        /descripcion|observacion|justificacion|mejora|solicitudes|motivoObligatorio|motivoVariacion|codigosPermiso/i.test(
                            campo,
                        );
                    return (
                        <div
                            className={`campo ${largo ? "ancho-formulario" : ""}`}
                            key={campo}
                        >
                            {s.type !== "boolean" && (
                                <label
                                    htmlFor={`${identificadorCampos}-${campo}`}
                                >
                                    {nombreVisible}
                                    {obligatorio && (
                                        <span aria-hidden="true"> *</span>
                                    )}
                                </label>
                            )}
                            {referencia || campo === "codigosPermiso" ? (
                                <SelectorReferencia
                                    campo={campo}
                                    idObra={idObra}
                                    valor={datos[campo]}
                                    cambiar={actualizar}
                                    multiple={multi}
                                    obligatorio={obligatorio}
                                    etiquetaVisible={nombreVisible}
                                    idControl={`${identificadorCampos}-${campo}`}
                                />
                            ) : s.type === "boolean" ? (
                                <label className="opcion-check">
                                    <input
                                        id={`${identificadorCampos}-${campo}`}
                                        type="checkbox"
                                        checked={Boolean(datos[campo])}
                                        onChange={(e) =>
                                            actualizar(e.target.checked)
                                        }
                                    />
                                    <span>{nombreVisible}</span>
                                </label>
                            ) : opcionesCampo ? (
                                <select
                                    id={`${identificadorCampos}-${campo}`}
                                    required={obligatorio}
                                    value={String(datos[campo] ?? "")}
                                    onChange={(e) => actualizar(e.target.value)}
                                >
                                    <option value="">Seleccionar…</option>
                                    {opcionesCampo.map((o) => (
                                        <option key={o} value={o}>
                                            {etiqueta(o)}
                                        </option>
                                    ))}
                                </select>
                            ) : largo ? (
                                <textarea
                                    id={`${identificadorCampos}-${campo}`}
                                    value={String(datos[campo] ?? "")}
                                    onChange={(e) => actualizar(e.target.value)}
                                    required={obligatorio}
                                    rows={3}
                                />
                            ) : (
                                <input
                                    id={`${identificadorCampos}-${campo}`}
                                    value={String(datos[campo] ?? "")}
                                    onChange={(e) => actualizar(e.target.value)}
                                    required={obligatorio}
                                    type={
                                        s.format === "date"
                                            ? "date"
                                            : esFechaHora(s, campo)
                                              ? "datetime-local"
                                              : esNumero(s)
                                                ? "number"
                                                : /contrasena/i.test(campo)
                                                  ? "password"
                                                  : campo === "correo"
                                                    ? "email"
                                                    : campo === "telefono"
                                                      ? "tel"
                                                      : "text"
                                    }
                                    step={
                                        esFechaHora(s, campo)
                                            ? "1"
                                            : esNumero(s)
                                              ? s.format === "double"
                                                  ? "any"
                                                  : "1"
                                              : undefined
                                    }
                                    autoComplete={
                                        /contrasena/i.test(campo)
                                            ? "new-password"
                                            : undefined
                                    }
                                    aria-invalid={!!fallo}
                                    aria-describedby={
                                        fallo
                                            ? `${identificadorCampos}-error-${campo}`
                                            : undefined
                                    }
                                />
                            )}
                            {fallo && (
                                <span
                                    id={`${identificadorCampos}-error-${campo}`}
                                    className="error-campo"
                                >
                                    {fallo.join(" ")}
                                </span>
                            )}
                            {campo === "radioAsistenciaMetros" && (
                                <small className="ayuda-campo">
                                    La API verifica distancia más precisión GPS
                                    dentro de este radio.
                                </small>
                            )}
                        </div>
                    );
                })}
        </div>
    );
}

export function FormularioOperacion({
    accion,
    fila = {},
    idObra,
    cerrar,
    alGuardar,
    operacionExistente,
}: {
    accion: Accion;
    fila?: Fila;
    idObra?: number;
    cerrar: () => void;
    alGuardar: (registro?: Fila) => void;
    operacionExistente?: OperacionPendiente;
}) {
    const { perfil } = useSesion();
    const aviso = useAviso();
    const consultas = useQueryClient();
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const base = useMemo(() => {
        const propiedades = esquemas[accion.esquema].properties || {};
        const inicial: Fila = {};
        for (const [n, s] of Object.entries(propiedades)) {
            const origen = fila[n] ?? accion.valores?.[n];
            if (origen !== undefined && origen !== null)
                inicial[n] = esFechaHora(s, n)
                    ? horaLocal(new Date(String(origen)))
                    : origen;
            else if (s.default !== undefined) inicial[n] = s.default;
            else if (s.type === "boolean") inicial[n] = false;
            else if (s.format === "date" && !/Fin|Vencimiento/.test(n))
                inicial[n] = fechaJornada();
            else if (
                esFechaHora(s, n) &&
                [
                    "entregadoEn",
                    "entregadaEn",
                    "devueltoEn",
                    "fechaHora",
                    "inicioEn",
                    "solicitadaEn",
                    "ejecutadaEn",
                ].includes(n)
            )
                inicial[n] = horaLocal();
        }
        if (propiedades.idObra) inicial.idObra = fila.idObra || idObra || "";
        if (propiedades.idEntregaOrigen)
            inicial.idEntregaOrigen = fila.idEntregaEpp || "";
        if (propiedades.uuidRegistro) {
            const confirmaBorrador =
                accion.esquema === "SolicitudRegistrarAvance" &&
                fila.estado === "BORRADOR" &&
                accion.valores?.estado === "REGISTRADO";
            inicial.uuidRegistro =
                (operacionExistente || confirmaBorrador) && fila.uuidRegistro
                    ? fila.uuidRegistro
                    : crypto.randomUUID();
        }
        if (propiedades.uuidMovimiento)
            inicial.uuidMovimiento = crypto.randomUUID();
        if (propiedades.estadoInicial) inicial.estadoInicial = "PLANIFICADA";
        if (propiedades.tipoEvidencia) inicial.tipoEvidencia = "FOTOGRAFIA";
        if (accion.esquema === "SolicitudActualizarActa") {
            inicial.idsAvances = (
                (fila.avancesConsolidados as Fila[]) || []
            ).map((a) => a.idAvance);
            inicial.idsIncidencias = (
                (fila.incidenciasConsolidadas as Fila[]) || []
            ).map((i) => i.idIncidencia);
        }
        if (operacionExistente?.dependencia)
            inicial[operacionExistente.dependencia.campo] =
                `pendiente:${operacionExistente.dependencia.idLocal}`;
        return { ...inicial, ...accion.valores };
    }, [accion, fila, idObra]);
    const [datos, setDatos] = useState<Fila>(base);
    const [obraContexto, setObraContexto] = useState(
        idObra || Number(fila.idObra) || undefined,
    );
    const contextoObra = Number(datos.idObra) || obraContexto;
    const actividad = useDatos<Fila>(
        accion.esquema === "SolicitudRegistrarAvance" &&
            Number(datos.idActividad) > 0
            ? `/api/seguimiento/actividades/${Number(datos.idActividad)}`
            : null,
    );
    const estadoActividad = String(
        actividad.datos?.estado || fila.estado || "",
    );
    const cambiar = useCallback(
        (campos: Fila) =>
            setDatos((d) => ({
                ...d,
                ...("idObra" in campos &&
                String(campos.idObra) !== String(d.idObra)
                    ? {
                          idAsignacion: "",
                          idAsignacionResponsable: "",
                          idActividad: "",
                          idAvance: "",
                          idCuadrillaObra: "",
                          idIncidenciaVinculada: "",
                          idsActividadesAfectadas: [],
                          idsAvances: [],
                          idsIncidencias: [],
                          participantes: [],
                      }
                    : {}),
                ...campos,
            })),
        [],
    );
    const requiereContexto = Object.keys(
        esquemas[accion.esquema].properties || {},
    ).some((n) =>
        [
            "idAsignacion",
            "idAsignacionTrabajadorObra",
            "idAsignacionResponsable",
            "idActividad",
            "idAvance",
            "idIncidenciaVinculada",
            "idCuadrillaObra",
            "idAsistencia",
        ].includes(n),
    );
    const enviar = async (e: FormEvent) => {
        e.preventDefault();
        setError(undefined);
        setOcupado(true);
        try {
            if (
                exigeJustificacion(accion.esquema, datos, estadoActividad) &&
                !String(datos[campoJustificacion(accion.esquema)] || "").trim()
            )
                throw new Error(
                    "Registra el motivo obligatorio del trabajo incompleto o retrasado antes de guardar.",
                );
            let guardado: Fila | undefined;
            const cuerpo = normalizarSolicitud(accion.esquema, datos);
            if (
                accion.esquema === "SolicitudRegistrarEvidencia" &&
                !cuerpo.contenidoArchivo
            )
                throw new Error(
                    "Selecciona o captura un archivo de evidencia.",
                );
            const dependencia = String(datos.idAvance || "").startsWith(
                "pendiente:",
            )
                ? {
                      idLocal: String(datos.idAvance).slice(10),
                      campo: "idAvance",
                  }
                : undefined;
            if (operacionExistente) {
                await guardarOperacion({
                    ...operacionExistente,
                    datos: {
                        ...operacionExistente.datos,
                        ...cuerpo,
                        uuidRegistro: operacionExistente.datos.uuidRegistro,
                    },
                    estado: "PENDIENTE",
                    proximoIntento: 0,
                    mensaje: undefined,
                    dependencia,
                });
                aviso(
                    "La captura se actualizó conservando su identidad original.",
                );
                void sincronizar(perfil!.idUsuario).catch(() => undefined);
            } else if (accion.coleccion) {
                if (accion.coleccion === "devolucionesEpp")
                    cuerpo.idEntregaEpp = fila.idEntregaEpp;
                await encolar(
                    perfil!.idUsuario,
                    accion.coleccion,
                    accion.titulo,
                    cuerpo,
                    accion.esquema,
                    dependencia,
                    accion.coleccion === "avances" && contextoObra
                        ? `/api/seguimiento/obras/${contextoObra}/avances`
                        : undefined,
                );
                aviso(
                    "Captura guardada en este dispositivo. Se confirmará al sincronizar.",
                );
                void sincronizar(perfil!.idUsuario).catch(() => undefined);
            } else {
                if (!navigator.onLine)
                    throw new Error(
                        "Esta operación requiere conexión. Conserva el formulario y vuelve a intentarlo al conectar.",
                    );
                const ruta = rutaConDatos(accion.ruta, { ...fila, ...datos });
                if (!ruta)
                    throw new Error(
                        "No se encontró el registro sobre el que realizar la operación.",
                    );
                guardado = await api.escribir<Fila>(
                    accion.metodo,
                    ruta,
                    cuerpo,
                );
                aviso("Los cambios se guardaron correctamente.");
            }
            void consultas.invalidateQueries({ queryKey: ["datos"] });
            alGuardar(guardado);
            cerrar();
        } catch (err) {
            setError(err);
        } finally {
            setOcupado(false);
        }
    };
    return (
        <Modal
            titulo={accion.titulo}
            descripcion={
                accion.coleccion
                    ? "La captura se conservará hasta recibir confirmación del servidor."
                    : "Completa los campos de la operación."
            }
            cerrar={cerrar}
            amplio
        >
            <form onSubmit={enviar}>
                {error !== undefined && <ErrorVisible error={error} />}
                {requiereContexto &&
                    !(
                        "idObra" in (esquemas[accion.esquema].properties || {})
                    ) && (
                        <div className="contexto-formulario">
                            <label>Obra para consultar las opciones</label>
                            <SelectorReferencia
                                campo="idObra"
                                valor={obraContexto}
                                cambiar={(v) => {
                                    setObraContexto(Number(v) || undefined);
                                    setDatos((d) => ({
                                        ...d,
                                        idAsignacion: "",
                                        idActividad: "",
                                        idAvance: "",
                                    }));
                                }}
                                etiquetaVisible="Obra"
                            />
                        </div>
                    )}
                {accion.esquema === "SolicitudRegistrarEvidencia" && (
                    <CapturaArchivo datos={datos} cambiar={cambiar} />
                )}
                {["SolicitudCrearObra", "SolicitudActualizarObra"].includes(
                    accion.esquema,
                ) && <UbicacionObra cambiar={cambiar} />}
                <Campos
                    nombre={accion.esquema}
                    datos={datos}
                    cambiar={cambiar}
                    idObra={contextoObra}
                    errores={
                        error instanceof ErrorApi ? error.problema.errors : {}
                    }
                    fila={fila}
                    estadoActividad={estadoActividad}
                />
                <div className="pie-formulario">
                    <span>* Campos obligatorios</span>
                    <div>
                        <Boton
                            type="button"
                            variante="secundario"
                            onClick={cerrar}
                            disabled={ocupado}
                        >
                            Cancelar
                        </Boton>
                        <Boton type="submit" ocupado={ocupado}>
                            <Save size={17} />
                            {accion.coleccion
                                ? "Guardar captura"
                                : "Guardar cambios"}
                        </Boton>
                    </div>
                </div>
            </form>
        </Modal>
    );
}
