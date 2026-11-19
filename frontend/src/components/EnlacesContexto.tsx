import { Link } from "react-router-dom";
import { useSesion } from "../app/Sesion";
import { permisos as p } from "../api/permisos";
import type { Fila, Recurso } from "../features/Modulos/catalogo";

export function EnlacesContexto({
    recurso,
    datos,
}: {
    recurso: Recurso;
    datos: Fila;
}) {
    const { puede } = useSesion();
    const obra = Number(datos.idObra);
    const enlaces: { nombre: string; ruta: string; permiso: string }[] = [];
    if (recurso.clave === "trabajadores")
        enlaces.push({
            nombre: "Asignar a una obra",
            ruta: `/personal?vista=asignaciones&crear=asignaciones&idTrabajador=${datos.idTrabajador}`,
            permiso: p.gestionarAsignaciones,
        });
    if (
        recurso.clave === "incidencias" &&
        obra &&
        Array.isArray(datos.idsActividadesAfectadas)
    )
        datos.idsActividadesAfectadas.forEach((id, i) =>
            enlaces.push({
                nombre: `Ver actividad afectada ${i + 1}`,
                ruta: `/seguimiento?vista=actividades&registro=${Number(id)}&idObra=${obra}`,
                permiso: p.consultarObras,
            }),
        );
    if (
        ["entregas", "reposiciones"].includes(recurso.clave) &&
        datos.idTrabajador
    )
        enlaces.push({
            nombre: "Historial de EPP del trabajador",
            ruta: `/epp?vista=historial-epp&idTrabajador=${datos.idTrabajador}`,
            permiso: p.consultarHistorialEpp,
        });
    if (recurso.clave === "obras" && obra) {
        enlaces.push(
            {
                nombre: "Personal de la obra",
                ruta: `/personal?vista=asignaciones&idObra=${obra}`,
                permiso: p.consultarTrabajadores,
            },
            {
                nombre: "Seguimiento de la obra",
                ruta: `/seguimiento?vista=actividades&idObra=${obra}`,
                permiso: p.consultarObras,
            },
            {
                nombre: "Actas de la obra",
                ruta: `/actas?idObra=${obra}`,
                permiso: p.elaborarActas,
            },
        );
    }
    if (recurso.clave === "asignaciones" && obra)
        enlaces.push({
            nombre: "Organizar cuadrillas",
            ruta: `/personal?vista=miembros&idObra=${obra}`,
            permiso: p.gestionarAsignaciones,
        });
    if (recurso.clave === "actividades" && obra)
        enlaces.push(
            {
                nombre: "Avances de la actividad",
                ruta: `/seguimiento?vista=avances&idObra=${obra}&idActividad=${datos.idActividad}`,
                permiso: p.consultarObras,
            },
            {
                nombre: "Incidencias de la obra",
                ruta: `/seguimiento?vista=incidencias&idObra=${obra}`,
                permiso: p.consultarObras,
            },
        );
    if (
        recurso.clave === "inventario" ||
        recurso.clave === "asignaciones-herramientas"
    )
        enlaces.push({
            nombre: "Historial de la herramienta",
            ruta: `/herramientas?vista=eventos&idHerramienta=${datos.idHerramienta}${obra ? "&idObra=" + obra : ""}`,
            permiso: p.registrarIncidenciasHerramientas,
        });
    if (recurso.clave === "eventos")
        enlaces.push({
            nombre: "Detalle de la asignación",
            ruta: `/herramientas?vista=asignaciones-herramientas&registro=${datos.idAsignacionHerramienta}&idObra=${obra}`,
            permiso: p.gestionarAsignacionesHerramientas,
        });
    const visibles = enlaces.filter((e) => puede(e.permiso));
    return visibles.length ? (
        <nav
            className="enlaces-contexto"
            aria-label="Continuar con este registro"
        >
            {visibles.map((e) => (
                <Link
                    className="boton boton-secundario"
                    key={e.nombre}
                    to={e.ruta}
                >
                    {e.nombre}
                </Link>
            ))}
        </nav>
    ) : null;
}
