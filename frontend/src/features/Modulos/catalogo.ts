import { permisos as p } from "../../api/permisos";
import type { Coleccion } from "../../offline/almacenamiento";

export type Fila = Record<string, unknown>;
export type Accion = {
    titulo: string;
    metodo: "POST" | "PUT" | "PATCH";
    ruta: string;
    esquema: string;
    permiso: string;
    coleccion?: Coleccion;
    valores?: Fila;
    visible?: (fila: Fila) => boolean;
    requiereDetalle?: boolean;
};
export type Recurso = {
    clave: string;
    titulo: string;
    descripcion: string;
    ruta: string;
    identidad: string;
    permisos: string[];
    columnas: string[];
    crear?: Accion;
    acciones?: Accion[];
    detalle?: string;
    filtros?: string[];
    requiereObra?: boolean;
};
export type Modulo = {
    titulo: string;
    descripcion: string;
    ceja: string;
    recursos: Recurso[];
};
const accion = (
    titulo: string,
    metodo: Accion["metodo"],
    ruta: string,
    esquema: string,
    permiso: string,
    coleccion?: Coleccion,
    valores?: Fila,
    visible?: Accion["visible"],
): Accion => ({
    titulo,
    metodo,
    ruta,
    esquema,
    permiso,
    coleccion,
    valores,
    visible,
});
const activa = (f: Fila) => f.estado === "ACTIVA";
const obra = "/api/obras";
const personal = "/api/personal";
const seguimiento = "/api/seguimiento";
const epp = "/api/epp";
const herramientas = "/api/herramientas";

