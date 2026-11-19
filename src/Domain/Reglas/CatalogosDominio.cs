namespace ConstructoraEderTorres.Dominio.Reglas;

public static class EstadosUsuario
{
    public const string Activo = "ACTIVO";
    public const string Bloqueado = "BLOQUEADO";
    public const string Inactivo = "INACTIVO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Activo,
        Bloqueado,
        Inactivo
    };
}

public static class AccionesAuditoria
{
    public const string Crear = "CREAR";
    public const string Modificar = "MODIFICAR";
    public const string Anular = "ANULAR";
    public const string Aprobar = "APROBAR";
    public const string Rechazar = "RECHAZAR";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        Crear,
        Modificar,
        Anular,
        Aprobar,
        Rechazar
    };
}

public static class EstadosObra
{
    public const string Planificada = "PLANIFICADA";
    public const string Activa = "ACTIVA";
    public const string Suspendida = "SUSPENDIDA";
    public const string Finalizada = "FINALIZADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Planificada,
        Activa,
        Suspendida,
        Finalizada
    };
}

public static class EstadosTrabajador
{
    public const string Activo = "ACTIVO";
    public const string Inactivo = "INACTIVO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Activo,
        Inactivo
    };
}

public static class TiposContratacion
{
    public const string Fijo = "FIJO";
    public const string Subcontratado = "SUBCONTRATADO";
    public const string Temporal = "TEMPORAL";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Fijo,
        Subcontratado,
        Temporal
    };
}

public static class EstadosAsignacionObra
{
    public const string Activa = "ACTIVA";
    public const string Finalizada = "FINALIZADA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Activa,
        Finalizada,
        Anulada
    };
}

public static class NivelesEspecialidad
{
    public const string Basico = "BASICO";
    public const string Intermedio = "INTERMEDIO";
    public const string Avanzado = "AVANZADO";
    public const string Certificado = "CERTIFICADO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Basico,
        Intermedio,
        Avanzado,
        Certificado
    };
}

public static class EstadosAsistencia
{
    public const string Abierta = "ABIERTA";
    public const string Completa = "COMPLETA";
    public const string Corregida = "CORREGIDA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Abierta,
        Completa,
        Corregida,
        Anulada
    };
}

public static class EstadosCorreccionAsistencia
{
    public const string Pendiente = "PENDIENTE";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Pendiente,
        Aprobada,
        Rechazada,
        Anulada
    };
}

public static class EstadosActividad
{
    public const string Planificada = "PLANIFICADA";
    public const string EnProceso = "EN_PROCESO";
    public const string Completa = "COMPLETA";
    public const string Retrasada = "RETRASADA";
    public const string Cancelada = "CANCELADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Planificada,
        EnProceso,
        Completa,
        Retrasada,
        Cancelada
    };
}

public static class EstadosAvance
{
    public const string Borrador = "BORRADOR";
    public const string Registrado = "REGISTRADO";
    public const string Anulado = "ANULADO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Borrador,
        Registrado,
        Anulado
    };
}

public static class TiposEvidencia
{
    public const string Fotografia = "FOTOGRAFIA";
    public const string Bitacora = "BITACORA";
    public const string Documento = "DOCUMENTO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Fotografia,
        Bitacora,
        Documento
    };
}

public static class TiposIncidenciaObra
{
    public const string Clima = "CLIMA";
    public const string MaterialImportado = "MATERIAL_IMPORTADO";
    public const string Tecnica = "TECNICA";
    public const string Seguridad = "SEGURIDAD";
    public const string Otra = "OTRA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Clima,
        MaterialImportado,
        Tecnica,
        Seguridad,
        Otra
    };
}

public static class EstadosIncidencia
{
    public const string Abierta = "ABIERTA";
    public const string Resuelta = "RESUELTA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Abierta,
        Resuelta,
        Anulada
    };
}

