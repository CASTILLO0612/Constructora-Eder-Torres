import { construirRuta } from "../api/cliente";
import { coincideBusqueda } from "../hooks/usoBusqueda";
import { leerConsulta } from "./almacenamiento";

export async function buscarConsultaGuardada(
    idUsuario: number,
    ruta: string,
    parametros: Record<string, unknown>,
) {
    const base = {
        ...parametros,
        buscar: undefined,
        pagina: 1,
        tamanoPagina: 100,
    };
    let local = await leerConsulta(
        idUsuario,
        construirRuta(ruta, base) + "#lista-completa",
    );
    if (!local) {
        for (const tamano of [
            ...new Set([Number(parametros.tamanoPagina) || 100, 100, 25, 50]),
        ]) {
            local = await leerConsulta(
                idUsuario,
                construirRuta(ruta, { ...base, tamanoPagina: tamano }),
            );
            if (local) break;
        }
    }
    if (!local || !Array.isArray(local.datos)) return undefined;
    const palabras = String(parametros.buscar || "");
    const campos =
        /^(nombre|primerNombre|segundoNombre|primerApellido|segundoApellido|codigo|cedula|descripcion|observaciones|direccion|correo|telefono|estado|tipo|motivo|justificacion|condicion|numeroActa|marca|modelo|resolucion|accion|entidad|campo)/i;
    const filas = local.datos.filter(
        (fila) =>
            fila &&
            typeof fila === "object" &&
            coincideBusqueda(
                Object.entries(fila)
                    .filter(
                        ([campo, valor]) =>
                            campos.test(campo) && typeof valor === "string",
                    )
                    .map(([, valor]) => valor)
                    .join(" "),
                palabras,
            ),
    );
    const tamano = Number(parametros.tamanoPagina) || 100;
    const inicio = (Number(parametros.pagina) - 1 || 0) * tamano;
    return { ...local, datos: filas.slice(inicio, inicio + tamano) };
}
