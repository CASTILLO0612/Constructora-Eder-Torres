using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed partial class AdaptadorSeguimientoObraPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj,
    PaginacionConsulta paginacion) : IPuertoSeguimientoObra
{
    public async Task<IReadOnlyList<RespuestaEvidencia>> ListarEvidenciasPorAvanceAsync(
        long idAvance, CancellationToken cancelacion = default) =>
        await ConsultarEvidenciasBase(contexto.Evidencias.Where(e => e.IdAvance == idAvance)
            .OrderByDescending(e => e.CapturadaEn).ThenByDescending(e => e.IdEvidencia).Paginar(paginacion))
            .ToListAsync(cancelacion);

    public Task<bool> ExisteObraAsync(int idObra, CancellationToken cancelacion = default) =>
        contexto.Obras.AnyAsync(o => o.IdObra == idObra, cancelacion);

    public async Task<int?> ObtenerIdObraDeAsignacionAsync(int idAsignacion, CancellationToken cancelacion = default) =>
        await contexto.AsignacionesTrabajadorObra.AsNoTracking()
            .Where(a => a.IdAsignacion == idAsignacion)
            .Select(a => (int?)a.IdObra)
            .FirstOrDefaultAsync(cancelacion);

    public async Task<int?> ObtenerIdObraDeActividadAsync(int idActividad, CancellationToken cancelacion = default) =>
        await contexto.Actividades.AsNoTracking()
            .Where(a => a.IdActividad == idActividad)
            .Select(a => (int?)a.IdObra)
            .FirstOrDefaultAsync(cancelacion);

    public async Task<(int IdActividad, int IdObra)?> ObtenerContextoAvanceAsync(
        long idAvance,
        CancellationToken cancelacion = default)
    {
        var fila = await (
            from av in contexto.Avances.AsNoTracking()
            join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
            where av.IdAvance == idAvance
            select new { av.IdActividad, act.IdObra })
            .FirstOrDefaultAsync(cancelacion);

        return fila is null ? null : (fila.IdActividad, fila.IdObra);
    }

    public async Task<int?> ObtenerIdObraDeIncidenciaAsync(long idIncidencia, CancellationToken cancelacion = default) =>
        await contexto.Incidencias.AsNoTracking()
            .Where(i => i.IdIncidencia == idIncidencia)
            .Select(i => (int?)i.IdObra)
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> ExisteCodigoActividadEnObraAsync(
        int idObra,
        string codigo,
        int? excluirIdActividad = null,
        CancellationToken cancelacion = default) =>
        contexto.Actividades.AnyAsync(
            a => a.IdObra == idObra
                 && a.Codigo.ToLower() == codigo.ToLower()
                 && (excluirIdActividad == null || a.IdActividad != excluirIdActividad.Value),
            cancelacion);

    public async Task<RespuestaActividad> CrearActividadAsync(
        SolicitudCrearActividad solicitud,
        string estadoInicial,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var actividad = new Actividad
        {
            IdObra = solicitud.IdObra,
            IdAsignacionResponsable = solicitud.IdAsignacionResponsable,
            Codigo = solicitud.Codigo,
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            UnidadMedida = solicitud.UnidadMedida,
            CantidadPlanificada = solicitud.CantidadPlanificada,
            FechaInicioPlan = solicitud.FechaInicioPlan,
            FechaFinPlan = solicitud.FechaFinPlan,
            Estado = estadoInicial
        };

        contexto.Actividades.Add(actividad);
        await contexto.SaveChangesAsync(cancelacion);

        if (solicitud.Participantes is { Count: > 0 })
        {
            foreach (var p in solicitud.Participantes.DistinctBy(x => x.IdAsignacion))
            {
                contexto.TrabajadoresActividad.Add(new TrabajadorActividad
                {
                    IdActividad = actividad.IdActividad,
                    IdAsignacion = p.IdAsignacion,
                    RolParticipacion = string.IsNullOrWhiteSpace(p.RolParticipacion) ? null : p.RolParticipacion.Trim()
                });
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = actividad.IdActividad.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerActividadPorIdAsync(actividad.IdActividad, cancelacion))!;
    }

    public async Task<RespuestaActividad?> ObtenerActividadPorIdAsync(
        int idActividad,
        CancellationToken cancelacion = default)
    {
        var actividad = await contexto.Actividades.AsNoTracking()
            .FirstOrDefaultAsync(a => a.IdActividad == idActividad, cancelacion);
        if (actividad is null)
        {
            return null;
        }

        var acumulado = await contexto.Avances.AsNoTracking()
            .Where(av => av.IdActividad == idActividad && av.Estado == EstadosAvance.Registrado)
            .SumAsync(av => (decimal?)av.CantidadEjecutada, cancelacion) ?? 0m;

        var participantes = await ConsultarParticipantesActividadAsync([idActividad], cancelacion);
        return MapearActividad(actividad, acumulado, participantes.GetValueOrDefault(idActividad, []));
    }

    public async Task<IReadOnlyList<RespuestaActividad>> ListarActividadesPorObraAsync(
        int idObra,
        CancellationToken cancelacion = default)
    {
        var actividades = await contexto.Actividades.AsNoTracking()
            .Where(a => a.IdObra == idObra)
            .OrderBy(a => a.IdActividad)
            .Paginar(paginacion).ToListAsync(cancelacion);

        if (actividades.Count == 0)
        {
            return [];
        }

        var ids = actividades.Select(a => a.IdActividad).ToArray();
        var acumulados = await contexto.Avances.AsNoTracking()
            .Where(av => ids.Contains(av.IdActividad) && av.Estado == EstadosAvance.Registrado)
            .GroupBy(av => av.IdActividad)
            .Select(g => new { IdActividad = g.Key, Total = g.Sum(x => x.CantidadEjecutada) })
            .ToDictionaryAsync(x => x.IdActividad, x => x.Total, cancelacion);

        var participantes = await ConsultarParticipantesActividadAsync(ids, cancelacion);

        return actividades.Select(a => MapearActividad(
            a,
            acumulados.GetValueOrDefault(a.IdActividad, 0m),
            participantes.GetValueOrDefault(a.IdActividad, []))).ToList();
    }

    public async Task<RespuestaActividad?> ActualizarActividadAsync(
        int idActividad,
        SolicitudActualizarActividad solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var actividad = await contexto.Actividades.FirstOrDefaultAsync(a => a.IdActividad == idActividad, cancelacion);
        if (actividad is null)
        {
            return null;
        }

        actividad.IdAsignacionResponsable = solicitud.IdAsignacionResponsable;
        actividad.Nombre = solicitud.Nombre;
        actividad.Descripcion = solicitud.Descripcion;
        actividad.UnidadMedida = solicitud.UnidadMedida;
        actividad.CantidadPlanificada = solicitud.CantidadPlanificada;
        actividad.FechaInicioPlan = solicitud.FechaInicioPlan;
        actividad.FechaFinPlan = solicitud.FechaFinPlan;
        actividad.Estado = solicitud.Estado;

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerActividadPorIdAsync(idActividad, cancelacion);
    }

    public async Task<RespuestaParticipanteActividad> AgregarParticipanteActividadAsync(
        int idActividad,
        SolicitudParticipanteActividad solicitud,
        CancellationToken cancelacion = default)
    {
        var existente = await contexto.TrabajadoresActividad
            .FirstOrDefaultAsync(
                ta => ta.IdActividad == idActividad && ta.IdAsignacion == solicitud.IdAsignacion,
                cancelacion);

        if (existente is null)
        {
            existente = new TrabajadorActividad
            {
                IdActividad = idActividad,
                IdAsignacion = solicitud.IdAsignacion,
                RolParticipacion = string.IsNullOrWhiteSpace(solicitud.RolParticipacion) ? null : solicitud.RolParticipacion.Trim()
            };
            contexto.TrabajadoresActividad.Add(existente);
        }
        else
        {
            existente.RolParticipacion = string.IsNullOrWhiteSpace(solicitud.RolParticipacion) ? null : solicitud.RolParticipacion.Trim();
        }

        await contexto.SaveChangesAsync(cancelacion);

        return await (
            from ta in contexto.TrabajadoresActividad.AsNoTracking()
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on ta.IdAsignacion equals asig.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
            where ta.IdActividad == idActividad && ta.IdAsignacion == solicitud.IdAsignacion
            select new RespuestaParticipanteActividad(
                ta.IdActividad,
                ta.IdAsignacion,
                t.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                ta.RolParticipacion))
            .FirstAsync(cancelacion);
    }

    public async Task<RespuestaAvance?> ObtenerAvancePorIdAsync(
        long idAvance,
        CancellationToken cancelacion = default) =>
        await (
            from av in contexto.Avances.AsNoTracking()
            join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
            where av.IdAvance == idAvance
            select new RespuestaAvance(
                av.IdAvance,
                av.IdActividad,
                act.IdObra,
                act.Codigo,
                act.Nombre,
                av.FechaAvance,
                av.CantidadEjecutada,
                av.Observaciones,
                av.Estado,
                av.IdUsuarioRegistro,
                av.UuidRegistro,
                av.CreadoEn,
                av.SincronizadoEn,
                false))
            .FirstOrDefaultAsync(cancelacion);
    public async Task<RespuestaAvance?> ObtenerAvancePorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default) =>
        await (
            from av in contexto.Avances.AsNoTracking()
            join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
            where av.UuidRegistro == uuidRegistro
            select new RespuestaAvance(
                av.IdAvance,
                av.IdActividad,
                act.IdObra,
                act.Codigo,
                act.Nombre,
                av.FechaAvance,
                av.CantidadEjecutada,
                av.Observaciones,
                av.Estado,
                av.IdUsuarioRegistro,
                av.UuidRegistro,
                av.CreadoEn,
                av.SincronizadoEn,
                false))
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> ExisteAvanceActividadFechaAsync(
        int idActividad,
        DateOnly fechaAvance,
        CancellationToken cancelacion = default) =>
        contexto.Avances.AnyAsync(
            av => av.IdActividad == idActividad && av.FechaAvance == fechaAvance,
            cancelacion);

    public async Task<RespuestaAvance> RegistrarAvanceAsync(
        SolicitudRegistrarAvance solicitud,
        string estado,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default,
        long? idAvanceBorrador = null)
    {
        var avance = idAvanceBorrador is { } idBorrador
            ? await contexto.Avances.SingleAsync(av => av.IdAvance == idBorrador, cancelacion)
            : new Avance
            {
                IdActividad = solicitud.IdActividad,
                FechaAvance = solicitud.FechaAvance,
                CantidadEjecutada = solicitud.CantidadEjecutada,
                Observaciones = solicitud.Observaciones,
                Estado = estado,
                IdUsuarioRegistro = idUsuarioRegistro,
                UuidRegistro = uuidRegistro,
                CreadoEn = creadoEnUtc,
                SincronizadoEn = sincronizadoEnUtc
            };

        if (idAvanceBorrador is not null)
        {
            if (avance.Estado != EstadosAvance.Borrador || estado != EstadosAvance.Registrado)
                throw new ExcepcionPersistencia(ErrorNegocio.Conflicto("transicion_avance_invalida",
                    "Solo un avance BORRADOR puede confirmarse como REGISTRADO."));
            avance.Estado = estado;
            avance.CantidadEjecutada = solicitud.CantidadEjecutada;
            avance.Observaciones = solicitud.Observaciones;
            avance.SincronizadoEn = sincronizadoEnUtc ?? avance.SincronizadoEn;
        }

        if (estado == EstadosAvance.Registrado && solicitud.EstadoActividad is { } siguiente)
        {
            var actividad = await contexto.Actividades.SingleAsync(a => a.IdActividad == solicitud.IdActividad, cancelacion);
            foreach (var paso in PoliticaEstadosActividad.RutaDeRegistro(actividad.Estado, siguiente))
            {
                contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria with
                {
                    Entidad = "actividades",
                    IdRegistro = actividad.IdActividad.ToString(),
                    Accion = AccionesAuditoria.Modificar,
                    Campo = "estado",
                    ValorAnterior = actividad.Estado,
                    ValorNuevo = paso,
                    UuidOperacion = Guid.NewGuid()
                }, creadoEnUtc));
                actividad.Estado = paso;
            }
        }
        if (idAvanceBorrador is null) contexto.Avances.Add(avance);
        await contexto.SaveChangesAsync(cancelacion);

        if (solicitud.IdIncidenciaVinculada is { } idIncidencia)
        {
            var yaVinculada = await contexto.ActividadesIncidencia.AnyAsync(
                ai => ai.IdActividad == solicitud.IdActividad && ai.IdIncidencia == idIncidencia,
                cancelacion);
            if (!yaVinculada)
            {
                contexto.ActividadesIncidencia.Add(new ActividadIncidencia
                {
                    IdActividad = solicitud.IdActividad,
                    IdIncidencia = idIncidencia
                });
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = avance.IdAvance.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await (
            from av in contexto.Avances.AsNoTracking()
            join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
            where av.IdAvance == avance.IdAvance
            select new RespuestaAvance(
                av.IdAvance,
                av.IdActividad,
                act.IdObra,
                act.Codigo,
                act.Nombre,
                av.FechaAvance,
                av.CantidadEjecutada,
                av.Observaciones,
                av.Estado,
                av.IdUsuarioRegistro,
                av.UuidRegistro,
                av.CreadoEn,
                av.SincronizadoEn,
                false))
            .FirstAsync(cancelacion);
    }

    public async Task<IReadOnlyList<RespuestaAvance>> ListarAvancesPorObraAsync(
        int idObra,
        int? idActividad = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken cancelacion = default)
    {
        return await (
            from av in contexto.Avances.AsNoTracking()
            join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
            where act.IdObra == idObra
                  && (idActividad == null || av.IdActividad == idActividad.Value)
                  && (fechaDesde == null || av.FechaAvance >= fechaDesde.Value)
                  && (fechaHasta == null || av.FechaAvance <= fechaHasta.Value)
            orderby av.FechaAvance descending, av.IdAvance descending
            select new RespuestaAvance(
                av.IdAvance,
                av.IdActividad,
                act.IdObra,
                act.Codigo,
                act.Nombre,
                av.FechaAvance,
                av.CantidadEjecutada,
                av.Observaciones,
                av.Estado,
                av.IdUsuarioRegistro,
                av.UuidRegistro,
                av.CreadoEn,
                av.SincronizadoEn,
                false))
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public Task<RespuestaEvidencia?> ObtenerEvidenciaPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default) =>
        ConsultarEvidenciasBase(contexto.Evidencias.Where(e => e.UuidRegistro == uuidRegistro)).FirstOrDefaultAsync(cancelacion);

    public Task<RespuestaEvidencia?> ObtenerEvidenciaPorIdAsync(long idEvidencia, CancellationToken cancelacion = default) =>
        ConsultarEvidenciasBase(contexto.Evidencias.Where(e => e.IdEvidencia == idEvidencia)).FirstOrDefaultAsync(cancelacion);

    private IQueryable<RespuestaEvidencia> ConsultarEvidenciasBase(IQueryable<Evidencia> origen) =>
        from ev in origen.AsNoTracking()
        join av in contexto.Avances.AsNoTracking() on ev.IdAvance equals av.IdAvance
        join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
        select new RespuestaEvidencia(ev.IdEvidencia, ev.IdAvance, av.IdActividad, act.IdObra,
            ev.TipoEvidencia, ev.RutaArchivo, ev.HuellaArchivo, ev.CapturadaEn, ev.Latitud, ev.Longitud,
            ev.IdUsuarioRegistro, ev.UuidRegistro, ev.CreadoEn, ev.SincronizadoEn, false);

    public async Task<RespuestaEvidencia> RegistrarEvidenciaAsync(
        long idAvance,
        string tipoEvidencia,
        string rutaArchivo,
        string huellaArchivo,
        DateTime capturadaEnUtc,
        decimal? latitud,
        decimal? longitud,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var evidencia = new Evidencia
        {
            IdAvance = idAvance,
            TipoEvidencia = tipoEvidencia,
            RutaArchivo = rutaArchivo,
            HuellaArchivo = huellaArchivo,
            CapturadaEn = capturadaEnUtc,
            Latitud = latitud,
            Longitud = longitud,
            IdUsuarioRegistro = idUsuarioRegistro,
            UuidRegistro = uuidRegistro,
            CreadoEn = creadoEnUtc,
            SincronizadoEn = sincronizadoEnUtc
        };

        contexto.Evidencias.Add(evidencia);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = evidencia.IdEvidencia.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await (
            from ev in contexto.Evidencias.AsNoTracking()
            join av in contexto.Avances.AsNoTracking() on ev.IdAvance equals av.IdAvance
            join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
            where ev.IdEvidencia == evidencia.IdEvidencia
            select new RespuestaEvidencia(
                ev.IdEvidencia,
                ev.IdAvance,
                av.IdActividad,
                act.IdObra,
                ev.TipoEvidencia,
                ev.RutaArchivo,
                ev.HuellaArchivo,
                ev.CapturadaEn,
                ev.Latitud,
                ev.Longitud,
                ev.IdUsuarioRegistro,
                ev.UuidRegistro,
                ev.CreadoEn,
                ev.SincronizadoEn,
                false))
            .FirstAsync(cancelacion);
    }

    public async Task<IReadOnlyList<RespuestaEvidencia>> ListarEvidenciasPorObraAsync(
        int idObra,
        long? idAvance = null,
        CancellationToken cancelacion = default)
    {
        return await (
            from ev in contexto.Evidencias.AsNoTracking()
            join av in contexto.Avances.AsNoTracking() on ev.IdAvance equals av.IdAvance
            join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
            where act.IdObra == idObra
                  && (idAvance == null || ev.IdAvance == idAvance.Value)
            orderby ev.CapturadaEn descending, ev.IdEvidencia descending
            select new RespuestaEvidencia(
                ev.IdEvidencia,
                ev.IdAvance,
                av.IdActividad,
                act.IdObra,
                ev.TipoEvidencia,
                ev.RutaArchivo,
                ev.HuellaArchivo,
                ev.CapturadaEn,
                ev.Latitud,
                ev.Longitud,
                ev.IdUsuarioRegistro,
                ev.UuidRegistro,
                ev.CreadoEn,
                ev.SincronizadoEn,
                false))
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public async Task<RespuestaIncidenciaObra?> ObtenerIncidenciaPorIdAsync(
        long idIncidencia,
        CancellationToken cancelacion = default)
    {
        var incidencia = await contexto.Incidencias.AsNoTracking()
            .FirstOrDefaultAsync(i => i.IdIncidencia == idIncidencia, cancelacion);
        if (incidencia is null)
        {
            return null;
        }

        var actividades = await contexto.ActividadesIncidencia.AsNoTracking()
            .Where(ai => ai.IdIncidencia == incidencia.IdIncidencia)
            .OrderBy(ai => ai.IdActividad)
            .Select(ai => ai.IdActividad)
            .ToListAsync(cancelacion);

        return MapearIncidencia(incidencia, actividades);
    }
    public async Task<RespuestaIncidenciaObra?> ObtenerIncidenciaPorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default)
    {
        var incidencia = await contexto.Incidencias.AsNoTracking()
            .FirstOrDefaultAsync(i => i.UuidRegistro == uuidRegistro, cancelacion);
        if (incidencia is null)
        {
            return null;
        }

        var actividades = await contexto.ActividadesIncidencia.AsNoTracking()
            .Where(ai => ai.IdIncidencia == incidencia.IdIncidencia)
            .OrderBy(ai => ai.IdActividad)
            .Select(ai => ai.IdActividad)
            .ToListAsync(cancelacion);

        return MapearIncidencia(incidencia, actividades);
    }

    public async Task<RespuestaIncidenciaObra> RegistrarIncidenciaObraAsync(
        SolicitudRegistrarIncidenciaObra solicitud,
        string estado,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var incidencia = new Incidencia
        {
            IdObra = solicitud.IdObra,
            TipoIncidencia = solicitud.TipoIncidencia,
            InicioEn = solicitud.InicioEn,
            FinEn = solicitud.FinEn,
            HorasPerdidas = solicitud.HorasPerdidas,
            Descripcion = solicitud.Descripcion,
            Justificacion = solicitud.Justificacion,
            Estado = estado,
            IdUsuarioRegistro = idUsuarioRegistro,
            UuidRegistro = uuidRegistro,
            CreadoEn = creadoEnUtc,
            SincronizadoEn = sincronizadoEnUtc
        };

        contexto.Incidencias.Add(incidencia);
        await contexto.SaveChangesAsync(cancelacion);

        var idsActividades = solicitud.IdsActividadesAfectadas?.Distinct().OrderBy(x => x).ToList() ?? [];
        foreach (var idActividad in idsActividades)
        {
            contexto.ActividadesIncidencia.Add(new ActividadIncidencia
            {
                IdIncidencia = incidencia.IdIncidencia,
                IdActividad = idActividad
            });
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = incidencia.IdIncidencia.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return MapearIncidencia(incidencia, idsActividades);
    }

    public async Task<IReadOnlyList<RespuestaIncidenciaObra>> ListarIncidenciasPorObraAsync(
        int idObra,
        CancellationToken cancelacion = default)
    {
        var incidencias = await contexto.Incidencias.AsNoTracking()
            .Where(i => i.IdObra == idObra)
            .OrderByDescending(i => i.InicioEn)
            .ThenByDescending(i => i.IdIncidencia)
            .Paginar(paginacion).ToListAsync(cancelacion);

        if (incidencias.Count == 0)
        {
            return [];
        }

        var ids = incidencias.Select(i => i.IdIncidencia).ToArray();
        var vinculos = await contexto.ActividadesIncidencia.AsNoTracking()
            .Where(ai => ids.Contains(ai.IdIncidencia))
            .ToListAsync(cancelacion);

        var porIncidencia = vinculos
            .GroupBy(v => v.IdIncidencia)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(v => v.IdActividad).OrderBy(x => x).ToList());

        return incidencias.Select(i => MapearIncidencia(
            i,
            porIncidencia.GetValueOrDefault(i.IdIncidencia, []))).ToList();
    }

    public Task<bool> ExisteNumeroActaEnObraAsync(
        int idObra,
        string numeroActa,
        CancellationToken cancelacion = default) =>
        contexto.Actas.AnyAsync(
            a => a.IdObra == idObra && a.NumeroActa.ToLower() == numeroActa.ToLower(),
            cancelacion);

    public async Task<RespuestaActa> ElaborarActaAsync(
        SolicitudElaborarActa solicitud,
        string estadoInicial,
        int idUsuarioCreador,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var acta = new Acta
        {
            IdObra = solicitud.IdObra,
            NumeroActa = solicitud.NumeroActa,
            PeriodoInicio = solicitud.PeriodoInicio,
            PeriodoFin = solicitud.PeriodoFin,
            Observaciones = solicitud.Observaciones,
            MejorasPropuestas = solicitud.MejorasPropuestas,
            SolicitudesCambio = solicitud.SolicitudesCambio,
            Estado = estadoInicial,
            IdUsuarioCreador = idUsuarioCreador,
            UuidRegistro = uuidRegistro,
            CreadoEn = creadoEnUtc
        };

        contexto.Actas.Add(acta);
        await contexto.SaveChangesAsync(cancelacion);

        var idsAvances = solicitud.IdsAvances is { Count: > 0 }
            ? solicitud.IdsAvances.Distinct().ToList()
            : await (
                from av in contexto.Avances.AsNoTracking()
                join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
                where act.IdObra == solicitud.IdObra && av.Estado == EstadosAvance.Registrado
                      && av.FechaAvance >= solicitud.PeriodoInicio
                      && av.FechaAvance <= solicitud.PeriodoFin
                select av.IdAvance)
                .ToListAsync(cancelacion);

        foreach (var idAvance in idsAvances)
        {
            contexto.ActasAvance.Add(new ActaAvance
            {
                IdActa = acta.IdActa,
                IdAvance = idAvance
            });
        }

        var idsIncidencias = solicitud.IdsIncidencias is { Count: > 0 }
            ? solicitud.IdsIncidencias.Distinct().ToList()
            : await contexto.Incidencias.AsNoTracking()
                .Where(i => i.IdObra == solicitud.IdObra
                            && DateOnly.FromDateTime(i.InicioEn) >= solicitud.PeriodoInicio
                            && DateOnly.FromDateTime(i.InicioEn) <= solicitud.PeriodoFin)
                .Select(i => i.IdIncidencia)
                .ToListAsync(cancelacion);

        foreach (var idIncidencia in idsIncidencias)
        {
            contexto.ActasIncidencia.Add(new ActaIncidencia
            {
                IdActa = acta.IdActa,
                IdIncidencia = idIncidencia
            });
        }

        if (solicitud.Participantes is { Count: > 0 })
        {
            foreach (var p in solicitud.Participantes.DistinctBy(x => x.IdAsignacion))
            {
                contexto.TrabajadoresActa.Add(new TrabajadorActa
                {
                    IdActa = acta.IdActa,
                    IdAsignacion = p.IdAsignacion,
                    TipoParticipacion = p.TipoParticipacion.Trim().ToUpperInvariant(),
                    RutaFirma = string.IsNullOrWhiteSpace(p.RutaFirma) ? null : p.RutaFirma.Trim(),
                    FirmadoEn = p.FirmadoEn
                });
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = acta.IdActa.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerActaPorIdAsync(acta.IdActa, cancelacion))!;
    }

    public async Task<RespuestaActa?> ObtenerActaPorIdAsync(
        int idActa,
        CancellationToken cancelacion = default)
    {
        var fila = await (
            from a in contexto.Actas.AsNoTracking()
            join o in contexto.Obras.AsNoTracking() on a.IdObra equals o.IdObra
            where a.IdActa == idActa
            select new { Acta = a, Obra = o })
            .FirstOrDefaultAsync(cancelacion);

        if (fila is null)
        {
            return null;
        }

        return await ConstruirRespuestaActaAsync(fila.Acta, fila.Obra.Codigo, fila.Obra.Nombre, cancelacion);
    }

    public async Task<IReadOnlyList<ResumenActa>> ListarActasPorObraAsync(
        int idObra, CancellationToken cancelacion = default) =>
        await contexto.Actas.AsNoTracking().Where(a => a.IdObra == idObra)
            .OrderByDescending(a => a.PeriodoInicio).ThenByDescending(a => a.IdActa)
            .Paginar(paginacion)
            .Select(a => new ResumenActa(a.IdActa, a.IdObra, a.NumeroActa,
                a.PeriodoInicio, a.PeriodoFin, a.Estado, a.CreadoEn))
            .ToListAsync(cancelacion);

    public async Task<RespuestaActa?> EnviarActaARevisionAsync(
        int idActa,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var acta = await contexto.Actas.FirstOrDefaultAsync(a => a.IdActa == idActa, cancelacion);
        if (acta is null)
        {
            return null;
        }

        acta.Estado = EstadosActa.Pendiente;
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerActaPorIdAsync(idActa, cancelacion);
    }

    public async Task<RespuestaActa?> ResolverActaAsync(
        int idActa,
        int idUsuarioRevisor,
        string decision,
        string? comentario,
        DateTime fechaHoraUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var acta = await contexto.Actas.FirstOrDefaultAsync(a => a.IdActa == idActa, cancelacion);
        if (acta is null)
        {
            return null;
        }

        acta.Estado = decision;
        contexto.AprobacionesActa.Add(new AprobacionActa
        {
            IdActa = idActa,
            IdUsuario = idUsuarioRevisor,
            Decision = decision,
            Comentario = comentario,
            FechaHora = fechaHoraUtc
        });

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, fechaHoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerActaPorIdAsync(idActa, cancelacion);
    }

    private async Task<RespuestaActa> ConstruirRespuestaActaAsync(
        Acta acta,
        string codigoObra,
        string nombreObra,
        CancellationToken cancelacion)
    {
        var idsAvances = await contexto.ActasAvance.AsNoTracking()
            .Where(aa => aa.IdActa == acta.IdActa)
            .Select(aa => aa.IdAvance)
            .ToListAsync(cancelacion);

        var avances = idsAvances.Count == 0
            ? []
            : await (
                from av in contexto.Avances.AsNoTracking()
                join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
                where idsAvances.Contains(av.IdAvance)
                orderby av.FechaAvance, av.IdAvance
                select new RespuestaAvance(
                    av.IdAvance,
                    av.IdActividad,
                    act.IdObra,
                    act.Codigo,
                    act.Nombre,
                    av.FechaAvance,
                    av.CantidadEjecutada,
                    av.Observaciones,
                    av.Estado,
                    av.IdUsuarioRegistro,
                    av.UuidRegistro,
                    av.CreadoEn,
                    av.SincronizadoEn,
                    false))
                .ToListAsync(cancelacion);

        var evidencias = idsAvances.Count == 0
            ? []
            : await (
                from ev in contexto.Evidencias.AsNoTracking()
                join av in contexto.Avances.AsNoTracking() on ev.IdAvance equals av.IdAvance
                join act in contexto.Actividades.AsNoTracking() on av.IdActividad equals act.IdActividad
                where idsAvances.Contains(ev.IdAvance)
                orderby ev.CapturadaEn, ev.IdEvidencia
                select new RespuestaEvidencia(
                    ev.IdEvidencia,
                    ev.IdAvance,
                    av.IdActividad,
                    act.IdObra,
                    ev.TipoEvidencia,
                    ev.RutaArchivo,
                    ev.HuellaArchivo,
                    ev.CapturadaEn,
                    ev.Latitud,
                    ev.Longitud,
                    ev.IdUsuarioRegistro,
                    ev.UuidRegistro,
                    ev.CreadoEn,
                    ev.SincronizadoEn,
                    false))
                .ToListAsync(cancelacion);

        var idsIncidencias = await contexto.ActasIncidencia.AsNoTracking()
            .Where(ai => ai.IdActa == acta.IdActa)
            .Select(ai => ai.IdIncidencia)
            .ToListAsync(cancelacion);

        var incidenciasEntidades = idsIncidencias.Count == 0
            ? []
            : await contexto.Incidencias.AsNoTracking()
                .Where(i => idsIncidencias.Contains(i.IdIncidencia))
                .OrderBy(i => i.InicioEn)
                .ToListAsync(cancelacion);

        var vinculosIncidencias = idsIncidencias.Count == 0
            ? []
            : await contexto.ActividadesIncidencia.AsNoTracking()
                .Where(ai => idsIncidencias.Contains(ai.IdIncidencia))
                .ToListAsync(cancelacion);

        var mapaActInc = vinculosIncidencias
            .GroupBy(x => x.IdIncidencia)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.IdActividad).OrderBy(x => x).ToList());

        var incidencias = incidenciasEntidades
            .Select(i => MapearIncidencia(i, mapaActInc.GetValueOrDefault(i.IdIncidencia, [])))
            .ToList();

        var participantes = await (
            from ta in contexto.TrabajadoresActa.AsNoTracking()
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on ta.IdAsignacion equals asig.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
            where ta.IdActa == acta.IdActa
            orderby ta.IdAsignacion
            select new RespuestaParticipanteActa(
                ta.IdActa,
                ta.IdAsignacion,
                t.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                ta.TipoParticipacion,
                ta.RutaFirma,
                ta.FirmadoEn))
            .ToListAsync(cancelacion);

        var aprobaciones = await (
            from ap in contexto.AprobacionesActa.AsNoTracking()
            join u in contexto.Usuarios.AsNoTracking() on ap.IdUsuario equals u.IdUsuario
            where ap.IdActa == acta.IdActa
            orderby ap.FechaHora, ap.IdAprobacion
            select new RespuestaAprobacionActa(
                ap.IdAprobacion,
                ap.IdActa,
                ap.IdUsuario,
                u.NombreUsuario,
                ap.Decision,
                ap.Comentario,
                ap.FechaHora))
            .ToListAsync(cancelacion);

        return new RespuestaActa(
            acta.IdActa,
            acta.IdObra,
            codigoObra,
            nombreObra,
            acta.NumeroActa,
            acta.PeriodoInicio,
            acta.PeriodoFin,
            acta.Observaciones,
            acta.MejorasPropuestas,
            acta.SolicitudesCambio,
            acta.Estado,
            acta.IdUsuarioCreador,
            acta.UuidRegistro,
            acta.CreadoEn,
            avances,
            evidencias,
            incidencias,
            participantes,
            aprobaciones);
    }

    private async Task<Dictionary<int, IReadOnlyList<RespuestaParticipanteActividad>>> ConsultarParticipantesActividadAsync(
        int[] idsActividades,
        CancellationToken cancelacion)
    {
        var filas = await (
            from ta in contexto.TrabajadoresActividad.AsNoTracking()
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on ta.IdAsignacion equals asig.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
            where idsActividades.Contains(ta.IdActividad)
            orderby ta.IdAsignacion
            select new RespuestaParticipanteActividad(
                ta.IdActividad,
                ta.IdAsignacion,
                t.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                ta.RolParticipacion))
            .ToListAsync(cancelacion);

        return filas
            .GroupBy(f => f.IdActividad)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RespuestaParticipanteActividad>)g.ToList());
    }

    private static RespuestaActividad MapearActividad(
        Actividad a,
        decimal acumulado,
        IReadOnlyList<RespuestaParticipanteActividad> participantes) =>
        new(
            a.IdActividad,
            a.IdObra,
            a.IdAsignacionResponsable,
            a.Codigo,
            a.Nombre,
            a.Descripcion,
            a.UnidadMedida,
            a.CantidadPlanificada,
            acumulado,
            a.FechaInicioPlan,
            a.FechaFinPlan,
            a.Estado,
            participantes);

    private static RespuestaIncidenciaObra MapearIncidencia(
        Incidencia i,
        IReadOnlyList<int> idsActividades) =>
        new(
            i.IdIncidencia,
            i.IdObra,
            i.TipoIncidencia,
            i.InicioEn,
            i.FinEn,
            i.HorasPerdidas,
            i.Descripcion,
            i.Justificacion,
            i.Estado,
            i.IdUsuarioRegistro,
            i.UuidRegistro,
            i.CreadoEn,
            i.SincronizadoEn,
            idsActividades,
            false);
}