public static class EstadosActa
{
    public const string Borrador = "BORRADOR";
    public const string Pendiente = "PENDIENTE";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Borrador,
        Pendiente,
        Aprobada,
        Rechazada,
        Anulada
    };
}

public static class DecisionesActa
{
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        Aprobada,
        Rechazada
    };
}

public static class EstadosEpp
{
    public const string Disponible = "DISPONIBLE";
    public const string Entregado = "ENTREGADO";
    public const string EnRevision = "EN_REVISION";
    public const string Perdido = "PERDIDO";
    public const string Retirado = "RETIRADO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Disponible,
        Entregado,
        EnRevision,
        Perdido,
        Retirado
    };
}

public static class CondicionesEpp
{
    public const string Nuevo = "NUEVO";
    public const string Bueno = "BUENO";
    public const string Desgastado = "DESGASTADO";
    public const string Danado = "DANADO";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        Nuevo,
        Bueno,
        Desgastado,
        Danado
    };
}

public static class EstadosEntregaEpp
{
    public const string Activa = "ACTIVA";
    public const string Devuelta = "DEVUELTA";
    public const string Perdida = "PERDIDA";
    public const string Reemplazada = "REEMPLAZADA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Activa,
        Devuelta,
        Perdida,
        Reemplazada,
        Anulada
    };
}

public static class MotivosReposicionEpp
{
    public const string Dano = "DANO";
    public const string Perdida = "PERDIDA";
    public const string Desgaste = "DESGASTE";
    public const string Vencimiento = "VENCIMIENTO";
    public const string Otro = "OTRO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Dano,
        Perdida,
        Desgaste,
        Vencimiento,
        Otro
    };
}

public static class EstadosReposicionEpp
{
    public const string Pendiente = "PENDIENTE";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Ejecutada = "EJECUTADA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Pendiente,
        Aprobada,
        Rechazada,
        Ejecutada,
        Anulada
    };
}

public static class TiposMovimientoEpp
{
    public const string Entrada = "ENTRADA";
    public const string Entrega = "ENTREGA";
    public const string Devolucion = "DEVOLUCION";
    public const string Perdida = "PERDIDA";
    public const string Retiro = "RETIRO";
    public const string Ajuste = "AJUSTE";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Entrada,
        Entrega,
        Devolucion,
        Perdida,
        Retiro,
        Ajuste
    };
}

public static class EstadosHerramienta
{
    public const string Disponible = "DISPONIBLE";
    public const string Asignada = "ASIGNADA";
    public const string EnRevision = "EN_REVISION";
    public const string Perdida = "PERDIDA";
    public const string Retirada = "RETIRADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Disponible,
        Asignada,
        EnRevision,
        Perdida,
        Retirada
    };
}

public static class CondicionesHerramienta
{
    public const string Nueva = "NUEVA";
    public const string Buena = "BUENA";
    public const string Desgastada = "DESGASTADA";
    public const string Danada = "DANADA";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        Nueva,
        Buena,
        Desgastada,
        Danada
    };
}

public static class EstadosAsignacionHerramienta
{
    public const string Activa = "ACTIVA";
    public const string Devuelta = "DEVUELTA";
    public const string Perdida = "PERDIDA";
    public const string Anulada = "ANULADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Activa,
        Devuelta,
        Perdida,
        Anulada
    };
}

public static class TiposIncidenciaHerramienta
{
    public const string Dano = "DANO";
    public const string Perdida = "PERDIDA";
    public const string Demora = "DEMORA";
    public const string Devolucion = "DEVOLUCION";
    public const string Otra = "OTRA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Dano,
        Perdida,
        Demora,
        Devolucion,
        Otra
    };
}

public static class EstadosIncidenciaHerramienta
{
    public const string Abierta = "ABIERTA";
    public const string EnProceso = "EN_PROCESO";
    public const string Resuelta = "RESUELTA";
    public const string Anulada = "ANULADA";
}
