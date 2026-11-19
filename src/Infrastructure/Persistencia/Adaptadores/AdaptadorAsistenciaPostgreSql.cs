using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Asistencia;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed class AdaptadorAsistenciaPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj,
    ICalendarioLaboral calendario,
    PaginacionConsulta paginacion) : IPuertoAsistencia
{
    public async Task BloquearMarcacionesAsync(IReadOnlyList<int> idsAsignaciones, CancellationToken cancelacion = default)
    {
        if (contexto.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La marcación requiere una transacción activa.");
        var trabajadores = await contexto.AsignacionesTrabajadorObra.AsNoTracking()
            .Where(a => idsAsignaciones.Contains(a.IdAsignacion)).Select(a => a.IdTrabajador)
            .Distinct().Order().ToListAsync(cancelacion);
        // El orden estable evita interbloqueos en listas asistidas. El bloqueo dura hasta COMMIT/ROLLBACK.
        foreach (var idTrabajador in trabajadores)
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext('constructora.asistencia'), {idTrabajador})", cancelacion);
    }

    public async Task BloquearAsistenciaAsync(long idAsistencia, CancellationToken cancelacion = default)
    {
        var asignacion = await contexto.Asistencias.AsNoTracking().Where(a => a.IdAsistencia == idAsistencia)
            .Select(a => (int?)a.IdAsignacion).FirstOrDefaultAsync(cancelacion);
        if (asignacion is { } id) await BloquearMarcacionesAsync([id], cancelacion);
    }

    public async Task<IReadOnlyList<RespuestaAsignacionAsistencia>> ListarAsignacionesPropiasAsync(
        int idTrabajador, DateOnly fecha, CancellationToken cancelacion = default) =>
        await (from a in contexto.AsignacionesTrabajadorObra.AsNoTracking()
            join o in contexto.Obras.AsNoTracking() on a.IdObra equals o.IdObra
            join t in contexto.Trabajadores.AsNoTracking() on a.IdTrabajador equals t.IdTrabajador
            where a.IdTrabajador == idTrabajador && t.Estado == EstadosTrabajador.Activo
                && a.Estado == EstadosAsignacionObra.Activa && a.FechaInicio <= fecha
                && (a.FechaFin == null || a.FechaFin >= fecha)
            orderby a.IdAsignacion
            select new RespuestaAsignacionAsistencia(a.IdAsignacion, o.IdObra, o.Codigo, o.Nombre, o.Direccion,
                o.Estado, o.Latitud, o.Longitud, o.RadioAsistenciaMetros, a.FechaInicio, a.FechaFin,
                contexto.Asistencias.Where(r => r.IdAsignacion == a.IdAsignacion && r.SalidaEn == null
                    && r.Estado != EstadosAsistencia.Anulada).Select(r => (long?)r.IdAsistencia).FirstOrDefault()))
            .Buscar(paginacion, a => a.CodigoObra, a => a.NombreObra, a => a.Direccion, a => a.EstadoObra)
            .Paginar(paginacion).ToListAsync(cancelacion);

    public Task<bool> ExisteSolapamientoAsync(int idTrabajador, DateTime entradaEn, DateTime? salidaEn,
        long? excluirIdAsistencia = null, CancellationToken cancelacion = default) =>
        (from a in contexto.Asistencias.AsNoTracking()
         join asignacion in contexto.AsignacionesTrabajadorObra on a.IdAsignacion equals asignacion.IdAsignacion
         where asignacion.IdTrabajador == idTrabajador && a.Estado != EstadosAsistencia.Anulada
             && (excluirIdAsistencia == null || a.IdAsistencia != excluirIdAsistencia)
             && (salidaEn == null || a.EntradaEn < salidaEn)
             && (a.SalidaEn == null || a.SalidaEn > entradaEn)
         select a.IdAsistencia).AnyAsync(cancelacion);

    public async Task<IReadOnlyList<RespuestaCorreccionAsistencia>> ListarCorreccionesAsync(
        int? idObra, string? estado, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default)
    {
        var obras = idsObrasPermitidas?.ToArray();
        return await (from c in contexto.CorreccionesAsistencia.AsNoTracking()
            join a in contexto.Asistencias on c.IdAsistencia equals a.IdAsistencia
            join asignacion in contexto.AsignacionesTrabajadorObra on a.IdAsignacion equals asignacion.IdAsignacion
            where (idObra == null || asignacion.IdObra == idObra) && (estado == null || c.Estado == estado)
                && (obras == null || obras.Contains(asignacion.IdObra))
            orderby c.SolicitadoEn descending, c.IdCorreccion descending
            select new RespuestaCorreccionAsistencia(c.IdCorreccion, c.IdAsistencia, asignacion.IdObra,
                c.IdUsuarioSolicita, c.IdUsuarioAprueba, c.EntradaAnterior, c.SalidaAnterior,
                c.EntradaNueva, c.SalidaNueva, c.Motivo, c.Estado, c.SolicitadoEn, c.ResueltoEn))
            .Buscar(paginacion, c => c.Motivo, c => c.Estado)
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public async Task<RespuestaAsistencia?> ObtenerPorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default) =>
        await ConsultarAsistenciasBase(contexto.Asistencias.AsNoTracking().Where(a => a.UuidRegistro == uuidRegistro))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaAsistencia?> ObtenerPorIdAsync(
        long idAsistencia,
        CancellationToken cancelacion = default) =>
        await ConsultarAsistenciasBase(contexto.Asistencias.AsNoTracking().Where(a => a.IdAsistencia == idAsistencia))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<ContextoAsignacionObra?> ObtenerContextoAsignacionAsync(
        int idAsignacion, CancellationToken cancelacion = default)
    {
        var fila = await (
            from a in contexto.AsignacionesTrabajadorObra.AsNoTracking()
            join t in contexto.Trabajadores.AsNoTracking() on a.IdTrabajador equals t.IdTrabajador
            where a.IdAsignacion == idAsignacion
            select new
            {
                a.IdObra,
                a.IdTrabajador,
                EstadoAsignacion = a.Estado,
                EstadoTrabajador = t.Estado,
                a.FechaInicio,
                a.FechaFin
            }).FirstOrDefaultAsync(cancelacion);
        return fila is null ? null : new ContextoAsignacionObra(fila.IdObra, fila.IdTrabajador,
            fila.EstadoAsignacion, fila.EstadoTrabajador, fila.FechaInicio, fila.FechaFin);
    }

    public Task<bool> ExisteMarcacionMismaEntradaAsync(
        int idAsignacion,
        DateTime entradaEn,
        CancellationToken cancelacion = default) =>
        contexto.Asistencias.AnyAsync(
            a => a.IdAsignacion == idAsignacion && a.EntradaEn == entradaEn,
            cancelacion);

    public async Task<RespuestaAsistencia> RegistrarAsistenciaAsync(
        SolicitudRegistrarAsistencia solicitud,
        string estado,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var registro = new RegistroAsistencia
        {
            IdAsignacion = solicitud.IdAsignacion,
            FechaJornada = solicitud.FechaJornada,
            EntradaEn = solicitud.EntradaEn,
            SalidaEn = solicitud.SalidaEn,
            LatitudEntrada = solicitud.LatitudEntrada,
            LongitudEntrada = solicitud.LongitudEntrada,
            LatitudSalida = solicitud.LatitudSalida,
            LongitudSalida = solicitud.LongitudSalida,
            PrecisionGpsEntradaMetros = solicitud.PrecisionGpsEntradaMetros,
            PrecisionGpsSalidaMetros = solicitud.PrecisionGpsSalidaMetros,
            Estado = estado,
            IdUsuarioRegistro = idUsuarioRegistro,
            UuidRegistro = uuidRegistro,
            CreadoEn = creadoEnUtc,
            SincronizadoEn = sincronizadoEnUtc
        };

        contexto.Asistencias.Add(registro);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = registro.IdAsistencia.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerPorIdAsync(registro.IdAsistencia, cancelacion))!;
    }

    public async Task<RespuestaAsistencia?> RegistrarSalidaAsistenciaAsync(
        long idAsistencia,
        DateTime salidaEn,
        decimal? latitudSalida,
        decimal? longitudSalida,
        decimal? precisionGpsSalidaMetros,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var registro = await contexto.Asistencias
            .FirstOrDefaultAsync(a => a.IdAsistencia == idAsistencia, cancelacion);
        if (registro is null)
        {
            return null;
        }

        registro.SalidaEn = salidaEn;
        registro.Estado = EstadosAsistencia.Completa;
        registro.LatitudSalida = latitudSalida;
        registro.LongitudSalida = longitudSalida;
        registro.PrecisionGpsSalidaMetros = precisionGpsSalidaMetros;
        if (sincronizadoEnUtc.HasValue) registro.SincronizadoEn = sincronizadoEnUtc;

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerPorIdAsync(idAsistencia, cancelacion);
    }

    public async Task<RespuestaCorreccionAsistencia> CrearCorreccionAsistenciaAsync(
        long idAsistencia,
        int idUsuarioSolicita,
        DateTime? entradaAnterior,
        DateTime? salidaAnterior,
        DateTime? entradaNueva,
        DateTime? salidaNueva,
        string motivo,
        DateTime solicitadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var correccion = new CorreccionAsistencia
        {
            IdAsistencia = idAsistencia,
            IdUsuarioSolicita = idUsuarioSolicita,
            IdUsuarioAprueba = null,
            EntradaAnterior = entradaAnterior,
            SalidaAnterior = salidaAnterior,
            EntradaNueva = entradaNueva,
            SalidaNueva = salidaNueva,
            Motivo = motivo,
            Estado = EstadosCorreccionAsistencia.Pendiente,
            SolicitadoEn = solicitadoEnUtc,
            ResueltoEn = null
        };

        contexto.CorreccionesAsistencia.Add(correccion);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = correccion.IdCorreccion.ToString() },
            solicitadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerCorreccionPorIdAsync(correccion.IdCorreccion, cancelacion))!;
    }

    public async Task<RespuestaCorreccionAsistencia?> ObtenerCorreccionPorIdAsync(
        long idCorreccion,
        CancellationToken cancelacion = default) =>
        await (
            from c in contexto.CorreccionesAsistencia.AsNoTracking()
            join a in contexto.Asistencias.AsNoTracking() on c.IdAsistencia equals a.IdAsistencia
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on a.IdAsignacion equals asig.IdAsignacion
            where c.IdCorreccion == idCorreccion
            select new RespuestaCorreccionAsistencia(
                c.IdCorreccion,
                c.IdAsistencia,
                asig.IdObra,
                c.IdUsuarioSolicita,
                c.IdUsuarioAprueba,
                c.EntradaAnterior,
                c.SalidaAnterior,
                c.EntradaNueva,
                c.SalidaNueva,
                c.Motivo,
                c.Estado,
                c.SolicitadoEn,
                c.ResueltoEn))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaCorreccionAsistencia?> ResolverCorreccionAsistenciaAsync(
        long idCorreccion,
        int idUsuarioAprueba,
        string nuevoEstado,
        DateTime resueltoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var correccion = await contexto.CorreccionesAsistencia
            .FirstOrDefaultAsync(c => c.IdCorreccion == idCorreccion, cancelacion);
        if (correccion is null)
        {
            return null;
        }

        correccion.IdUsuarioAprueba = idUsuarioAprueba;
        correccion.Estado = nuevoEstado;
        correccion.ResueltoEn = resueltoEnUtc;

        if (nuevoEstado == EstadosCorreccionAsistencia.Aprobada)
        {
            var asistencia = await contexto.Asistencias
                .FirstOrDefaultAsync(a => a.IdAsistencia == correccion.IdAsistencia, cancelacion);
            if (asistencia is not null)
            {
                if (asistencia.EntradaEn != correccion.EntradaAnterior || asistencia.SalidaEn != correccion.SalidaAnterior)
                    throw new ExcepcionPersistencia(ErrorNegocio.Conflicto("correccion_desactualizada",
                        "La asistencia cambió después de solicitar esta corrección. Revise sus horas antes de resolverla."));
                var idTrabajador = await contexto.AsignacionesTrabajadorObra.Where(a => a.IdAsignacion == asistencia.IdAsignacion)
                    .Select(a => a.IdTrabajador).SingleAsync(cancelacion);
                if (await ExisteSolapamientoAsync(idTrabajador, correccion.EntradaNueva ?? asistencia.EntradaEn,
                    correccion.SalidaNueva ?? asistencia.SalidaEn, asistencia.IdAsistencia, cancelacion))
                    throw new ExcepcionPersistencia(ErrorNegocio.Conflicto("asistencia_solapada",
                        "La corrección se superpone con otra asistencia del trabajador."));
                if (correccion.EntradaNueva is not null)
                {
                    asistencia.EntradaEn = correccion.EntradaNueva.Value;
                    asistencia.FechaJornada = calendario.ObtenerFecha(asistencia.EntradaEn);
                }

                if (correccion.SalidaNueva is not null)
                {
                    asistencia.SalidaEn = correccion.SalidaNueva.Value;
                }

                asistencia.Estado = EstadosAsistencia.Corregida;
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, resueltoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerCorreccionPorIdAsync(idCorreccion, cancelacion);
    }

    private IQueryable<RegistroAsistencia> FiltrarRegistros(FiltroConsolidadoAsistencia filtro, IReadOnlySet<int>? obras)
    {
        var consulta = contexto.Asistencias.AsNoTracking().Where(a => a.Estado != EstadosAsistencia.Anulada);
        if (filtro.Estado is not null) consulta = consulta.Where(a => a.Estado == filtro.Estado);
        var permitidas = obras?.ToArray();
        consulta = consulta.Where(a => contexto.AsignacionesTrabajadorObra.Any(asig =>
            asig.IdAsignacion == a.IdAsignacion
            && (filtro.IdObra == null || asig.IdObra == filtro.IdObra)
            && (filtro.IdTrabajador == null || asig.IdTrabajador == filtro.IdTrabajador)
            && (permitidas == null || permitidas.Contains(asig.IdObra))));
        if (filtro.FechaDesde is { } desde) consulta = consulta.Where(a => a.FechaJornada >= desde);
        if (filtro.FechaHasta is { } hasta) consulta = consulta.Where(a => a.FechaJornada <= hasta);
        if (filtro.IdCuadrilla is { } cuadrilla)
            consulta = consulta.Where(a =>
                (from tc in contexto.TrabajadoresCuadrilla
                 join co in contexto.CuadrillasObra on tc.IdCuadrillaObra equals co.IdCuadrillaObra
                 where tc.IdAsignacion == a.IdAsignacion && co.IdCuadrilla == cuadrilla
                    && tc.FechaInicio <= a.FechaJornada && (tc.FechaFin == null || tc.FechaFin >= a.FechaJornada)
                 select tc.IdTrabajadorCuadrilla).Any());
        return consulta;
    }

    public async Task<IReadOnlyList<RespuestaAsistencia>> ListarRegistrosAsync(FiltroConsolidadoAsistencia filtro,
        IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default) =>
        await ConsultarAsistenciasBase(FiltrarRegistros(filtro, idsObrasPermitidas)
            .OrderByDescending(a => a.FechaJornada).ThenByDescending(a => a.IdAsistencia))
            .Buscar(paginacion, a => a.NombreCompletoTrabajador, a => a.Estado)
            .Paginar(paginacion)
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<ItemConsolidadoAsistencia>> ConsultarConsolidadoAsync(
        FiltroConsolidadoAsistencia filtro, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default)
    {
        var consulta =
            from a in FiltrarRegistros(filtro, idsObrasPermitidas)
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on a.IdAsignacion equals asig.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
            join o in contexto.Obras.AsNoTracking() on asig.IdObra equals o.IdObra
            group a by new
            {
                o.IdObra,
                CodigoObra = o.Codigo,
                NombreObra = o.Nombre,
                t.IdTrabajador,
                t.Cedula,
                NombreTrabajador = t.PrimerNombre + " " + t.PrimerApellido
            } into g
            orderby g.Key.IdObra, g.Key.IdTrabajador
            select new
            {
                g.Key,
                Total = g.Count(),
                Completas = g.Count(a => a.SalidaEn != null),
                Abiertas = g.Count(a => a.SalidaEn == null),
                Corregidas = g.Count(a => a.Estado == EstadosAsistencia.Corregida),
                Horas = g.Sum(a => a.SalidaEn != null ? (decimal)(a.SalidaEn.Value - a.EntradaEn).TotalHours : 0m)
            };
        var filas = await consulta.Buscar(paginacion, x => x.Key.CodigoObra, x => x.Key.NombreObra, x => x.Key.NombreTrabajador, x => x.Key.Cedula)
            .Paginar(paginacion).ToListAsync(cancelacion);
        var ids = filas.Select(f => f.Key.IdTrabajador).ToArray();
        var cuadrillas = await (
            from tc in contexto.TrabajadoresCuadrilla.AsNoTracking()
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on tc.IdAsignacion equals asig.IdAsignacion
            join co in contexto.CuadrillasObra.AsNoTracking() on tc.IdCuadrillaObra equals co.IdCuadrillaObra
            join c in contexto.Cuadrillas.AsNoTracking() on co.IdCuadrilla equals c.IdCuadrilla
            where ids.Contains(asig.IdTrabajador) && (filtro.IdCuadrilla == null || c.IdCuadrilla == filtro.IdCuadrilla)
                && (filtro.FechaHasta == null || tc.FechaInicio <= filtro.FechaHasta)
                && (filtro.FechaDesde == null || tc.FechaFin == null || tc.FechaFin >= filtro.FechaDesde)
            orderby tc.FechaInicio descending, tc.IdTrabajadorCuadrilla descending
            select new { asig.IdTrabajador, asig.IdObra, c.IdCuadrilla, c.Nombre }).ToListAsync(cancelacion);
        var mapa = cuadrillas.GroupBy(c => (c.IdObra, c.IdTrabajador)).ToDictionary(g => g.Key, g => g.First());
        return filas.Select(f =>
        {
            var cuadrilla = mapa.GetValueOrDefault((f.Key.IdObra, f.Key.IdTrabajador));
            return new ItemConsolidadoAsistencia(f.Key.IdObra, f.Key.CodigoObra, f.Key.NombreObra,
                f.Key.IdTrabajador, f.Key.Cedula, f.Key.NombreTrabajador, cuadrilla?.IdCuadrilla, cuadrilla?.Nombre,
                f.Total, f.Completas, f.Abiertas, f.Corregidas, decimal.Round(f.Horas, 2));
        }).ToList();
    }

    private IQueryable<RespuestaAsistencia> ConsultarAsistenciasBase(IQueryable<RegistroAsistencia> origen) =>
        from a in origen
        join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on a.IdAsignacion equals asig.IdAsignacion
        join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
        select new RespuestaAsistencia(
            a.IdAsistencia,
            a.IdAsignacion,
            t.IdTrabajador,
            t.PrimerNombre + " " + t.PrimerApellido,
            asig.IdObra,
            a.FechaJornada,
            a.EntradaEn,
            a.SalidaEn,
            a.LatitudEntrada,
            a.LongitudEntrada,
            a.LatitudSalida,
            a.LongitudSalida,
            a.Estado,
            a.IdUsuarioRegistro,
            a.UuidRegistro,
            a.CreadoEn,
            a.SincronizadoEn,
            false,
            a.PrecisionGpsEntradaMetros,
            a.PrecisionGpsSalidaMetros);
}