const trabajadores: Recurso = {
    clave: "trabajadores",
    titulo: "Trabajadores",
    descripcion: "El equipo que hace posible cada obra.",
    ruta: `${personal}/trabajadores`,
    identidad: "idTrabajador",
    permisos: [p.consultarTrabajadores, p.gestionarTrabajadores],
    columnas: ["nombreCompleto", "cedula", "nombreCargo", "telefono", "estado"],
    detalle: `${personal}/trabajadores/{idTrabajador}`,
    filtros: ["estado"],
    crear: accion(
        "Registrar trabajador",
        "POST",
        `${personal}/trabajadores`,
        "SolicitudCrearTrabajador",
        p.gestionarTrabajadores,
    ),
    acciones: [
        accion(
            "Editar trabajador",
            "PUT",
            `${personal}/trabajadores/{idTrabajador}`,
            "SolicitudActualizarTrabajador",
            p.gestionarTrabajadores,
        ),
        accion(
            "Cambiar estado",
            "PATCH",
            `${personal}/trabajadores/{idTrabajador}/estado`,
            "SolicitudCambiarEstadoTrabajador",
            p.gestionarTrabajadores,
        ),
    ],
};
const asignaciones: Recurso = {
    clave: "asignaciones",
    titulo: "Asignaciones a obra",
    descripcion: "Personal, responsabilidad y períodos de asignación.",
    ruta: `${personal}/obras/{idObra}/asignaciones`,
    identidad: "idAsignacion",
    requiereObra: true,
    permisos: [p.consultarTrabajadores, p.gestionarAsignaciones],
    columnas: [
        "nombreCompletoTrabajador",
        "tipoContratacion",
        "nombreEspecialidad",
        "fechaInicio",
        "fechaFin",
        "estado",
    ],
    detalle: `${personal}/asignaciones-obra/{idAsignacion}`,
    crear: accion(
        "Asignar trabajador",
        "POST",
        `${personal}/asignaciones-obra`,
        "SolicitudAsignarTrabajadorObra",
        p.gestionarAsignaciones,
    ),
    acciones: [
        accion(
            "Finalizar asignación",
            "PATCH",
            `${personal}/asignaciones-obra/{idAsignacion}/finalizacion`,
            "SolicitudFinalizarAsignacionObra",
            p.gestionarAsignaciones,
            undefined,
            { estado: "FINALIZADA" },
            activa,
        ),
    ],
};
const actividades: Recurso = {
    clave: "actividades",
    titulo: "Actividades",
    descripcion: "Planificación y avance físico de la obra seleccionada.",
    ruta: `${seguimiento}/obras/{idObra}/actividades`,
    identidad: "idActividad",
    requiereObra: true,
    permisos: [p.gestionarActividades, p.registrarAvances, p.elaborarActas],
    columnas: [
        "codigo",
        "nombre",
        "unidadMedida",
        "progreso",
        "fechaFinPlan",
        "estado",
    ],
    detalle: `${seguimiento}/actividades/{idActividad}`,
    crear: accion(
        "Nueva actividad",
        "POST",
        `${seguimiento}/actividades`,
        "SolicitudCrearActividad",
        p.gestionarActividades,
    ),
    acciones: [
        accion(
            "Editar actividad",
            "PUT",
            `${seguimiento}/actividades/{idActividad}`,
            "SolicitudActualizarActividad",
            p.gestionarActividades,
        ),
        accion(
            "Registrar avance",
            "POST",
            `${seguimiento}/avances`,
            "SolicitudRegistrarAvance",
            p.registrarAvances,
            "avances",
        ),
        accion(
            "Justificar retraso",
            "POST",
            `${seguimiento}/avances/justificar-retraso`,
            "SolicitudJustificarActividad",
            p.registrarAvances,
        ),
    ],
};
const avances: Recurso = {
    clave: "avances",
    titulo: "Avances",
    descripcion: "Cantidades registradas y evidencias del trabajo realizado.",
    ruta: `${seguimiento}/obras/{idObra}/avances`,
    identidad: "idAvance",
    requiereObra: true,
    permisos: [p.registrarAvances, p.registrarEvidencias, p.elaborarActas],
    columnas: [
        "nombreActividad",
        "fechaAvance",
        "cantidadEjecutada",
        "observaciones",
        "estado",
    ],
    detalle: `${seguimiento}/avances/{idAvance}`,
    filtros: ["fechaDesde", "fechaHasta"],
    crear: accion(
        "Registrar avance",
        "POST",
        `${seguimiento}/avances`,
        "SolicitudRegistrarAvance",
        p.registrarAvances,
        "avances",
    ),
    acciones: [
        accion(
            "Confirmar borrador",
            "POST",
            `${seguimiento}/avances`,
            "SolicitudRegistrarAvance",
            p.registrarAvances,
            "avances",
            { estado: "REGISTRADO" },
            (f) => f.estado === "BORRADOR",
        ),
        accion(
            "Añadir evidencia",
            "POST",
            `${seguimiento}/evidencias`,
            "SolicitudRegistrarEvidencia",
            p.registrarEvidencias,
            "evidencias",
        ),
    ],
};
const incidencias: Recurso = {
    clave: "incidencias",
    titulo: "Incidencias",
    descripcion: "Situaciones que afectan las actividades o el cronograma.",
    ruta: `${seguimiento}/obras/{idObra}/incidencias`,
    identidad: "idIncidencia",
    requiereObra: true,
    permisos: [p.registrarIncidenciasObra, p.registrarAvances, p.elaborarActas],
    columnas: [
        "tipoIncidencia",
        "descripcion",
        "inicioEn",
        "horasPerdidas",
        "estado",
    ],
    detalle: `${seguimiento}/incidencias/{idIncidencia}`,
    crear: accion(
        "Registrar incidencia",
        "POST",
        `${seguimiento}/incidencias`,
        "SolicitudRegistrarIncidenciaObra",
        p.registrarIncidenciasObra,
        "incidenciasObra",
    ),
};
const actas: Recurso = {
    clave: "actas",
    titulo: "Actas semanales",
    descripcion: "Actividades, avances, incidencias y acuerdos del período.",
    ruta: `${seguimiento}/obras/{idObra}/actas`,
    identidad: "idActa",
    requiereObra: true,
    permisos: [p.elaborarActas, p.aprobarActas],
    columnas: [
        "numeroActa",
        "periodoInicio",
        "periodoFin",
        "creadoEn",
        "estado",
    ],
    detalle: `${seguimiento}/actas/{idActa}`,
    crear: accion(
        "Elaborar acta",
        "POST",
        `${seguimiento}/actas`,
        "SolicitudElaborarActa",
        p.elaborarActas,
    ),
    acciones: [
        {
            ...accion(
                "Editar acta",
                "PUT",
                `${seguimiento}/actas/{idActa}`,
                "SolicitudActualizarActa",
                p.elaborarActas,
                undefined,
                undefined,
                (f) => ["BORRADOR", "RECHAZADA"].includes(String(f.estado)),
            ),
            requiereDetalle: true,
        },
        accion(
            "Enviar a revisión",
            "POST",
            `${seguimiento}/actas/{idActa}/enviar-revision`,
            "SolicitudEnviarActa",
            p.elaborarActas,
            undefined,
            undefined,
            (f) => f.estado === "BORRADOR",
        ),
        accion(
            "Resolver acta",
            "PATCH",
            `${seguimiento}/actas/{idActa}/resolucion`,
            "SolicitudResolverActa",
            p.aprobarActas,
            undefined,
            undefined,
            (f) => f.estado === "PENDIENTE",
        ),
    ],
};
const consolidado: Recurso = {
    clave: "consolidado",
    titulo: "Consolidado de asistencia",
    descripcion: "Jornadas y horas registradas; no incluye cálculo salarial.",
    ruta: "/api/asistencias/consolidado",
    identidad: "idTrabajador",
    permisos: [p.consultarConsolidadoAsistencia],
    columnas: [
        "nombreCompletoTrabajador",
        "nombreObra",
        "totalJornadasRegistradas",
        "jornadasCompletas",
        "jornadasAbiertas",
        "horasTrabajadasTotales",
    ],
    filtros: [
        "idObra",
        "idTrabajador",
        "idCuadrilla",
        "fechaDesde",
        "fechaHasta",
    ],
};
const correcciones: Recurso = {
    clave: "correcciones",
    titulo: "Correcciones",
    descripcion:
        "Solicitudes y decisiones con trazabilidad de los valores anteriores.",
    ruta: "/api/asistencias/correcciones",
    identidad: "idCorreccion",
    permisos: [p.corregirAsistencia, p.aprobarCorreccionAsistencia],
    columnas: [
        "idAsistencia",
        "motivo",
        "entradaNueva",
        "salidaNueva",
        "estado",
    ],
    filtros: ["idObra", "estado"],
    detalle: "/api/asistencias/correcciones/{idCorreccion}",
    crear: accion(
        "Solicitar corrección",
        "POST",
        "/api/asistencias/correcciones",
        "SolicitudCrearCorreccionAsistencia",
        p.corregirAsistencia,
    ),
    acciones: [
        accion(
            "Resolver corrección",
            "PATCH",
            "/api/asistencias/correcciones/{idCorreccion}/resolucion",
            "SolicitudResolverCorreccionAsistencia",
            p.aprobarCorreccionAsistencia,
            undefined,
            undefined,
            (f) => f.estado === "PENDIENTE",
        ),
    ],
};
const entregas: Recurso = {
    clave: "entregas",
    titulo: "Entregas de EPP",
    descripcion: "Equipo entregado a cada trabajador en su obra.",
    ruta: `${epp}/entregas`,
    identidad: "idEntregaEpp",
    permisos: [
        p.registrarEntregasEpp,
        p.consultarHistorialEpp,
        p.gestionarReposicionesEpp,
    ],
    columnas: [
        "nombreCompletoTrabajador",
        "nombreTipoEpp",
        "codigoInternoEpp",
        "entregadoEn",
        "estado",
    ],
    filtros: ["idObra", "idTrabajador", "estado"],
    detalle: `${epp}/entregas/{idEntregaEpp}`,
    crear: accion(
        "Registrar entrega",
        "POST",
        `${epp}/entregas`,
        "SolicitudRegistrarEntregaEpp",
        p.registrarEntregasEpp,
        "entregasEpp",
    ),
    acciones: [
        accion(
            "Registrar devolución",
            "PATCH",
            `${epp}/entregas/{idEntregaEpp}/devolucion`,
            "SolicitudDevolverEpp",
            p.registrarEntregasEpp,
            "devolucionesEpp",
            undefined,
            activa,
        ),
        accion(
            "Solicitar reposición",
            "POST",
            `${epp}/reposiciones`,
            "SolicitudRegistrarReposicionEpp",
            p.gestionarReposicionesEpp,
            "reposicionesEpp",
            undefined,
            activa,
        ),
    ],
};
const inventarioEpp: Recurso = {
    clave: "existencias",
    titulo: "Existencias",
    descripcion: "Piezas físicas de protección y su disponibilidad.",
    ruta: `${epp}/existencias`,
    identidad: "idEpp",
    permisos: [p.gestionarInventarioEpp],
    columnas: [
        "codigoInterno",
        "nombreTipoEpp",
        "numeroSerie",
        "condicion",
        "estado",
    ],
    filtros: ["idTipoEpp", "estado"],
    detalle: `${epp}/existencias/{idEpp}`,
    crear: accion(
        "Registrar entrada de EPP",
        "POST",
        `${epp}/entradas`,
        "SolicitudRegistrarEntradaEpp",
        p.gestionarInventarioEpp,
    ),
    acciones: [
        accion(
            "Dictaminar revisión",
            "POST",
            `${epp}/existencias/{idEpp}/revision`,
            "SolicitudRevisarEpp",
            p.gestionarInventarioEpp,
            undefined,
            undefined,
            (f) => f.estado === "EN_REVISION",
        ),
        accion(
            "Entregar equipo",
            "POST",
            `${epp}/entregas`,
            "SolicitudRegistrarEntregaEpp",
            p.registrarEntregasEpp,
            "entregasEpp",
            undefined,
            (f) => f.estado === "DISPONIBLE",
        ),
    ],
};
const reposiciones: Recurso = {
    clave: "reposiciones",
    titulo: "Reposiciones",
    descripcion: "Reemplazos por daño, pérdida, desgaste o vencimiento.",
    ruta: `${epp}/reposiciones`,
    identidad: "idReposicionEpp",
    permisos: [p.gestionarReposicionesEpp],
    columnas: [
        "idEntregaOrigen",
        "motivo",
        "descripcion",
        "equipoDevuelto",
        "estado",
    ],
    filtros: ["idObra", "estado"],
    detalle: `${epp}/reposiciones/{idReposicionEpp}`,
    crear: accion(
        "Solicitar reposición",
        "POST",
        `${epp}/reposiciones`,
        "SolicitudRegistrarReposicionEpp",
        p.gestionarReposicionesEpp,
        "reposicionesEpp",
    ),
    acciones: [
        accion(
            "Ejecutar reposición",
            "POST",
            `${epp}/reposiciones/{idReposicionEpp}/ejecutar`,
            "SolicitudEjecutarReposicionEpp",
            p.gestionarReposicionesEpp,
            undefined,
            undefined,
            (f) => f.estado === "PENDIENTE",
        ),
    ],
};
const tiposEpp: Recurso = {
    clave: "tipos-epp",
    titulo: "Tipos de EPP",
    descripcion: "Catálogo y existencia mínima por tipo de protección.",
    ruta: `${epp}/tipos`,
    identidad: "idTipoEpp",
    permisos: [p.gestionarInventarioEpp],
    columnas: [
        "nombre",
        "descripcion",
        "vidaUtilDias",
        "existenciaMinima",
        "activo",
    ],
    crear: accion(
        "Crear tipo de EPP",
        "POST",
        `${epp}/tipos`,
        "SolicitudCrearTipoEpp",
        p.gestionarInventarioEpp,
    ),
};
const inventarioHerramientas: Recurso = {
    clave: "inventario",
    titulo: "Inventario",
    descripcion: "Herramientas físicas y disponibilidad para asignación.",
    ruta: herramientas,
    identidad: "idHerramienta",
    permisos: [p.gestionarInventarioHerramientas],
    columnas: [
        "codigoInterno",
        "nombreTipoHerramienta",
        "marca",
        "modelo",
        "condicion",
        "estado",
    ],
    filtros: ["estado", "idTipoHerramienta"],
    detalle: `${herramientas}/{idHerramienta}`,
    crear: accion(
        "Registrar herramienta",
        "POST",
        herramientas,
        "SolicitudRegistrarHerramienta",
        p.gestionarInventarioHerramientas,
    ),
    acciones: [
        accion(
            "Editar herramienta",
            "PUT",
            `${herramientas}/{idHerramienta}`,
            "SolicitudActualizarHerramienta",
            p.gestionarInventarioHerramientas,
        ),
        accion(
            "Dictaminar revisión",
            "POST",
            `${herramientas}/{idHerramienta}/revision`,
            "SolicitudRevisarHerramienta",
            p.gestionarInventarioHerramientas,
            undefined,
            undefined,
            (f) => f.estado === "EN_REVISION",
        ),
        accion(
            "Asignar herramienta",
            "POST",
            `${herramientas}/asignaciones`,
            "SolicitudAsignarHerramienta",
            p.gestionarAsignacionesHerramientas,
            "asignacionesHerramienta",
            undefined,
            (f) => f.estado === "DISPONIBLE",
        ),
    ],
};
const asignacionesHerramientas: Recurso = {
    clave: "asignaciones-herramientas",
    titulo: "Asignaciones",
    descripcion: "Entregas de herramientas y responsables en cada obra.",
    ruta: `${herramientas}/asignaciones`,
    identidad: "idAsignacionHerramienta",
    permisos: [p.gestionarAsignacionesHerramientas],
    columnas: [
        "codigoInternoHerramienta",
        "nombreCompletoTrabajador",
        "codigoObra",
        "entregadaEn",
        "estado",
    ],
    filtros: ["idObra", "estado"],
    detalle: `${herramientas}/asignaciones/{idAsignacionHerramienta}`,
    crear: accion(
        "Asignar herramienta",
        "POST",
        `${herramientas}/asignaciones`,
        "SolicitudAsignarHerramienta",
        p.gestionarAsignacionesHerramientas,
        "asignacionesHerramienta",
    ),
    acciones: [
        accion(
            "Devolución o incidencia",
            "POST",
            `${herramientas}/eventos`,
            "SolicitudRegistrarEventoHerramienta",
            p.registrarIncidenciasHerramientas,
            "eventosHerramienta",
            undefined,
            activa,
        ),
    ],
};
const eventosHerramientas: Recurso = {
    clave: "eventos",
    titulo: "Eventos e incidencias",
    descripcion: "Historial de devoluciones, daños y pérdidas.",
    ruta: `${herramientas}/eventos`,
    identidad: "idIncidenciaHerramienta",
    permisos: [
        p.registrarIncidenciasHerramientas,
        p.gestionarAsignacionesHerramientas,
    ],
    columnas: [
        "idAsignacionHerramienta",
        "tipoIncidencia",
        "fechaHora",
        "descripcion",
        "estado",
    ],
    filtros: ["idObra", "idHerramienta", "idAsignacionHerramienta"],
    detalle: `${herramientas}/eventos/{idIncidenciaHerramienta}`,
    crear: accion(
        "Registrar evento",
        "POST",
        `${herramientas}/eventos`,
        "SolicitudRegistrarEventoHerramienta",
        p.registrarIncidenciasHerramientas,
        "eventosHerramienta",
    ),
    acciones: [
        accion(
            "Atender incidencia",
            "PATCH",
            `${herramientas}/eventos/{idIncidenciaHerramienta}/atencion`,
            "SolicitudAtenderIncidenciaHerramienta",
            p.registrarIncidenciasHerramientas,
            undefined,
            undefined,
            (f) => ["ABIERTA", "EN_PROCESO"].includes(String(f.estado)),
        ),
    ],
};

