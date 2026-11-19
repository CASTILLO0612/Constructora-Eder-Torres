namespace ConstructoraEderTorres.Dominio.Seguridad;

public static class NombresRol
{
    public const string GerenteGeneral = "GERENTE_GENERAL";
    public const string GerenteOperaciones = "GERENTE_OPERACIONES";
    public const string IngenieroResidente = "INGENIERO_RESIDENTE";
    public const string HigieneSeguridad = "HIGIENE_SEGURIDAD";
    public const string Administrativo = "ADMINISTRATIVO";
    public const string Trabajador = "TRABAJADOR";

    public static readonly IReadOnlySet<string> ConAlcanceGlobalObras = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        GerenteGeneral,
        GerenteOperaciones,
        HigieneSeguridad,
        Administrativo
    };
}

public sealed record DefinicionPermiso(string Codigo, string Nombre, string Descripcion);

public sealed record DefinicionRol(
    string Nombre,
    string Descripcion,
    IReadOnlyList<string> CodigosPermiso);

public static class CodigosPermiso
{
    public const string GestionarUsuarios = "seguridad.usuarios.gestionar";
    public const string GestionarRoles = "seguridad.roles.gestionar";
    public const string ConsultarAuditoria = "seguridad.auditoria.consultar";

    public const string GestionarObras = "obras.gestionar";
    public const string ConsultarObras = "obras.consultar";

    public const string GestionarTrabajadores = "personal.trabajadores.gestionar";
    public const string ConsultarTrabajadores = "personal.trabajadores.consultar";
    public const string GestionarAsignaciones = "personal.asignaciones.gestionar";

    public const string RegistrarAsistencia = "asistencia.registrar";
    public const string RegistrarAsistenciaPropia = "asistencia.propia.registrar";
    public const string ConsultarAsistenciaPropia = "asistencia.propia.consultar";
    public const string CorregirAsistencia = "asistencia.corregir";
    public const string AprobarCorreccionAsistencia = "asistencia.aprobar_correccion";
    public const string ConsultarConsolidadoAsistencia = "asistencia.consolidado.consultar";

    public const string GestionarActividades = "seguimiento.actividades.gestionar";
    public const string RegistrarAvances = "seguimiento.avances.registrar";
    public const string RegistrarEvidencias = "seguimiento.evidencias.registrar";
    public const string RegistrarIncidenciasObra = "seguimiento.incidencias.registrar";
    public const string ElaborarActas = "seguimiento.actas.elaborar";
    public const string AprobarActas = "seguimiento.actas.aprobar";

    public const string GestionarInventarioEpp = "epp.inventario.gestionar";
    public const string RegistrarEntregasEpp = "epp.entregas.registrar";
    public const string GestionarReposicionesEpp = "epp.reposiciones.gestionar";
    public const string ConsultarHistorialEpp = "epp.historial.consultar";
    public const string ConsultarReportesEpp = "epp.reportes.consultar";

    public const string GestionarInventarioHerramientas = "herramientas.inventario.gestionar";
    public const string GestionarAsignacionesHerramientas = "herramientas.asignaciones.gestionar";
    public const string RegistrarIncidenciasHerramientas = "herramientas.incidencias.registrar";

    public const string OperarSincronizacion = "sincronizacion.operar";

