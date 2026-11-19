import type { ReactNode } from "react";
import { Eye } from "lucide-react";
import { nombreCampo } from "./etiquetas";
import { nombrePersona, numero, fecha, texto, etiqueta } from "../app/formatos";
import { Estado } from "./Interfaz";
import type { Fila } from "../features/Modulos/catalogo";
export function valorCelda(campo: string, fila: Fila) {
    const valor = fila[campo];
    if (campo === "nombreCompleto") return nombrePersona(fila);
    if (campo === "estado") return <Estado valor={valor} />;
    if (campo === "progreso") {
        const porcentaje =
            Math.min(
                100,
                (Number(fila.cantidadEjecutadaAcumulada) /
                    Number(fila.cantidadPlanificada)) *
                    100,
            ) || 0;
        return (
            <div className="progreso-tabla">
                <div>
                    <i style={{ width: `${porcentaje}%` }} />
                </div>
                <span>{numero(porcentaje, 0)}%</span>
            </div>
        );
    }
    if (valor === null || valor === undefined || valor === "")
        return <span className="dato-ausente">—</span>;
    if (typeof valor === "boolean") return valor ? "Sí" : "No";
    if (Array.isArray(valor))
        return (
            <span className="resumen-lista">
                {valor
                    .slice(0, 3)
                    .map((x) =>
                        typeof x === "object"
                            ? etiqueta(
                                  String(
                                      (x as Fila).nombreRol ||
                                          (x as Fila).nombreEspecialidad ||
                                          (x as Fila).nombre ||
                                          "",
                                  ),
                              )
                            : etiqueta(String(x)),
                    )
                    .join(", ")}
                {valor.length > 3 ? ` +${valor.length - 3}` : ""}
            </span>
        );
    if (/fecha|En$|EnUtc$|Utc$/.test(campo))
        return fecha(valor, /En$|Utc$/.test(campo));
    if (typeof valor === "number") return numero(valor);
    if (/^[A-Z_]+$/.test(String(valor))) return etiqueta(String(valor));
    return texto(valor);
}
export function TablaDatos({
    filas,
    columnas,
    acciones,
    ver,
    identidad,
}: {
    filas: Fila[];
    columnas: string[];
    acciones?: (fila: Fila) => ReactNode;
    ver?: (fila: Fila) => void;
    identidad: string;
}) {
    return (
        <div className="contenedor-tabla">
            <table className="tabla-datos">
                <thead>
                    <tr>
                        {columnas.map((c) => (
                            <th key={c} scope="col">
                                {nombreCampo(c)}
                            </th>
                        ))}
                        {(ver || acciones) && (
                            <th scope="col" className="columna-acciones">
                                Acciones
                            </th>
                        )}
                    </tr>
                </thead>
                <tbody>
                    {filas.map((f, i) => (
                        <tr key={`${f[identidad] ?? i}-${i}`}>
                            {columnas.map((c, n) => (
                                <td
                                    key={c}
                                    data-label={nombreCampo(c)}
                                    className={n === 0 ? "dato-principal" : ""}
                                >
                                    {n === 0 && ver ? (
                                        <button
                                            className="enlace-fila"
                                            onClick={() => ver(f)}
                                        >
                                            {valorCelda(c, f)}
                                        </button>
                                    ) : (
                                        valorCelda(c, f)
                                    )}
                                </td>
                            ))}
                            {(ver || acciones) && (
                                <td className="acciones-fila">
                                    {ver && (
                                        <button
                                            className="boton-icono"
                                            title="Ver detalle"
                                            aria-label={`Ver detalle de ${texto(f.nombre || f.codigo || f.numeroActa || f.nombreCompletoTrabajador || f[identidad])}`}
                                            onClick={() => ver(f)}
                                        >
                                            <Eye size={17} />
                                        </button>
                                    )}
                                    {acciones?.(f)}
                                </td>
                            )}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}
