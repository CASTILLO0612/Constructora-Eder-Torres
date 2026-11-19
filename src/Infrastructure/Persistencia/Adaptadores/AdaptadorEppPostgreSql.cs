using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.EPP;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed partial class AdaptadorEppPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj,
    PaginacionConsulta paginacion) : IPuertoEpp
{
    public Task<ContextoMovimientoEpp?> ObtenerMovimientoPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default) =>
        contexto.MovimientosExistenciasEpp.AsNoTracking().Where(m => m.UuidRegistro == uuidRegistro)
            .Select(m => new ContextoMovimientoEpp(m.IdEntregaEpp, m.TipoMovimiento, m.FechaHora, m.Observacion))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<IReadOnlyList<RespuestaEntregaEpp>> ListarEntregasAsync(int? idObra, int? idTrabajador,
        string? estado, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default)
    {
        var obras = idsObrasPermitidas?.ToArray();
        var origen = contexto.EntregasEpp.AsNoTracking()
            .Where(e => (estado == null || e.Estado == estado) && contexto.AsignacionesTrabajadorObra.Any(a =>
                a.IdAsignacion == e.IdAsignacion && (idObra == null || a.IdObra == idObra)
                && (idTrabajador == null || a.IdTrabajador == idTrabajador) && (obras == null || obras.Contains(a.IdObra))))
            .OrderByDescending(e => e.EntregadoEn).ThenByDescending(e => e.IdEntregaEpp).Paginar(paginacion);
        return await ConsultarEntregasBase(origen).ToListAsync(cancelacion);
    }

    public async Task<IReadOnlyList<RespuestaReposicionEpp>> ListarReposicionesAsync(int? idObra,
        string? estado, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default)
    {
        var obras = idsObrasPermitidas?.ToArray();
        var origen = contexto.ReposicionesEpp.AsNoTracking()
            .Where(r => (estado == null || r.Estado == estado)
                && (from e in contexto.EntregasEpp
                    join a in contexto.AsignacionesTrabajadorObra on e.IdAsignacion equals a.IdAsignacion
                    where e.IdEntregaEpp == r.IdEntregaOrigen && (idObra == null || a.IdObra == idObra)
                        && (obras == null || obras.Contains(a.IdObra)) select e.IdEntregaEpp).Any())
            .OrderByDescending(r => r.SolicitadaEn).ThenByDescending(r => r.IdReposicionEpp).Paginar(paginacion);
        return await ConsultarReposicionesBase(origen).ToListAsync(cancelacion);
    }

    public Task<bool> ExisteTipoEppPorNombreAsync(string nombre, CancellationToken cancelacion = default) =>
        contexto.TiposEpp.AnyAsync(t => t.Nombre.ToLower() == nombre.ToLower(), cancelacion);

    public async Task<RespuestaTipoEpp?> ObtenerTipoEppPorIdAsync(int idTipoEpp, CancellationToken cancelacion = default) =>
        await contexto.TiposEpp.AsNoTracking()
            .Where(t => t.IdTipoEpp == idTipoEpp)
            .Select(t => new RespuestaTipoEpp(t.IdTipoEpp, t.Nombre, t.Descripcion, t.VidaUtilDias, t.ExistenciaMinima, t.Activo))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaTipoEpp> CrearTipoEppAsync(SolicitudCrearTipoEpp solicitud, CancellationToken cancelacion = default)
    {
        var tipo = new TipoEpp
        {
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            VidaUtilDias = solicitud.VidaUtilDias,
            ExistenciaMinima = solicitud.ExistenciaMinima,
            Activo = true
        };

        contexto.TiposEpp.Add(tipo);
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaTipoEpp(tipo.IdTipoEpp, tipo.Nombre, tipo.Descripcion, tipo.VidaUtilDias, tipo.ExistenciaMinima, tipo.Activo);
    }

    public async Task<IReadOnlyList<RespuestaTipoEpp>> ListarTiposEppAsync(CancellationToken cancelacion = default) =>
        await contexto.TiposEpp.AsNoTracking()
            .OrderBy(t => t.IdTipoEpp)
            .Select(t => new RespuestaTipoEpp(t.IdTipoEpp, t.Nombre, t.Descripcion, t.VidaUtilDias, t.ExistenciaMinima, t.Activo))
            .Paginar(paginacion).ToListAsync(cancelacion);

    public Task<bool> ExisteEppPorCodigoAsync(string codigoInterno, CancellationToken cancelacion = default) =>
        contexto.EquiposProteccionPersonal.AnyAsync(e => e.CodigoInterno.ToLower() == codigoInterno.ToLower(), cancelacion);

    public Task<bool> ExisteEppPorSerieAsync(string numeroSerie, CancellationToken cancelacion = default) =>
        contexto.EquiposProteccionPersonal.AnyAsync(
            e => e.NumeroSerie != null && e.NumeroSerie.ToLower() == numeroSerie.ToLower(),
            cancelacion);

    public async Task<RespuestaEpp?> ObtenerEppPorIdAsync(int idEpp, CancellationToken cancelacion = default) =>
        await (
            from e in contexto.EquiposProteccionPersonal.AsNoTracking()
            join t in contexto.TiposEpp.AsNoTracking() on e.IdTipoEpp equals t.IdTipoEpp
            where e.IdEpp == idEpp
            select new RespuestaEpp(
                e.IdEpp,
                e.IdTipoEpp,
                t.Nombre,
                e.CodigoInterno,
                e.NumeroSerie,
                e.FechaAdquisicion,
                e.Estado,
                e.Condicion))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaEpp> RegistrarEntradaEppAsync(
        SolicitudRegistrarEntradaEpp solicitud,
        int idUsuario,
        DateTime fechaHoraUtc,
        Guid uuidMovimiento,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var epp = new Epp
        {
            IdTipoEpp = solicitud.IdTipoEpp,
            CodigoInterno = solicitud.CodigoInterno,
            NumeroSerie = solicitud.NumeroSerie,
            FechaAdquisicion = solicitud.FechaAdquisicion,
            Estado = EstadosEpp.Disponible,
            Condicion = solicitud.Condicion
        };

        contexto.EquiposProteccionPersonal.Add(epp);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.MovimientosExistenciasEpp.Add(new MovimientoExistenciasEpp
        {
            IdEpp = epp.IdEpp,
            IdEntregaEpp = null,
            TipoMovimiento = TiposMovimientoEpp.Entrada,
            FechaHora = fechaHoraUtc,
            IdUsuario = idUsuario,
            Observacion = string.IsNullOrWhiteSpace(solicitud.Observacion)
                ? "Ingreso inicial a almacen"
                : solicitud.Observacion.Trim(),
            UuidRegistro = uuidMovimiento
        });

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = epp.IdEpp.ToString() },
            fechaHoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerEppPorIdAsync(epp.IdEpp, cancelacion))!;
    }

    public async Task<IReadOnlyList<RespuestaEpp>> ConsultarExistenciasEppAsync(
        int? idTipoEpp,
        string? estado,
        string? condicion,
        CancellationToken cancelacion = default)
    {
        return await (
            from e in contexto.EquiposProteccionPersonal.AsNoTracking()
            join t in contexto.TiposEpp.AsNoTracking() on e.IdTipoEpp equals t.IdTipoEpp
            where (idTipoEpp == null || e.IdTipoEpp == idTipoEpp.Value)
                  && (string.IsNullOrWhiteSpace(estado) || e.Estado == estado)
                  && (string.IsNullOrWhiteSpace(condicion) || e.Condicion == condicion)
            orderby e.IdEpp
            select new RespuestaEpp(
                e.IdEpp,
                e.IdTipoEpp,
                t.Nombre,
                e.CodigoInterno,
                e.NumeroSerie,
                e.FechaAdquisicion,
                e.Estado,
                e.Condicion))
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public async Task<IReadOnlyList<ResumenExistenciaTipoEpp>> ConsultarResumenExistenciasAsync(
        int? idTipoEpp = null, CancellationToken cancelacion = default) =>
        await contexto.TiposEpp.AsNoTracking()
            .Where(t => idTipoEpp == null || t.IdTipoEpp == idTipoEpp)
            .OrderBy(t => t.IdTipoEpp).Paginar(paginacion)
            .Select(t => new ResumenExistenciaTipoEpp(t.IdTipoEpp, t.Nombre, t.ExistenciaMinima,
                contexto.EquiposProteccionPersonal.Count(e => e.IdTipoEpp == t.IdTipoEpp && e.Estado == EstadosEpp.Disponible),
                contexto.EquiposProteccionPersonal.Count(e => e.IdTipoEpp == t.IdTipoEpp && e.Estado == EstadosEpp.Entregado),
                contexto.EquiposProteccionPersonal.Count(e => e.IdTipoEpp == t.IdTipoEpp && e.Estado == EstadosEpp.EnRevision),
                contexto.EquiposProteccionPersonal.Count(e => e.IdTipoEpp == t.IdTipoEpp && e.Estado == EstadosEpp.Perdido),
                contexto.EquiposProteccionPersonal.Count(e => e.IdTipoEpp == t.IdTipoEpp && e.Estado == EstadosEpp.Retirado),
                contexto.EquiposProteccionPersonal.Count(e => e.IdTipoEpp == t.IdTipoEpp && e.Estado == EstadosEpp.Disponible) < t.ExistenciaMinima))
            .ToListAsync(cancelacion);

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

    public async Task<RespuestaEntregaEpp?> ObtenerEntregaPorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default) =>
        await ConsultarEntregasBase(contexto.EntregasEpp.AsNoTracking().Where(e => e.UuidRegistro == uuidRegistro))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaEntregaEpp?> ObtenerEntregaPorIdAsync(
        long idEntregaEpp,
        CancellationToken cancelacion = default) =>
        await ConsultarEntregasBase(contexto.EntregasEpp.AsNoTracking().Where(e => e.IdEntregaEpp == idEntregaEpp))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaEntregaEpp> RegistrarEntregaEppAsync(
        SolicitudRegistrarEntregaEpp solicitud,
        string condicionEntrega,
        int idUsuarioEntrega,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var epp = await contexto.EquiposProteccionPersonal
            .FirstAsync(e => e.IdEpp == solicitud.IdEpp, cancelacion);

        epp.Estado = EstadosEpp.Entregado;

        var entrega = new EntregaEpp
        {
            IdEpp = solicitud.IdEpp,
            IdAsignacion = solicitud.IdAsignacion,
            IdUsuarioEntrega = idUsuarioEntrega,
            EntregadoEn = solicitud.EntregadoEn,
            CondicionEntrega = condicionEntrega,
            DevueltoEn = null,
            CondicionDevolucion = null,
            Estado = EstadosEntregaEpp.Activa,
            RutaConstancia = string.IsNullOrWhiteSpace(solicitud.RutaConstancia) ? null : solicitud.RutaConstancia.Trim(),
            UuidRegistro = uuidRegistro,
            CreadoEn = creadoEnUtc,
            SincronizadoEn = sincronizadoEnUtc
        };

        contexto.EntregasEpp.Add(entrega);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.MovimientosExistenciasEpp.Add(new MovimientoExistenciasEpp
        {
            IdEpp = epp.IdEpp,
            IdEntregaEpp = entrega.IdEntregaEpp,
            TipoMovimiento = TiposMovimientoEpp.Entrega,
            FechaHora = solicitud.EntregadoEn,
            IdUsuario = idUsuarioEntrega,
            Observacion = $"Entrega a asignacion #{solicitud.IdAsignacion}",
            UuidRegistro = Guid.NewGuid()
        });

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = entrega.IdEntregaEpp.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerEntregaPorIdAsync(entrega.IdEntregaEpp, cancelacion))!;
    }

    public async Task<RespuestaEntregaEpp?> RegistrarDevolucionEppAsync(
        long idEntregaEpp,
        SolicitudDevolverEpp solicitud,
        int idUsuario,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var entrega = await contexto.EntregasEpp
            .FirstOrDefaultAsync(e => e.IdEntregaEpp == idEntregaEpp, cancelacion);
        if (entrega is null)
        {
            return null;
        }

        var condicion = solicitud.CondicionDevolucion.Trim().ToUpperInvariant();
        entrega.DevueltoEn = solicitud.DevueltoEn;
        entrega.CondicionDevolucion = condicion;
        entrega.Estado = EstadosEntregaEpp.Devuelta;

        var epp = await contexto.EquiposProteccionPersonal
            .FirstAsync(e => e.IdEpp == entrega.IdEpp, cancelacion);

        epp.Condicion = condicion;
        epp.Estado = condicion == CondicionesEpp.Danado
            ? EstadosEpp.EnRevision
            : EstadosEpp.Disponible;

        contexto.MovimientosExistenciasEpp.Add(new MovimientoExistenciasEpp
        {
            IdEpp = epp.IdEpp,
            IdEntregaEpp = entrega.IdEntregaEpp,
            TipoMovimiento = TiposMovimientoEpp.Devolucion,
            FechaHora = solicitud.DevueltoEn,
            IdUsuario = idUsuario,
            Observacion = string.IsNullOrWhiteSpace(solicitud.Observacion)
                ? $"Devolucion en condicion {condicion}"
                : solicitud.Observacion.Trim(),
            UuidRegistro = solicitud.UuidRegistro!.Value
        });

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerEntregaPorIdAsync(idEntregaEpp, cancelacion);
    }

    public async Task<RespuestaReposicionEpp?> ObtenerReposicionPorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default) =>
        await ConsultarReposicionesBase(contexto.ReposicionesEpp.AsNoTracking().Where(r => r.UuidRegistro == uuidRegistro))
            .FirstOrDefaultAsync(cancelacion);

    public Task<RespuestaReposicionEpp?> ObtenerReposicionPorIdAsync(long idReposicion, CancellationToken cancelacion = default) =>
        ConsultarReposicionesBase(contexto.ReposicionesEpp.Where(r => r.IdReposicionEpp == idReposicion)).FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaReposicionEpp> RegistrarReposicionEppAsync(
        SolicitudRegistrarReposicionEpp solicitud, int idUsuario, DateTime ahoraUtc, Guid uuidRegistro,
        DateTime? sincronizadoEnUtc, RegistroEventoAuditoria auditoria, long? idReposicionExistente,
        CancellationToken cancelacion = default)
    {
        var reposicion = idReposicionExistente is { } id
            ? await contexto.ReposicionesEpp.SingleAsync(r => r.IdReposicionEpp == id, cancelacion)
            : new ReposicionEpp
            {
                IdEntregaOrigen = solicitud.IdEntregaOrigen,
                Motivo = solicitud.Motivo,
                Descripcion = solicitud.Descripcion,
                IdUsuarioSolicita = idUsuario,
                SolicitadaEn = solicitud.SolicitadaEn ?? ahoraUtc,
                UuidRegistro = uuidRegistro,
                Estado = EstadosReposicionEpp.Pendiente
            };
        if (idReposicionExistente is null) contexto.ReposicionesEpp.Add(reposicion);
        reposicion.EquipoDevuelto = solicitud.EquipoDevuelto;

        if (solicitud.IdEppSustituto is { } idSustituto)
        {
            var origen = await contexto.EntregasEpp.SingleAsync(e => e.IdEntregaEpp == solicitud.IdEntregaOrigen, cancelacion);
            var anterior = await contexto.EquiposProteccionPersonal.SingleAsync(e => e.IdEpp == origen.IdEpp, cancelacion);
            var sustituto = await contexto.EquiposProteccionPersonal.SingleAsync(e => e.IdEpp == idSustituto, cancelacion);
            var fecha = solicitud.SolicitadaEn ?? ahoraUtc;
            var perdida = solicitud.Motivo == MotivosReposicionEpp.Perdida;
            origen.Estado = perdida ? EstadosEntregaEpp.Perdida : EstadosEntregaEpp.Reemplazada;
            if (solicitud.EquipoDevuelto)
            {
                origen.DevueltoEn = fecha;
                origen.CondicionDevolucion = CondicionesEpp.Danado;
            }
            anterior.Condicion = CondicionesEpp.Danado;
            anterior.Estado = perdida ? EstadosEpp.Perdido
                : solicitud.Motivo == MotivosReposicionEpp.Vencimiento ? EstadosEpp.Retirado : EstadosEpp.EnRevision;
            sustituto.Estado = EstadosEpp.Entregado;
            var nueva = new EntregaEpp
            {
                IdEpp = sustituto.IdEpp,
                IdAsignacion = origen.IdAsignacion,
                IdUsuarioEntrega = idUsuario,
                EntregadoEn = fecha,
                CondicionEntrega = sustituto.Condicion,
                Estado = EstadosEntregaEpp.Activa,
                UuidRegistro = Guid.NewGuid(),
                CreadoEn = ahoraUtc,
                SincronizadoEn = sincronizadoEnUtc
            };
            contexto.EntregasEpp.Add(nueva);
            await contexto.SaveChangesAsync(cancelacion);
            contexto.MovimientosExistenciasEpp.AddRange(
                new MovimientoExistenciasEpp
                {
                    IdEpp = anterior.IdEpp,
                    IdEntregaEpp = origen.IdEntregaEpp,
                    TipoMovimiento = perdida ? TiposMovimientoEpp.Perdida
                        : solicitud.Motivo == MotivosReposicionEpp.Vencimiento ? TiposMovimientoEpp.Retiro : TiposMovimientoEpp.Devolucion,
                    FechaHora = fecha,
                    IdUsuario = idUsuario,
                    Observacion = "Reposición: " + solicitud.Descripcion,
                    UuidRegistro = Guid.NewGuid()
                },
                new MovimientoExistenciasEpp
                {
                    IdEpp = sustituto.IdEpp,
                    IdEntregaEpp = nueva.IdEntregaEpp,
                    TipoMovimiento = TiposMovimientoEpp.Entrega,
                    FechaHora = fecha,
                    IdUsuario = idUsuario,
                    Observacion = "Entrega sustituta",
                    UuidRegistro = Guid.NewGuid()
                });
            reposicion.IdEntregaNueva = nueva.IdEntregaEpp;
            reposicion.IdUsuarioAprueba = idUsuario;
            reposicion.ResueltaEn = fecha;
            reposicion.Estado = EstadosReposicionEpp.Ejecutada;
        }

        await contexto.SaveChangesAsync(cancelacion);
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria with
        {
            IdRegistro = reposicion.IdReposicionEpp.ToString(),
            Accion = idReposicionExistente is null ? AccionesAuditoria.Crear : AccionesAuditoria.Modificar,
            UuidOperacion = Guid.NewGuid()
        }, ahoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return (await ObtenerReposicionPorIdAsync(reposicion.IdReposicionEpp, cancelacion))!;
    }

    public async Task<IReadOnlyList<EventoHistorialEppTrabajador>> ConsultarHistorialPorTrabajadorAsync(
        int idTrabajador, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default)
    {
        var ids = idsObrasPermitidas?.ToArray();
        var consulta =
            from ent in contexto.EntregasEpp.AsNoTracking()
            join asig in contexto.AsignacionesTrabajadorObra on ent.IdAsignacion equals asig.IdAsignacion
            join o in contexto.Obras on asig.IdObra equals o.IdObra
            join e in contexto.EquiposProteccionPersonal on ent.IdEpp equals e.IdEpp
            join t in contexto.TiposEpp on e.IdTipoEpp equals t.IdTipoEpp
            where asig.IdTrabajador == idTrabajador && (ids == null || ids.Contains(o.IdObra))
            select new { Entrega = ent, Obra = o, Epp = e, Tipo = t };
        var entregas = consulta.Select(x => new
        {
            Fecha = x.Entrega.EntregadoEn,
            TipoEvento = "ENTREGA",
            Id = x.Entrega.IdEntregaEpp,
            x.Obra.IdObra,
            CodigoObra = x.Obra.Codigo,
            NombreObra = x.Obra.Nombre,
            x.Epp.IdEpp,
            x.Epp.CodigoInterno,
            NombreTipo = x.Tipo.Nombre,
            x.Epp.Estado,
            Condicion = x.Entrega.CondicionEntrega,
            Descripcion = "Estado entrega: " + x.Entrega.Estado
        });
        var devoluciones = consulta.Where(x => x.Entrega.DevueltoEn != null).Select(x => new
        {
            Fecha = x.Entrega.DevueltoEn!.Value,
            TipoEvento = "DEVOLUCION",
            Id = x.Entrega.IdEntregaEpp,
            x.Obra.IdObra,
            CodigoObra = x.Obra.Codigo,
            NombreObra = x.Obra.Nombre,
            x.Epp.IdEpp,
            x.Epp.CodigoInterno,
            NombreTipo = x.Tipo.Nombre,
            x.Epp.Estado,
            Condicion = x.Entrega.CondicionDevolucion ?? x.Epp.Condicion,
            Descripcion = "Devolución registrada"
        });
        var reposiciones =
            from x in consulta
            join r in contexto.ReposicionesEpp on x.Entrega.IdEntregaEpp equals r.IdEntregaOrigen
            select new
            {
                Fecha = r.SolicitadaEn,
                TipoEvento = "REPOSICION_" + r.Motivo,
                Id = r.IdReposicionEpp,
                x.Obra.IdObra,
                CodigoObra = x.Obra.Codigo,
                NombreObra = x.Obra.Nombre,
                x.Epp.IdEpp,
                x.Epp.CodigoInterno,
                NombreTipo = x.Tipo.Nombre,
                x.Epp.Estado,
                Condicion = x.Epp.Condicion,
                Descripcion = r.Descripcion
            };
        return await entregas.Concat(devoluciones).Concat(reposiciones)
            .OrderByDescending(e => e.Fecha).ThenBy(e => e.TipoEvento).ThenByDescending(e => e.Id)
            .Paginar(paginacion)
            .Select(e => new EventoHistorialEppTrabajador(e.Fecha, e.TipoEvento, e.Id, e.IdObra,
                e.CodigoObra, e.NombreObra, e.IdEpp, e.CodigoInterno, e.NombreTipo, e.Estado, e.Condicion, e.Descripcion))
            .ToListAsync(cancelacion);
    }

    public async Task<IReadOnlyList<ItemReporteEpp>> GenerarReporteEppAsync(
        FiltroReporteEpp filtro,
        IReadOnlySet<int>? idsObrasPermitidas,
        CancellationToken cancelacion = default)
    {
        var consulta =
            from ent in contexto.EntregasEpp.AsNoTracking()
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on ent.IdAsignacion equals asig.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
            join o in contexto.Obras.AsNoTracking() on asig.IdObra equals o.IdObra
            join epp in contexto.EquiposProteccionPersonal.AsNoTracking() on ent.IdEpp equals epp.IdEpp
            join tipo in contexto.TiposEpp.AsNoTracking() on epp.IdTipoEpp equals tipo.IdTipoEpp
            select new
            {
                Entrega = ent,
                Obra = o,
                Trabajador = t,
                Tipo = tipo
            };

        if (filtro.IdObra is not null)
        {
            consulta = consulta.Where(x => x.Obra.IdObra == filtro.IdObra.Value);
        }

        if (idsObrasPermitidas is not null)
        {
            var ids = idsObrasPermitidas.ToArray();
            consulta = consulta.Where(x => ids.Contains(x.Obra.IdObra));
        }

        if (filtro.IdTrabajador is not null)
        {
            consulta = consulta.Where(x => x.Trabajador.IdTrabajador == filtro.IdTrabajador.Value);
        }

        if (filtro.IdTipoEpp is not null)
        {
            consulta = consulta.Where(x => x.Tipo.IdTipoEpp == filtro.IdTipoEpp.Value);
        }

        if (filtro.FechaDesde is not null)
        {
            consulta = consulta.Where(x => DateOnly.FromDateTime(x.Entrega.EntregadoEn) >= filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta is not null)
        {
            consulta = consulta.Where(x => DateOnly.FromDateTime(x.Entrega.EntregadoEn) <= filtro.FechaHasta.Value);
        }

        return await consulta
            .Where(x => x.Entrega.Estado != EstadosEntregaEpp.Anulada)
            .GroupBy(x => new
            {
                x.Obra.IdObra,
                x.Obra.Codigo,
                NombreObra = x.Obra.Nombre,
                x.Trabajador.IdTrabajador,
                x.Trabajador.Cedula,
                NombreTrabajador = x.Trabajador.PrimerNombre + " " + x.Trabajador.PrimerApellido,
                x.Tipo.IdTipoEpp,
                NombreTipo = x.Tipo.Nombre
            })
            .OrderBy(g => g.Key.IdObra)
            .ThenBy(g => g.Key.IdTrabajador)
            .ThenBy(g => g.Key.IdTipoEpp)
            .Select(g => new ItemReporteEpp(
                g.Key.IdObra,
                g.Key.Codigo,
                g.Key.NombreObra,
                g.Key.IdTrabajador,
                g.Key.Cedula,
                g.Key.NombreTrabajador,
                g.Key.IdTipoEpp,
                g.Key.NombreTipo,
                g.Count(),
                g.Count(x => x.Entrega.Estado == EstadosEntregaEpp.Activa),
                g.Count(x => x.Entrega.Estado == EstadosEntregaEpp.Reemplazada),
                g.Count(x => x.Entrega.Estado == EstadosEntregaEpp.Perdida)))
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    private IQueryable<RespuestaEntregaEpp> ConsultarEntregasBase(IQueryable<EntregaEpp> origen) =>
        from ent in origen
        join epp in contexto.EquiposProteccionPersonal.AsNoTracking() on ent.IdEpp equals epp.IdEpp
        join tipo in contexto.TiposEpp.AsNoTracking() on epp.IdTipoEpp equals tipo.IdTipoEpp
        join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on ent.IdAsignacion equals asig.IdAsignacion
        join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
        join o in contexto.Obras.AsNoTracking() on asig.IdObra equals o.IdObra
        select new RespuestaEntregaEpp(
            ent.IdEntregaEpp,
            ent.IdEpp,
            epp.CodigoInterno,
            epp.IdTipoEpp,
            tipo.Nombre,
            ent.IdAsignacion,
            t.IdTrabajador,
            t.PrimerNombre + " " + t.PrimerApellido,
            o.IdObra,
            o.Codigo,
            ent.IdUsuarioEntrega,
            ent.EntregadoEn,
            ent.CondicionEntrega,
            ent.DevueltoEn,
            ent.CondicionDevolucion,
            ent.Estado,
            ent.RutaConstancia,
            ent.UuidRegistro,
            ent.CreadoEn,
            ent.SincronizadoEn,
            false);

    private IQueryable<RespuestaReposicionEpp> ConsultarReposicionesBase(IQueryable<ReposicionEpp> origen) =>
        from r in origen
        join ent in contexto.EntregasEpp.AsNoTracking() on r.IdEntregaOrigen equals ent.IdEntregaEpp
        join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on ent.IdAsignacion equals asig.IdAsignacion
        select new RespuestaReposicionEpp(
            r.IdReposicionEpp,
            r.IdEntregaOrigen,
            r.IdEntregaNueva,
            asig.IdTrabajador,
            asig.IdObra,
            r.Motivo,
            r.Descripcion,
            r.EquipoDevuelto,
            r.IdUsuarioSolicita,
            r.SolicitadaEn,
            r.IdUsuarioAprueba,
            r.ResueltaEn,
            r.Estado,
            r.UuidRegistro,
            false);
}