export const modulos: Record<string, Modulo> = {
    obras: {
        titulo: "Obras",
        ceja: "PORTAFOLIO DE PROYECTOS",
        descripcion:
            "Una visión clara de tus proyectos, desde la planificación hasta la entrega.",
        recursos: [
            {
                clave: "obras",
                titulo: "Todas las obras",
                descripcion: "Proyectos y geocercas registradas.",
                ruta: obra,
                identidad: "idObra",
                permisos: [p.consultarObras, p.gestionarObras],
                columnas: [
                    "codigo",
                    "nombre",
                    "direccion",
                    "fechaFinPlan",
                    "estado",
                ],
                filtros: ["estado"],
                detalle: `${obra}/{idObra}`,
                crear: accion(
                    "Nueva obra",
                    "POST",
                    obra,
                    "SolicitudCrearObra",
                    p.gestionarObras,
                ),
                acciones: [
                    accion(
                        "Editar obra",
                        "PUT",
                        `${obra}/{idObra}`,
                        "SolicitudActualizarObra",
                        p.gestionarObras,
                    ),
                    accion(
                        "Cambiar estado",
                        "PATCH",
                        `${obra}/{idObra}/estado`,
                        "SolicitudCambiarEstadoObra",
                        p.gestionarObras,
                        undefined,
                        undefined,
                        (f) => f.estado !== "FINALIZADA",
                    ),
                ],
            },
        ],
    },
    personal: {
        titulo: "Personal",
        ceja: "EQUIPO Y ORGANIZACIÓN",
        descripcion: "Personas, especialidades y asignaciones en cada obra.",
        recursos: [
            trabajadores,
            asignaciones,
            {
                clave: "cuadrillas",
                titulo: "Cuadrillas",
                descripcion: "Organización de los equipos de trabajo.",
                ruta: `${personal}/cuadrillas`,
                identidad: "idCuadrilla",
                permisos: [p.consultarTrabajadores, p.gestionarAsignaciones],
                columnas: ["codigo", "nombre", "descripcion", "activo"],
                crear: accion(
                    "Crear cuadrilla",
                    "POST",
                    `${personal}/cuadrillas`,
                    "SolicitudCuadrilla",
                    p.gestionarAsignaciones,
                ),
            },
            {
                clave: "cuadrillas-obra",
                titulo: "Cuadrillas por obra",
                descripcion: "Cuadrillas y períodos en la obra seleccionada.",
                ruta: `${personal}/obras/{idObra}/cuadrillas`,
                identidad: "idCuadrillaObra",
                requiereObra: true,
                permisos: [p.consultarTrabajadores, p.gestionarAsignaciones],
                columnas: [
                    "codigoCuadrilla",
                    "nombreCuadrilla",
                    "fechaInicio",
                    "fechaFin",
                    "estado",
                ],
                crear: accion(
                    "Asignar cuadrilla",
                    "POST",
                    `${personal}/cuadrillas-obra`,
                    "SolicitudAsignarCuadrillaObra",
                    p.gestionarAsignaciones,
                ),
            },
            {
                clave: "miembros",
                titulo: "Miembros de cuadrilla",
                descripcion:
                    "Integrantes asignados a cada cuadrilla de la obra.",
                ruta: `${personal}/obras/{idObra}/miembros-cuadrilla`,
                identidad: "idTrabajadorCuadrilla",
                requiereObra: true,
                permisos: [p.consultarTrabajadores, p.gestionarAsignaciones],
                columnas: [
                    "nombreCompletoTrabajador",
                    "nombreCuadrilla",
                    "fechaInicio",
                    "fechaFin",
                ],
                crear: accion(
                    "Añadir integrante",
                    "POST",
                    `${personal}/trabajadores-cuadrilla`,
                    "SolicitudAsignarTrabajadorCuadrilla",
                    p.gestionarAsignaciones,
                ),
            },
            {
                clave: "cargos",
                titulo: "Cargos",
                descripcion: "Catálogo de cargos del personal.",
                ruta: `${personal}/cargos`,
                identidad: "idCargo",
                permisos: [p.consultarTrabajadores, p.gestionarTrabajadores],
                columnas: ["nombre", "descripcion", "activo"],
                crear: accion(
                    "Crear cargo",
                    "POST",
                    `${personal}/cargos`,
                    "SolicitudCargo",
                    p.gestionarTrabajadores,
                ),
            },
            {
                clave: "especialidades",
                titulo: "Especialidades",
                descripcion: "Competencias y especialidades del equipo.",
                ruta: `${personal}/especialidades`,
                identidad: "idEspecialidad",
                permisos: [p.consultarTrabajadores, p.gestionarTrabajadores],
                columnas: ["nombre", "descripcion", "activo"],
                crear: accion(
                    "Crear especialidad",
                    "POST",
                    `${personal}/especialidades`,
                    "SolicitudEspecialidad",
                    p.gestionarTrabajadores,
                ),
            },
        ],
    },
    seguimiento: {
        titulo: "Seguimiento de obra",
        ceja: "EJECUCIÓN Y CONTROL",
        descripcion: "Actividades, avances y evidencia del trabajo realizado.",
        recursos: [
            actividades,
            avances,
            {
                clave: "evidencias",
                titulo: "Evidencias",
                descripcion: "Archivos vinculados al avance seleccionado.",
                ruta: `${seguimiento}/avances/{idAvance}/evidencias`,
                identidad: "idEvidencia",
                permisos: [p.registrarEvidencias],
                columnas: ["tipoEvidencia", "capturadaEn", "creadoEn"],
                detalle: `${seguimiento}/evidencias/{idEvidencia}`,
                requiereObra: true,
                filtros: ["idAvance"],
                crear: accion(
                    "Añadir evidencia",
                    "POST",
                    `${seguimiento}/evidencias`,
                    "SolicitudRegistrarEvidencia",
                    p.registrarEvidencias,
                    "evidencias",
                ),
            },
            incidencias,
        ],
    },
    actas: {
        titulo: "Actas semanales",
        ceja: "ACUERDOS Y TRAZABILIDAD",
        descripcion:
            "Consolida el trabajo de cada semana y conserva sus decisiones.",
        recursos: [actas],
    },
    asistencia: {
        titulo: "Asistencia",
        ceja: "JORNADA Y PERSONAL",
        descripcion:
            "Consulta las marcaciones y gestiona correcciones autorizadas.",
        recursos: [
            {
                clave: "registros",
                titulo: "Registros",
                descripcion: "Entradas y salidas por jornada.",
                ruta: "/api/asistencias",
                identidad: "idAsistencia",
                permisos: [
                    p.registrarAsistencia,
                    p.consultarAsistenciaPropia,
                    p.consultarConsolidadoAsistencia,
                ],
                columnas: [
                    "nombreCompletoTrabajador",
                    "fechaJornada",
                    "entradaEn",
                    "salidaEn",
                    "estado",
                ],
                filtros: ["idObra", "idTrabajador", "fechaDesde", "fechaHasta"],
                detalle: "/api/asistencias/{idAsistencia}",
                acciones: [
                    accion(
                        "Solicitar corrección",
                        "POST",
                        "/api/asistencias/correcciones",
                        "SolicitudCrearCorreccionAsistencia",
                        p.corregirAsistencia,
                    ),
                ],
            },
            correcciones,
            consolidado,
        ],
    },
    epp: {
        titulo: "Equipo de protección",
        ceja: "HIGIENE Y SEGURIDAD",
        descripcion:
            "Disponibilidad, entregas y reposiciones de protección personal.",
        recursos: [
            inventarioEpp,
            entregas,
            reposiciones,
            tiposEpp,
            {
                clave: "historial-epp",
                titulo: "Historial por trabajador",
                descripcion: "Entregas, devoluciones, pérdidas y reposiciones.",
                ruta: `${epp}/trabajadores/{idTrabajador}/historial`,
                identidad: "idReferencia",
                permisos: [p.consultarHistorialEpp],
                columnas: [
                    "tipoEvento",
                    "fechaHora",
                    "nombreTipoEpp",
                    "detalle",
                ],
                filtros: ["idTrabajador"],
            },
        ],
    },
    herramientas: {
        titulo: "Herramientas",
        ceja: "RECURSOS DE OBRA",
        descripcion: "Inventario, responsables e historial de herramientas.",
        recursos: [
            inventarioHerramientas,
            asignacionesHerramientas,
            eventosHerramientas,
            {
                clave: "tipos-herramienta",
                titulo: "Tipos",
                descripcion: "Clasificación de las herramientas.",
                ruta: `${herramientas}/tipos`,
                identidad: "idTipoHerramienta",
                permisos: [p.gestionarInventarioHerramientas],
                columnas: [
                    "nombre",
                    "nombreCategoria",
                    "descripcion",
                    "activo",
                ],
                crear: accion(
                    "Crear tipo",
                    "POST",
                    `${herramientas}/tipos`,
                    "SolicitudCrearTipoHerramienta",
                    p.gestionarInventarioHerramientas,
                ),
            },
            {
                clave: "categorias",
                titulo: "Categorías",
                descripcion: "Categorías de herramienta.",
                ruta: `${herramientas}/categorias`,
                identidad: "idCategoria",
                permisos: [p.gestionarInventarioHerramientas],
                columnas: ["nombre", "descripcion", "activo"],
                crear: accion(
                    "Crear categoría",
                    "POST",
                    `${herramientas}/categorias`,
                    "SolicitudCrearCategoriaHerramienta",
                    p.gestionarInventarioHerramientas,
                ),
            },
        ],
    },
    reportes: {
        titulo: "Reportes",
        ceja: "INFORMACIÓN PARA DECIDIR",
        descripcion:
            "Consulta el detalle autorizado de asistencia y protección personal.",
        recursos: [
            consolidado,
            {
                clave: "reporte-epp",
                titulo: "Reporte de EPP",
                descripcion:
                    "Cantidades y tipos entregados por trabajador y obra.",
                ruta: `${epp}/reportes`,
                identidad: "idTrabajador",
                permisos: [p.consultarReportesEpp],
                columnas: [
                    "nombreCompletoTrabajador",
                    "nombreObra",
                    "nombreTipoEpp",
                    "cantidadEntregada",
                    "cantidadActiva",
                    "cantidadReemplazada",
                    "cantidadPerdida",
                ],
                filtros: [
                    "idObra",
                    "idTrabajador",
                    "idTipoEpp",
                    "fechaDesde",
                    "fechaHasta",
                ],
            },
        ],
    },
    accesos: {
        titulo: "Usuarios y accesos",
        ceja: "ADMINISTRACIÓN",
        descripcion: "Cuentas individuales y permisos de la organización.",
        recursos: [
            {
                clave: "usuarios",
                titulo: "Usuarios",
                descripcion: "Cuentas vinculadas al personal y sus roles.",
                ruta: "/api/usuarios",
                identidad: "idUsuario",
                permisos: [p.gestionarUsuarios],
                columnas: ["nombreUsuario", "roles", "ultimoAcceso", "estado"],
                filtros: ["estado"],
                detalle: "/api/usuarios/{idUsuario}",
                crear: accion(
                    "Crear usuario",
                    "POST",
                    "/api/usuarios",
                    "SolicitudCrearUsuario",
                    p.gestionarUsuarios,
                ),
                acciones: [
                    accion(
                        "Editar cuenta",
                        "PUT",
                        "/api/usuarios/{idUsuario}",
                        "SolicitudActualizarUsuario",
                        p.gestionarUsuarios,
                    ),
                    accion(
                        "Cambiar estado",
                        "PATCH",
                        "/api/usuarios/{idUsuario}/estado",
                        "SolicitudCambiarEstadoUsuario",
                        p.gestionarUsuarios,
                    ),
                    accion(
                        "Asignar rol",
                        "POST",
                        "/api/usuarios/{idUsuario}/roles",
                        "SolicitudAsignarRolUsuario",
                        p.gestionarUsuarios,
                    ),
                ],
            },
            {
                clave: "roles",
                titulo: "Roles",
                descripcion: "Permisos efectivos y responsabilidades.",
                ruta: "/api/roles",
                identidad: "idRol",
                permisos: [p.gestionarRoles],
                columnas: ["nombre", "descripcion", "codigosPermiso", "activo"],
                crear: accion(
                    "Crear rol",
                    "POST",
                    "/api/roles",
                    "SolicitudCrearRol",
                    p.gestionarRoles,
                ),
                acciones: [
                    accion(
                        "Configurar permisos",
                        "PUT",
                        "/api/roles/{idRol}/permisos",
                        "SolicitudAsignarPermisosRol",
                        p.gestionarRoles,
                    ),
                ],
            },
            {
                clave: "permisos",
                titulo: "Permisos",
                descripcion: "Catálogo de capacidades definidas por la API.",
                ruta: "/api/permisos",
                identidad: "idPermiso",
                permisos: [p.gestionarRoles],
                columnas: ["nombre", "codigo", "descripcion", "activo"],
            },
        ],
    },
    auditoria: {
        titulo: "Auditoría",
        ceja: "TRAZABILIDAD",
        descripcion: "Operaciones y cambios registrados en el sistema.",
        recursos: [
            {
                clave: "auditorias",
                titulo: "Eventos de auditoría",
                descripcion: "Consulta restringida de operaciones críticas.",
                ruta: "/api/auditorias",
                identidad: "idAuditoria",
                permisos: [p.consultarAuditoria],
                columnas: [
                    "fechaHora",
                    "nombreUsuario",
                    "entidad",
                    "accion",
                    "campo",
                ],
                filtros: [
                    "entidad",
                    "accion",
                    "idUsuario",
                    "desdeUtc",
                    "hastaUtc",
                ],
            },
        ],
    },
};

export function rutaConDatos(ruta: string, datos: Fila) {
    let completa = true;
    const valor = ruta.replace(/\{(\w+)\}/g, (_, campo) => {
        if (!datos[campo]) {
            completa = false;
            return "";
        }
        return encodeURIComponent(String(datos[campo]));
    });
    return completa ? valor : null;
}