    public static readonly IReadOnlyList<DefinicionPermiso> CatalogoBase =
    [
        new(GestionarUsuarios, "Gestionar cuentas de usuario", "Crear, consultar, actualizar y desactivar cuentas de usuarios autorizados."),
        new(GestionarRoles, "Gestionar roles y permisos", "Administrar roles, permisos y sus asignaciones."),
        new(ConsultarAuditoria, "Consultar auditoría", "Consultar trazabilidad de cambios sobre registros críticos."),
        new(GestionarObras, "Gestionar obras", "Crear, actualizar y cambiar el estado de obras."),
        new(ConsultarObras, "Consultar obras", "Consultar obras dentro del alcance autorizado."),
        new(GestionarTrabajadores, "Gestionar trabajadores", "Registrar, actualizar y desactivar trabajadores."),
        new(ConsultarTrabajadores, "Consultar trabajadores", "Consultar catálogo de trabajadores, cargos y especialidades."),
        new(GestionarAsignaciones, "Gestionar asignaciones y cuadrillas", "Asignar trabajadores y cuadrillas a obras."),
        new(RegistrarAsistencia, "Registrar asistencia", "Marcar entrada y salida de personal con lectura GPS puntual."),
        new(RegistrarAsistenciaPropia, "Registrar asistencia propia", "Marcar exclusivamente la entrada y salida del trabajador vinculado a la sesión."),
        new(ConsultarAsistenciaPropia, "Consultar asistencia propia", "Consultar exclusivamente las asistencias y asignaciones del trabajador vinculado a la sesión."),
        new(CorregirAsistencia, "Solicitar corrección de asistencia", "Registrar solicitudes de corrección de asistencia con motivo."),
        new(AprobarCorreccionAsistencia, "Aprobar corrección de asistencia", "Aprobar o rechazar correcciones de asistencia."),
        new(ConsultarConsolidadoAsistencia, "Consultar consolidado de asistencia", "Filtrar consolidado de asistencia por obra, trabajador, período y cuadrilla."),
        new(GestionarActividades, "Gestionar actividades de obra", "Registrar y actualizar actividades y sus participantes."),
        new(RegistrarAvances, "Registrar avances de obra", "Registrar avances físicos diarios/semanales y justificaciones."),
        new(RegistrarEvidencias, "Registrar evidencias de avance", "Adjuntar fotografías, bitácoras y documentos de avance."),
        new(RegistrarIncidenciasObra, "Registrar incidencias de obra", "Documentar incidencias que afecten actividades o cronograma."),
        new(ElaborarActas, "Elaborar actas semanales", "Consolidar avances, incidencias y participantes en acta semanal."),
        new(AprobarActas, "Aprobar o rechazar actas semanales", "Emitir decisión de aprobación o rechazo sobre actas semanales."),
        new(GestionarInventarioEpp, "Gestionar inventario de EPP", "Administrar tipos de EPP, altas y existencias."),
        new(RegistrarEntregasEpp, "Registrar entregas de EPP", "Registrar entregas y devoluciones de EPP a trabajadores."),
        new(GestionarReposicionesEpp, "Gestionar reposiciones de EPP", "Registrar y ejecutar reposiciones de EPP por daño, pérdida o desgaste."),
        new(ConsultarHistorialEpp, "Consultar historial de EPP", "Consultar historial cronológico de EPP por trabajador."),
        new(ConsultarReportesEpp, "Consultar reportes de EPP", "Generar reportes de EPP entregado por trabajador y obra."),
        new(GestionarInventarioHerramientas, "Gestionar inventario de herramientas", "Administrar categorías, tipos y herramientas individuales."),
        new(GestionarAsignacionesHerramientas, "Gestionar asignaciones de herramientas", "Asignar herramientas a trabajadores y obras."),
        new(RegistrarIncidenciasHerramientas, "Registrar devoluciones e incidencias de herramientas", "Registrar devoluciones, daños y pérdidas de herramientas."),
        new(OperarSincronizacion, "Sincronizar operaciones offline", "Enviar lotes de operaciones capturadas sin conexión.")
    ];

    // Plantilla inicial para una base sin usuarios; los permisos vigentes siempre se consultan en PostgreSQL.
    public static readonly IReadOnlyList<DefinicionRol> RolesIniciales =
    [
        new(NombresRol.Trabajador,
            "Marcación individual con GPS y consulta de asistencia propia, sin acceso a otros trabajadores.",
            [RegistrarAsistenciaPropia, ConsultarAsistenciaPropia, OperarSincronizacion]),
        new(
            NombresRol.GerenteGeneral,
            "Supervisión estratégica global, administración de seguridad, obras y aprobaciones.",
            CatalogoBase.Select(permiso => permiso.Codigo).ToArray()),
        new(
            NombresRol.GerenteOperaciones,
            "Coordinación y seguimiento operativo con alcance global de obras.",
            [
                ConsultarAuditoria,
                GestionarObras,
                ConsultarObras,
                GestionarTrabajadores,
                ConsultarTrabajadores,
                GestionarAsignaciones,
                RegistrarAsistencia,
                CorregirAsistencia,
                AprobarCorreccionAsistencia,
                ConsultarConsolidadoAsistencia,
                GestionarActividades,
                RegistrarAvances,
                RegistrarEvidencias,
                RegistrarIncidenciasObra,
                ElaborarActas,
                AprobarActas,
                GestionarInventarioEpp,
                RegistrarEntregasEpp,
                GestionarReposicionesEpp,
                ConsultarHistorialEpp,
                ConsultarReportesEpp,
                GestionarInventarioHerramientas,
                GestionarAsignacionesHerramientas,
                RegistrarIncidenciasHerramientas,
                OperarSincronizacion
            ]),
        new(
            NombresRol.IngenieroResidente,
            "Seguimiento operativo en campo, personal, asistencia, avances y actas dentro de sus obras asignadas.",
            [
                ConsultarObras,
                GestionarTrabajadores,
                ConsultarTrabajadores,
                GestionarAsignaciones,
                RegistrarAsistencia,
                CorregirAsistencia,
                ConsultarConsolidadoAsistencia,
                GestionarActividades,
                RegistrarAvances,
                RegistrarEvidencias,
                RegistrarIncidenciasObra,
                ElaborarActas,
                ConsultarHistorialEpp,
                OperarSincronizacion
            ]),
        new(
            NombresRol.HigieneSeguridad,
            "Control de inventario, entregas y reposiciones de EPP y gestión de herramientas.",
            [
                ConsultarObras,
                ConsultarTrabajadores,
                GestionarInventarioEpp,
                RegistrarEntregasEpp,
                GestionarReposicionesEpp,
                ConsultarHistorialEpp,
                ConsultarReportesEpp,
                GestionarInventarioHerramientas,
                GestionarAsignacionesHerramientas,
                RegistrarIncidenciasHerramientas,
                OperarSincronizacion
            ]),
        new(
            NombresRol.Administrativo,
            "Consulta administrativa de asistencia, personal y reportes autorizados.",
            [
                ConsultarObras,
                ConsultarTrabajadores,
                ConsultarConsolidadoAsistencia,
                ConsultarReportesEpp
            ])
    ];
}
