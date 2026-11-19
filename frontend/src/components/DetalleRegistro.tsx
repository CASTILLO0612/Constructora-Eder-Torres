import { useState } from "react";
import { Download, Printer } from "lucide-react";
import { useDatos } from "../hooks/usoDatos";
import { api } from "../api/cliente";
import {
    rutaConDatos,
    type Fila,
    type Recurso,
    type Accion,
} from "../features/Modulos/catalogo";
import { esCampoInterno, nombreCampo, nombrePermiso } from "./etiquetas";
import { texto } from "../app/formatos";
import { Modal, Boton, Cargando, ErrorVisible } from "./Interfaz";
import { valorCelda } from "./TablaDatos";
import { useSesion } from "../app/Sesion";
import { EnlacesContexto } from "./EnlacesContexto";
import { VistaPreviaEvidencia } from "./VistaPreviaEvidencia";
import type { RespuestaEvidencia } from "../api/contratos";
export default function Detalle({
    fila,
    recurso,
    cerrar,
    ejecutar,
}: {
    fila: Fila;
    recurso: Recurso;
    cerrar: () => void;
    ejecutar?: (accion: Accion, fila: Fila) => void;
}) {
    const ruta = recurso.detalle ? rutaConDatos(recurso.detalle, fila) : null;
    const consulta = useDatos<Fila>(ruta);
    const datosRegistro = consulta.datos || fila;
    const evidencias = useDatos<RespuestaEvidencia[]>(
        recurso.clave === "avances" && datosRegistro.idAvance
            ? `/api/seguimiento/avances/${datosRegistro.idAvance}/evidencias`
            : null,
        { tamanoPagina: 100 },
        true,
    );
    const datos: Fila =
        recurso.clave === "avances" && evidencias.datos
            ? { ...datosRegistro, evidencias: evidencias.datos }
            : datosRegistro;
    const { puede } = useSesion();
    const [error, setError] = useState<unknown>();
    const descargar = async (id: unknown) => {
        try {
            const archivo = await api.archivo(
                `/api/seguimiento/evidencias/${id}/archivo`,
            );
            const url = URL.createObjectURL(archivo.contenido);
            const a = document.createElement("a");
            a.href = url;
            a.download = archivo.nombre;
            a.click();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (e) {
            setError(e);
        }
    };
    return (
        <Modal
            titulo={texto(
                datos.nombre ||
                    datos.numeroActa ||
                    datos.nombreUsuario ||
                    recurso.titulo,
            )}
            descripcion="Detalle del registro"
            cerrar={cerrar}
            amplio
        >
            <div className="detalle-imprimible">
                {consulta.isLoading && <Cargando variante="detalle" />}
                {consulta.error && <ErrorVisible error={consulta.error} />}
                {!!datos.idEvidencia &&
                    datos.tipoEvidencia === "FOTOGRAFIA" && (
                        <VistaPreviaEvidencia
                            key={String(datos.idEvidencia)}
                            idEvidencia={Number(datos.idEvidencia)}
                            automatica
                        />
                    )}
                <dl className="detalle-datos">
                    {Object.entries(datos)
                        .filter(
                            ([c, v]) =>
                                !esCampoInterno(c) &&
                                !Array.isArray(v) &&
                                (typeof v !== "object" || v === null),
                        )
                        .map(([c]) => (
                            <div key={c}>
                                <dt>{nombreCampo(c)}</dt>
                                <dd>{valorCelda(c, datos)}</dd>
                            </div>
                        ))}
                </dl>
                {evidencias.isLoading && (
                    <Cargando texto="Cargando evidencias…" />
                )}
                {evidencias.error && <ErrorVisible error={evidencias.error} />}
                {Object.entries(datos)
                    .filter(([c, v]) => !esCampoInterno(c) && Array.isArray(v))
                    .map(([c, v]) => {
                        const lista = v as unknown[];
                        return (
                            <section className="detalle-seccion" key={c}>
                                <h3>
                                    {nombreCampo(c)}{" "}
                                    <span className="contador">
                                        {lista.length}
                                    </span>
                                </h3>
                                {!lista.length ? (
                                    <p className="ayuda-campo">
                                        No hay registros vinculados.
                                    </p>
                                ) : (
                                    lista.map((x, i) =>
                                        typeof x === "object" && x !== null ? (
                                            <div
                                                className="detalle-subregistro"
                                                key={i}
                                            >
                                                {Object.entries(x as Fila)
                                                    .filter(
                                                        ([n, z]) =>
                                                            !esCampoInterno(
                                                                n,
                                                            ) &&
                                                            typeof z !==
                                                                "object" &&
                                                            z !== null,
                                                    )
                                                    .map(([n]) => (
                                                        <p key={n}>
                                                            <strong>
                                                                {nombreCampo(n)}
                                                                :
                                                            </strong>{" "}
                                                            {valorCelda(
                                                                n,
                                                                x as Fila,
                                                            )}
                                                        </p>
                                                    ))}
                                                {!!(x as Fila).idEvidencia && (
                                                    <div className="acciones-evidencia">
                                                        {(x as Fila)
                                                            .tipoEvidencia ===
                                                            "FOTOGRAFIA" && (
                                                            <VistaPreviaEvidencia
                                                                idEvidencia={Number(
                                                                    (x as Fila)
                                                                        .idEvidencia,
                                                                )}
                                                            />
                                                        )}
                                                        <Boton
                                                            variante="secundario"
                                                            onClick={() =>
                                                                descargar(
                                                                    (x as Fila)
                                                                        .idEvidencia,
                                                                )
                                                            }
                                                        >
                                                            <Download
                                                                size={16}
                                                            />
                                                            Descargar evidencia
                                                        </Boton>
                                                    </div>
                                                )}
                                            </div>
                                        ) : (
                                            <p key={i}>
                                                {c === "codigosPermiso"
                                                    ? nombrePermiso(String(x))
                                                    : texto(x)}
                                            </p>
                                        ),
                                    )
                                )}
                            </section>
                        );
                    })}
            </div>
            {error !== undefined && <ErrorVisible error={error} />}
            {!consulta.isLoading && !consulta.error && (
                <>
                    <EnlacesContexto recurso={recurso} datos={datos} />
                    <div className="acciones-contexto">
                        {recurso.acciones
                            ?.filter(
                                (a) =>
                                    puede(a.permiso) &&
                                    (!a.visible || a.visible(datos)),
                            )
                            .map((a) => (
                                <Boton
                                    key={a.titulo}
                                    variante="secundario"
                                    onClick={() => ejecutar?.(a, datos)}
                                >
                                    {a.titulo}
                                </Boton>
                            ))}
                    </div>
                </>
            )}
            <div className="pie-formulario">
                <span>Información de la obra y el registro</span>
                <div>
                    {!!datos.idEvidencia && (
                        <Boton
                            variante="secundario"
                            onClick={() => descargar(datos.idEvidencia)}
                        >
                            <Download size={16} />
                            Descargar archivo
                        </Boton>
                    )}
                    {!!datos.idActa && (
                        <Boton
                            variante="secundario"
                            onClick={() => window.print()}
                        >
                            <Printer size={16} />
                            Imprimir acta
                        </Boton>
                    )}
                    <Boton onClick={cerrar}>Cerrar</Boton>
                </div>
            </div>
        </Modal>
    );
}
