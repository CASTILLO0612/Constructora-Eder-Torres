using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Herramientas;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed partial class AdaptadorHerramientasPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj,
    PaginacionConsulta paginacion) : IPuertoHerramientas
{
    public async Task<IReadOnlyList<RespuestaIncidenciaHerramienta>> ListarIncidenciasAsync(int? idObra,
        long? idAsignacionHerramienta, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default,
        int? idHerramienta = null)
    {
        var obras = idsObrasPermitidas?.ToArray();
        var origen = contexto.IncidenciasHerramienta.AsNoTracking()
            .Where(i => (idAsignacionHerramienta == null || i.IdAsignacionHerramienta == idAsignacionHerramienta)
                && (from ah in contexto.AsignacionesHerramienta
                    join a in contexto.AsignacionesTrabajadorObra on ah.IdAsignacionTrabajadorObra equals a.IdAsignacion
                    where ah.IdAsignacionHerramienta == i.IdAsignacionHerramienta && (idObra == null || a.IdObra == idObra)
                        && (idHerramienta == null || ah.IdHerramienta == idHerramienta)
                        && (obras == null || obras.Contains(a.IdObra)) select ah.IdAsignacionHerramienta).Any())
            .OrderByDescending(i => i.FechaHora).ThenByDescending(i => i.IdIncidenciaHerramienta).Paginar(paginacion);
        return await ConsultarIncidenciasBase(origen).ToListAsync(cancelacion);
    }

    public Task<bool> ExisteCategoriaPorNombreAsync(string nombre, CancellationToken cancelacion = default) =>
        contexto.CategoriasHerramienta.AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower(), cancelacion);

    public async Task<RespuestaCategoriaHerramienta?> ObtenerCategoriaPorIdAsync(
        int idCategoria,
        CancellationToken cancelacion = default) =>
        await contexto.CategoriasHerramienta.AsNoTracking()
            .Where(c => c.IdCategoria == idCategoria)
            .Select(c => new RespuestaCategoriaHerramienta(c.IdCategoria, c.Nombre, c.Descripcion, c.Activo))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaCategoriaHerramienta> CrearCategoriaAsync(
        SolicitudCrearCategoriaHerramienta solicitud,
        CancellationToken cancelacion = default)
    {
        var categoria = new CategoriaHerramienta
        {
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            Activo = true
        };

        contexto.CategoriasHerramienta.Add(categoria);
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaCategoriaHerramienta(categoria.IdCategoria, categoria.Nombre, categoria.Descripcion, categoria.Activo);
    }

    public async Task<IReadOnlyList<RespuestaCategoriaHerramienta>> ListarCategoriasAsync(
        CancellationToken cancelacion = default) =>
        await contexto.CategoriasHerramienta.AsNoTracking()
            .OrderBy(c => c.IdCategoria)
            .Select(c => new RespuestaCategoriaHerramienta(c.IdCategoria, c.Nombre, c.Descripcion, c.Activo))
            .Paginar(paginacion).ToListAsync(cancelacion);

    public Task<bool> ExisteTipoPorNombreEnCategoriaAsync(
        int idCategoria,
        string nombre,
        CancellationToken cancelacion = default) =>
        contexto.TiposHerramienta.AnyAsync(
            t => t.IdCategoria == idCategoria && t.Nombre.ToLower() == nombre.ToLower(),
            cancelacion);

    public async Task<RespuestaTipoHerramienta?> ObtenerTipoPorIdAsync(
        int idTipoHerramienta,
        CancellationToken cancelacion = default) =>
        await (
            from t in contexto.TiposHerramienta.AsNoTracking()
            join c in contexto.CategoriasHerramienta.AsNoTracking() on t.IdCategoria equals c.IdCategoria
            where t.IdTipoHerramienta == idTipoHerramienta
            select new RespuestaTipoHerramienta(
                t.IdTipoHerramienta,
                t.IdCategoria,
                c.Nombre,
                t.Nombre,
                t.Descripcion,
                t.Activo))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaTipoHerramienta> CrearTipoAsync(
        SolicitudCrearTipoHerramienta solicitud,
        CancellationToken cancelacion = default)
    {
        var tipo = new TipoHerramienta
        {
            IdCategoria = solicitud.IdCategoria,
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            Activo = true
        };

        contexto.TiposHerramienta.Add(tipo);
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerTipoPorIdAsync(tipo.IdTipoHerramienta, cancelacion))!;
    }

    public async Task<IReadOnlyList<RespuestaTipoHerramienta>> ListarTiposAsync(
        int? idCategoria = null,
        CancellationToken cancelacion = default)
    {
        return await (
            from t in contexto.TiposHerramienta.AsNoTracking()
            join c in contexto.CategoriasHerramienta.AsNoTracking() on t.IdCategoria equals c.IdCategoria
            where idCategoria == null || t.IdCategoria == idCategoria.Value
            orderby t.IdTipoHerramienta
            select new RespuestaTipoHerramienta(
                t.IdTipoHerramienta,
                t.IdCategoria,
                c.Nombre,
                t.Nombre,
                t.Descripcion,
                t.Activo))
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public Task<bool> ExisteHerramientaPorCodigoAsync(string codigoInterno, CancellationToken cancelacion = default) =>
        contexto.Herramientas.AnyAsync(h => h.CodigoInterno.ToLower() == codigoInterno.ToLower(), cancelacion);

    public Task<bool> ExisteHerramientaPorSerieAsync(string numeroSerie, CancellationToken cancelacion = default) =>
        contexto.Herramientas.AnyAsync(
            h => h.NumeroSerie != null && h.NumeroSerie.ToLower() == numeroSerie.ToLower(),
            cancelacion);

    public async Task<RespuestaHerramienta?> ObtenerHerramientaPorIdAsync(
        int idHerramienta,
        CancellationToken cancelacion = default) =>
        await ConsultarHerramientasBase(contexto.Herramientas.AsNoTracking().Where(h => h.IdHerramienta == idHerramienta))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaHerramienta> RegistrarHerramientaAsync(
        SolicitudRegistrarHerramienta solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var herramienta = new Herramienta
        {
            IdTipoHerramienta = solicitud.IdTipoHerramienta,
            CodigoInterno = solicitud.CodigoInterno,
            NumeroSerie = solicitud.NumeroSerie,
            Marca = solicitud.Marca,
            Modelo = solicitud.Modelo,
            FechaAdquisicion = solicitud.FechaAdquisicion,
            Estado = EstadosHerramienta.Disponible,
            Condicion = solicitud.Condicion
        };

        contexto.Herramientas.Add(herramienta);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = herramienta.IdHerramienta.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerHerramientaPorIdAsync(herramienta.IdHerramienta, cancelacion))!;
    }

    public async Task<IReadOnlyList<RespuestaHerramienta>> ListarHerramientasAsync(
        int? idTipoHerramienta = null,
        string? estado = null,
        string? condicion = null,
        CancellationToken cancelacion = default)
    {
        var origen = contexto.Herramientas.AsNoTracking();
        if (idTipoHerramienta is not null)
        {
            origen = origen.Where(h => h.IdTipoHerramienta == idTipoHerramienta.Value);
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            origen = origen.Where(h => h.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(condicion))
        {
            origen = origen.Where(h => h.Condicion == condicion);
        }

        return await ConsultarHerramientasBase(origen.OrderBy(h => h.IdHerramienta))
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public async Task<ContextoAsignacionObra?> ObtenerContextoAsignacionTrabajadorAsync(
        int idAsignacionTrabajadorObra, CancellationToken cancelacion = default)
    {
        var fila = await (
            from a in contexto.AsignacionesTrabajadorObra.AsNoTracking()
            join t in contexto.Trabajadores.AsNoTracking() on a.IdTrabajador equals t.IdTrabajador
            where a.IdAsignacion == idAsignacionTrabajadorObra
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

    public Task<bool> TieneAsignacionHerramientaActivaAsync(int idHerramienta, CancellationToken cancelacion = default) =>
        contexto.AsignacionesHerramienta.AnyAsync(
            a => a.IdHerramienta == idHerramienta
                 && a.Estado == EstadosAsignacionHerramienta.Activa
                 && a.DevueltaEn == null,
            cancelacion);

    public async Task<RespuestaAsignacionHerramienta?> ObtenerAsignacionPorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default) =>
        await ConsultarAsignacionesBase(contexto.AsignacionesHerramienta.AsNoTracking().Where(a => a.UuidRegistro == uuidRegistro))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaAsignacionHerramienta?> ObtenerAsignacionPorIdAsync(
        long idAsignacionHerramienta,
        CancellationToken cancelacion = default) =>
        await ConsultarAsignacionesBase(contexto.AsignacionesHerramienta.AsNoTracking().Where(a => a.IdAsignacionHerramienta == idAsignacionHerramienta))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaAsignacionHerramienta> AsignarHerramientaAsync(
        SolicitudAsignarHerramienta solicitud,
        string condicionEntrega,
        int idUsuarioEntrega,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var herramienta = await contexto.Herramientas
            .FirstAsync(h => h.IdHerramienta == solicitud.IdHerramienta, cancelacion);

        herramienta.Estado = EstadosHerramienta.Asignada;

        var asignacion = new AsignacionHerramienta
        {
            IdHerramienta = solicitud.IdHerramienta,
            IdAsignacionTrabajadorObra = solicitud.IdAsignacionTrabajadorObra,
            IdUsuarioEntrega = idUsuarioEntrega,
            EntregadaEn = solicitud.EntregadaEn,
            CondicionEntrega = condicionEntrega,
            DevueltaEn = null,
            CondicionDevolucion = null,
            Estado = EstadosAsignacionHerramienta.Activa,
            UuidRegistro = uuidRegistro,
            CreadoEn = creadoEnUtc,
            SincronizadoEn = sincronizadoEnUtc
        };

        contexto.AsignacionesHerramienta.Add(asignacion);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = asignacion.IdAsignacionHerramienta.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerAsignacionPorIdAsync(asignacion.IdAsignacionHerramienta, cancelacion))!;
    }

    public async Task<IReadOnlyList<RespuestaAsignacionHerramienta>> ListarAsignacionesHerramientaAsync(
        int? idObra = null,
        bool soloActivas = false,
        IReadOnlySet<int>? idsObrasPermitidas = null,
        CancellationToken cancelacion = default,
        string? estado = null)
    {
        var idsPermitidos = idsObrasPermitidas?.ToArray();
        return await (
            from a in contexto.AsignacionesHerramienta.AsNoTracking()
            join h in contexto.Herramientas.AsNoTracking() on a.IdHerramienta equals h.IdHerramienta
            join th in contexto.TiposHerramienta.AsNoTracking() on h.IdTipoHerramienta equals th.IdTipoHerramienta
            join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on a.IdAsignacionTrabajadorObra equals asig.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
            join o in contexto.Obras.AsNoTracking() on asig.IdObra equals o.IdObra
            where (idObra == null || o.IdObra == idObra.Value)
                  && (idsPermitidos == null || idsPermitidos.Contains(o.IdObra))
                  && (!soloActivas || (a.Estado == EstadosAsignacionHerramienta.Activa && a.DevueltaEn == null))
                  && (estado == null || a.Estado == estado)
            orderby a.EntregadaEn descending, a.IdAsignacionHerramienta descending
            select new RespuestaAsignacionHerramienta(
                a.IdAsignacionHerramienta,
                a.IdHerramienta,
                h.CodigoInterno,
                th.Nombre,
                a.IdAsignacionTrabajadorObra,
                t.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                o.IdObra,
                o.Codigo,
                a.IdUsuarioEntrega,
                a.EntregadaEn,
                a.CondicionEntrega,
                a.DevueltaEn,
                a.CondicionDevolucion,
                a.Estado,
                a.UuidRegistro,
                a.CreadoEn,
                a.SincronizadoEn,
                false))
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public async Task<RespuestaIncidenciaHerramienta?> ObtenerIncidenciaPorIdAsync(
        long idIncidencia,
        CancellationToken cancelacion = default) =>
        await ConsultarIncidenciasBase(contexto.IncidenciasHerramienta.AsNoTracking().Where(i => i.IdIncidenciaHerramienta == idIncidencia))
            .FirstOrDefaultAsync(cancelacion);
    public async Task<RespuestaIncidenciaHerramienta?> ObtenerIncidenciaPorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default) =>
        await ConsultarIncidenciasBase(contexto.IncidenciasHerramienta.AsNoTracking().Where(i => i.UuidRegistro == uuidRegistro))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<RespuestaIncidenciaHerramienta> RegistrarEventoHerramientaAsync(
        SolicitudRegistrarEventoHerramienta solicitud,
        string nuevoEstadoAsignacion,
        string? condicionDevolucion,
        string nuevoEstadoHerramienta,
        string nuevaCondicionHerramienta,
        string estadoIncidencia,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var asignacion = await contexto.AsignacionesHerramienta
            .FirstAsync(a => a.IdAsignacionHerramienta == solicitud.IdAsignacionHerramienta, cancelacion);

        var herramienta = await contexto.Herramientas
            .FirstAsync(h => h.IdHerramienta == asignacion.IdHerramienta, cancelacion);

        asignacion.Estado = nuevoEstadoAsignacion;
        if (nuevoEstadoAsignacion == EstadosAsignacionHerramienta.Devuelta)
        {
            asignacion.DevueltaEn = solicitud.FechaHora;
            asignacion.CondicionDevolucion = condicionDevolucion;
        }
        else if (nuevoEstadoAsignacion == EstadosAsignacionHerramienta.Perdida)
        {
            asignacion.DevueltaEn = null;
            asignacion.CondicionDevolucion = null;
        }

        herramienta.Estado = nuevoEstadoHerramienta;
        herramienta.Condicion = nuevaCondicionHerramienta;

        var incidencia = new IncidenciaHerramienta
        {
            IdAsignacionHerramienta = asignacion.IdAsignacionHerramienta,
            TipoIncidencia = solicitud.TipoIncidencia.Trim().ToUpperInvariant(),
            FechaHora = solicitud.FechaHora,
            Descripcion = solicitud.Descripcion.Trim(),
            Resolucion = string.IsNullOrWhiteSpace(solicitud.Resolucion) ? null : solicitud.Resolucion.Trim(),
            CostoEstimado = solicitud.CostoEstimado,
            IdUsuarioRegistro = idUsuarioRegistro,
            UuidRegistro = uuidRegistro,
            Estado = estadoIncidencia
        };

        contexto.IncidenciasHerramienta.Add(incidencia);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = incidencia.IdIncidenciaHerramienta.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ConsultarIncidenciasBase(contexto.IncidenciasHerramienta.AsNoTracking().Where(i => i.IdIncidenciaHerramienta == incidencia.IdIncidenciaHerramienta))
            .FirstAsync(cancelacion);
    }

    private IQueryable<RespuestaHerramienta> ConsultarHerramientasBase(IQueryable<Herramienta> origen) =>
        from h in origen
        join t in contexto.TiposHerramienta.AsNoTracking() on h.IdTipoHerramienta equals t.IdTipoHerramienta
        join c in contexto.CategoriasHerramienta.AsNoTracking() on t.IdCategoria equals c.IdCategoria
        select new RespuestaHerramienta(
            h.IdHerramienta,
            h.IdTipoHerramienta,
            t.Nombre,
            c.IdCategoria,
            c.Nombre,
            h.CodigoInterno,
            h.NumeroSerie,
            h.Marca,
            h.Modelo,
            h.FechaAdquisicion,
            h.Estado,
            h.Condicion);

    private IQueryable<RespuestaAsignacionHerramienta> ConsultarAsignacionesBase(IQueryable<AsignacionHerramienta> origen) =>
        from a in origen
        join h in contexto.Herramientas.AsNoTracking() on a.IdHerramienta equals h.IdHerramienta
        join th in contexto.TiposHerramienta.AsNoTracking() on h.IdTipoHerramienta equals th.IdTipoHerramienta
        join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on a.IdAsignacionTrabajadorObra equals asig.IdAsignacion
        join t in contexto.Trabajadores.AsNoTracking() on asig.IdTrabajador equals t.IdTrabajador
        join o in contexto.Obras.AsNoTracking() on asig.IdObra equals o.IdObra
        select new RespuestaAsignacionHerramienta(
            a.IdAsignacionHerramienta,
            a.IdHerramienta,
            h.CodigoInterno,
            th.Nombre,
            a.IdAsignacionTrabajadorObra,
            t.IdTrabajador,
            t.PrimerNombre + " " + t.PrimerApellido,
            o.IdObra,
            o.Codigo,
            a.IdUsuarioEntrega,
            a.EntregadaEn,
            a.CondicionEntrega,
            a.DevueltaEn,
            a.CondicionDevolucion,
            a.Estado,
            a.UuidRegistro,
            a.CreadoEn,
            a.SincronizadoEn,
            false);

    private IQueryable<RespuestaIncidenciaHerramienta> ConsultarIncidenciasBase(IQueryable<IncidenciaHerramienta> origen) =>
        from i in origen
        join a in contexto.AsignacionesHerramienta.AsNoTracking() on i.IdAsignacionHerramienta equals a.IdAsignacionHerramienta
        join h in contexto.Herramientas.AsNoTracking() on a.IdHerramienta equals h.IdHerramienta
        join asig in contexto.AsignacionesTrabajadorObra.AsNoTracking() on a.IdAsignacionTrabajadorObra equals asig.IdAsignacion
        select new RespuestaIncidenciaHerramienta(
            i.IdIncidenciaHerramienta,
            i.IdAsignacionHerramienta,
            h.IdHerramienta,
            h.CodigoInterno,
            asig.IdTrabajador,
            asig.IdObra,
            i.TipoIncidencia,
            i.FechaHora,
            i.Descripcion,
            i.Resolucion,
            i.CostoEstimado,
            i.IdUsuarioRegistro,
            i.UuidRegistro,
            i.Estado,
            a.Estado,
            h.Estado,
            false);
}
