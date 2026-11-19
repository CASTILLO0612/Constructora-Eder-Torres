using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed class AdaptadorPersonalPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj,
    ICalendarioLaboral calendario,
    PaginacionConsulta paginacion) : IPuertoPersonal
{
    public async Task<RespuestaTrabajadorCuadrilla?> ObtenerMiembroCuadrillaPorIdAsync(
        int idTrabajadorCuadrilla, CancellationToken cancelacion = default) =>
        await (from tc in contexto.TrabajadoresCuadrilla.AsNoTracking()
            join a in contexto.AsignacionesTrabajadorObra on tc.IdAsignacion equals a.IdAsignacion
            join t in contexto.Trabajadores on a.IdTrabajador equals t.IdTrabajador
            join co in contexto.CuadrillasObra on tc.IdCuadrillaObra equals co.IdCuadrillaObra
            join c in contexto.Cuadrillas on co.IdCuadrilla equals c.IdCuadrilla
            where tc.IdTrabajadorCuadrilla == idTrabajadorCuadrilla
            select new RespuestaTrabajadorCuadrilla(tc.IdTrabajadorCuadrilla, tc.IdAsignacion,
                a.IdTrabajador, t.PrimerNombre + " " + t.PrimerApellido, tc.IdCuadrillaObra,
                co.IdCuadrilla, c.Nombre, co.IdObra, tc.FechaInicio, tc.FechaFin))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<IReadOnlyList<RespuestaCargo>> ListarCargosAsync(CancellationToken cancelacion = default) =>
        await contexto.Cargos.AsNoTracking()
            .OrderBy(c => c.IdCargo)
            .Select(c => new RespuestaCargo(c.IdCargo, c.Nombre, c.Descripcion, c.Activo))
            .Buscar(paginacion, c => c.Nombre, c => c.Descripcion)
            .Paginar(paginacion).ToListAsync(cancelacion);

    public async Task<RespuestaCargo?> ObtenerCargoPorIdAsync(int idCargo, CancellationToken cancelacion = default) =>
        await contexto.Cargos.AsNoTracking()
            .Where(c => c.IdCargo == idCargo)
            .Select(c => new RespuestaCargo(c.IdCargo, c.Nombre, c.Descripcion, c.Activo))
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> ExisteCargoPorNombreAsync(string nombre, CancellationToken cancelacion = default) =>
        contexto.Cargos.AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower(), cancelacion);

    public async Task<RespuestaCargo> CrearCargoAsync(SolicitudCargo solicitud, CancellationToken cancelacion = default)
    {
        var cargo = new Cargo
        {
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            Activo = true
        };
        contexto.Cargos.Add(cargo);
        await contexto.SaveChangesAsync(cancelacion);
        return new RespuestaCargo(cargo.IdCargo, cargo.Nombre, cargo.Descripcion, cargo.Activo);
    }

    public async Task<IReadOnlyList<RespuestaEspecialidad>> ListarEspecialidadesAsync(CancellationToken cancelacion = default) =>
        await contexto.Especialidades.AsNoTracking()
            .OrderBy(e => e.IdEspecialidad)
            .Select(e => new RespuestaEspecialidad(e.IdEspecialidad, e.Nombre, e.Descripcion, e.Activo))
            .Buscar(paginacion, e => e.Nombre, e => e.Descripcion)
            .Paginar(paginacion).ToListAsync(cancelacion);

    public async Task<RespuestaEspecialidad?> ObtenerEspecialidadPorIdAsync(int idEspecialidad, CancellationToken cancelacion = default) =>
        await contexto.Especialidades.AsNoTracking()
            .Where(e => e.IdEspecialidad == idEspecialidad)
            .Select(e => new RespuestaEspecialidad(e.IdEspecialidad, e.Nombre, e.Descripcion, e.Activo))
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> ExisteEspecialidadPorNombreAsync(string nombre, CancellationToken cancelacion = default) =>
        contexto.Especialidades.AnyAsync(e => e.Nombre.ToLower() == nombre.ToLower(), cancelacion);

    public async Task<RespuestaEspecialidad> CrearEspecialidadAsync(SolicitudEspecialidad solicitud, CancellationToken cancelacion = default)
    {
        var especialidad = new Especialidad
        {
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            Activo = true
        };
        contexto.Especialidades.Add(especialidad);
        await contexto.SaveChangesAsync(cancelacion);
        return new RespuestaEspecialidad(especialidad.IdEspecialidad, especialidad.Nombre, especialidad.Descripcion, especialidad.Activo);
    }

    public async Task<IReadOnlyList<RespuestaCuadrilla>> ListarCuadrillasAsync(CancellationToken cancelacion = default) =>
        await contexto.Cuadrillas.AsNoTracking()
            .OrderBy(c => c.IdCuadrilla)
            .Select(c => new RespuestaCuadrilla(c.IdCuadrilla, c.Codigo, c.Nombre, c.Descripcion, c.Activo))
            .Buscar(paginacion, c => c.Codigo, c => c.Nombre, c => c.Descripcion)
            .Paginar(paginacion).ToListAsync(cancelacion);

    public async Task<RespuestaCuadrilla?> ObtenerCuadrillaPorIdAsync(int idCuadrilla, CancellationToken cancelacion = default) =>
        await contexto.Cuadrillas.AsNoTracking()
            .Where(c => c.IdCuadrilla == idCuadrilla)
            .Select(c => new RespuestaCuadrilla(c.IdCuadrilla, c.Codigo, c.Nombre, c.Descripcion, c.Activo))
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> ExisteCuadrillaPorCodigoAsync(string codigo, CancellationToken cancelacion = default) =>
        contexto.Cuadrillas.AnyAsync(c => c.Codigo.ToLower() == codigo.ToLower(), cancelacion);

    public async Task<RespuestaCuadrilla> CrearCuadrillaAsync(SolicitudCuadrilla solicitud, CancellationToken cancelacion = default)
    {
        var cuadrilla = new Cuadrilla
        {
            Codigo = solicitud.Codigo,
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            Activo = true
        };
        contexto.Cuadrillas.Add(cuadrilla);
        await contexto.SaveChangesAsync(cancelacion);
        return new RespuestaCuadrilla(cuadrilla.IdCuadrilla, cuadrilla.Codigo, cuadrilla.Nombre, cuadrilla.Descripcion, cuadrilla.Activo);
    }

    public Task<bool> ExisteTrabajadorPorCedulaAsync(string cedula, int? excluirIdTrabajador = null, CancellationToken cancelacion = default) =>
        contexto.Trabajadores.AnyAsync(
            t => t.Cedula.ToLower() == cedula.ToLower()
                 && (excluirIdTrabajador == null || t.IdTrabajador != excluirIdTrabajador.Value),
            cancelacion);

    public Task<bool> ExisteTrabajadorPorCorreoAsync(string correo, int? excluirIdTrabajador = null, CancellationToken cancelacion = default) =>
        contexto.Trabajadores.AnyAsync(
            t => t.Correo != null && t.Correo.ToLower() == correo.ToLower()
                 && (excluirIdTrabajador == null || t.IdTrabajador != excluirIdTrabajador.Value),
            cancelacion);

    public async Task<RespuestaTrabajador> CrearTrabajadorAsync(
        SolicitudCrearTrabajador solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var trabajador = new Trabajador
        {
            IdCargo = solicitud.IdCargo,
            Cedula = solicitud.Cedula,
            PrimerNombre = solicitud.PrimerNombre,
            SegundoNombre = solicitud.SegundoNombre,
            PrimerApellido = solicitud.PrimerApellido,
            SegundoApellido = solicitud.SegundoApellido,
            Telefono = solicitud.Telefono,
            Correo = solicitud.Correo,
            Direccion = solicitud.Direccion,
            FechaIngreso = solicitud.FechaIngreso,
            Estado = EstadosTrabajador.Activo
        };

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(cancelacion);

        if (solicitud.Especialidades is { Count: > 0 })
        {
            foreach (var item in solicitud.Especialidades.DistinctBy(e => e.IdEspecialidad))
            {
                contexto.TrabajadoresEspecialidad.Add(new TrabajadorEspecialidad
                {
                    IdTrabajador = trabajador.IdTrabajador,
                    IdEspecialidad = item.IdEspecialidad,
                    Nivel = string.IsNullOrWhiteSpace(item.Nivel) ? null : item.Nivel.Trim(),
                    FechaCertificacion = item.FechaCertificacion,
                    FechaVencimiento = item.FechaVencimiento
                });
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = trabajador.IdTrabajador.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerTrabajadorPorIdAsync(trabajador.IdTrabajador, cancelacion))!;
    }

    public async Task<RespuestaTrabajador?> ObtenerTrabajadorPorIdAsync(
        int idTrabajador,
        CancellationToken cancelacion = default)
    {
        var fila = await (
            from t in contexto.Trabajadores.AsNoTracking()
            join c in contexto.Cargos.AsNoTracking() on t.IdCargo equals c.IdCargo
            where t.IdTrabajador == idTrabajador
            select new { Trabajador = t, NombreCargo = c.Nombre })
            .FirstOrDefaultAsync(cancelacion);

        if (fila is null)
        {
            return null;
        }

        var especialidades = await (
            from te in contexto.TrabajadoresEspecialidad.AsNoTracking()
            join e in contexto.Especialidades.AsNoTracking() on te.IdEspecialidad equals e.IdEspecialidad
            where te.IdTrabajador == idTrabajador
            orderby e.Nombre
            select new RespuestaEspecialidadTrabajador(
                te.IdEspecialidad,
                e.Nombre,
                te.Nivel,
                te.FechaCertificacion,
                te.FechaVencimiento))
            .ToListAsync(cancelacion);

        return MapearTrabajador(fila.Trabajador, fila.NombreCargo, especialidades);
    }

    public async Task<IReadOnlyList<RespuestaTrabajador>> ListarTrabajadoresAsync(
        string? estado,
        bool soloAsignables,
        IReadOnlySet<int>? idsObrasAlcance,
        CancellationToken cancelacion = default)
    {
        var consulta =
            from t in contexto.Trabajadores.AsNoTracking()
            join c in contexto.Cargos.AsNoTracking() on t.IdCargo equals c.IdCargo
            select new { Trabajador = t, NombreCargo = c.Nombre };

        if (!string.IsNullOrWhiteSpace(estado))
        {
            consulta = consulta.Where(x => x.Trabajador.Estado == estado);
        }

        if (soloAsignables)
        {
            consulta = consulta.Where(x => x.Trabajador.Estado == EstadosTrabajador.Activo);
        }

        if (idsObrasAlcance is not null)
        {
            var obrasIds = idsObrasAlcance.ToArray();
            consulta = consulta.Where(x =>
                contexto.AsignacionesTrabajadorObra.Any(a =>
                    a.IdTrabajador == x.Trabajador.IdTrabajador && obrasIds.Contains(a.IdObra)));
        }

        var filas = await consulta
            .OrderBy(x => x.Trabajador.IdTrabajador)
            .Buscar(paginacion, x => x.Trabajador.PrimerNombre, x => x.Trabajador.SegundoNombre, x => x.Trabajador.PrimerApellido, x => x.Trabajador.SegundoApellido, x => x.Trabajador.Cedula, x => x.Trabajador.Telefono, x => x.Trabajador.Correo)
            .Paginar(paginacion).ToListAsync(cancelacion);

        if (filas.Count == 0)
        {
            return [];
        }

        var idsTrabajadores = filas.Select(f => f.Trabajador.IdTrabajador).ToArray();
        var especialidades = await (
            from te in contexto.TrabajadoresEspecialidad.AsNoTracking()
            join e in contexto.Especialidades.AsNoTracking() on te.IdEspecialidad equals e.IdEspecialidad
            where idsTrabajadores.Contains(te.IdTrabajador)
            orderby e.Nombre
            select new
            {
                te.IdTrabajador,
                Item = new RespuestaEspecialidadTrabajador(
                    te.IdEspecialidad,
                    e.Nombre,
                    te.Nivel,
                    te.FechaCertificacion,
                    te.FechaVencimiento)
            })
            .ToListAsync(cancelacion);

        var porTrabajador = especialidades
            .GroupBy(e => e.IdTrabajador)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RespuestaEspecialidadTrabajador>)g.Select(x => x.Item).ToList());

        return filas.Select(f => MapearTrabajador(
            f.Trabajador,
            f.NombreCargo,
            porTrabajador.GetValueOrDefault(f.Trabajador.IdTrabajador, []))).ToList();
    }

    public async Task<RespuestaTrabajador?> ActualizarTrabajadorAsync(
        int idTrabajador,
        SolicitudActualizarTrabajador solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var trabajador = await contexto.Trabajadores.FirstOrDefaultAsync(t => t.IdTrabajador == idTrabajador, cancelacion);
        if (trabajador is null)
        {
            return null;
        }

        trabajador.IdCargo = solicitud.IdCargo;
        trabajador.PrimerNombre = solicitud.PrimerNombre;
        trabajador.SegundoNombre = solicitud.SegundoNombre;
        trabajador.PrimerApellido = solicitud.PrimerApellido;
        trabajador.SegundoApellido = solicitud.SegundoApellido;
        trabajador.Telefono = solicitud.Telefono;
        trabajador.Correo = solicitud.Correo;
        trabajador.Direccion = solicitud.Direccion;

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerTrabajadorPorIdAsync(idTrabajador, cancelacion);
    }

    public async Task<RespuestaTrabajador?> CambiarEstadoTrabajadorAsync(
        int idTrabajador,
        string nuevoEstado,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var trabajador = await contexto.Trabajadores.FirstOrDefaultAsync(t => t.IdTrabajador == idTrabajador, cancelacion);
        if (trabajador is null)
        {
            return null;
        }

        trabajador.Estado = nuevoEstado;
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerTrabajadorPorIdAsync(idTrabajador, cancelacion);
    }

    public async Task<RespuestaEspecialidadTrabajador> RegistrarEspecialidadTrabajadorAsync(
        int idTrabajador,
        SolicitudEspecialidadTrabajador solicitud,
        CancellationToken cancelacion = default)
    {
        var existente = await contexto.TrabajadoresEspecialidad
            .FirstOrDefaultAsync(
                te => te.IdTrabajador == idTrabajador && te.IdEspecialidad == solicitud.IdEspecialidad,
                cancelacion);

        if (existente is null)
        {
            existente = new TrabajadorEspecialidad
            {
                IdTrabajador = idTrabajador,
                IdEspecialidad = solicitud.IdEspecialidad,
                Nivel = string.IsNullOrWhiteSpace(solicitud.Nivel) ? null : solicitud.Nivel.Trim(),
                FechaCertificacion = solicitud.FechaCertificacion,
                FechaVencimiento = solicitud.FechaVencimiento
            };
            contexto.TrabajadoresEspecialidad.Add(existente);
        }
        else
        {
            existente.Nivel = string.IsNullOrWhiteSpace(solicitud.Nivel) ? null : solicitud.Nivel.Trim();
            existente.FechaCertificacion = solicitud.FechaCertificacion;
            existente.FechaVencimiento = solicitud.FechaVencimiento;
        }

        await contexto.SaveChangesAsync(cancelacion);

        var especialidad = await contexto.Especialidades.AsNoTracking()
            .FirstAsync(e => e.IdEspecialidad == solicitud.IdEspecialidad, cancelacion);

        return new RespuestaEspecialidadTrabajador(
            existente.IdEspecialidad,
            especialidad.Nombre,
            existente.Nivel,
            existente.FechaCertificacion,
            existente.FechaVencimiento);
    }

    public Task<bool> TrabajadorTieneEspecialidadAsync(
        int idTrabajador,
        int idEspecialidad,
        CancellationToken cancelacion = default) =>
        contexto.TrabajadoresEspecialidad.AnyAsync(
            te => te.IdTrabajador == idTrabajador && te.IdEspecialidad == idEspecialidad,
            cancelacion);

    public Task<bool> TrabajadorPerteneceAObrasAsync(int idTrabajador, IReadOnlySet<int> obras, CancellationToken cancelacion = default)
    {
        var ids = obras.ToArray();
        var hoy = calendario.Hoy;
        return contexto.AsignacionesTrabajadorObra.AnyAsync(a => a.IdTrabajador == idTrabajador
            && ids.Contains(a.IdObra) && a.Estado == EstadosAsignacionObra.Activa
            && a.FechaInicio <= hoy && (a.FechaFin == null || a.FechaFin >= hoy), cancelacion);
    }

    public Task<bool> ExisteObraAsync(int idObra, CancellationToken cancelacion = default) =>
        contexto.Obras.AnyAsync(o => o.IdObra == idObra, cancelacion);

    public Task<bool> ExisteAsignacionActivaTrabajadorObraAsync(
        int idTrabajador,
        int idObra,
        CancellationToken cancelacion = default) =>
        contexto.AsignacionesTrabajadorObra.AnyAsync(
            a => a.IdTrabajador == idTrabajador
                 && a.IdObra == idObra
                 && a.Estado == EstadosAsignacionObra.Activa
                 && (a.FechaFin == null || a.FechaFin >= calendario.Hoy),
            cancelacion);

    public async Task<RespuestaAsignacionTrabajadorObra> AsignarTrabajadorAObraAsync(
        SolicitudAsignarTrabajadorObra solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        if (solicitud.IdEspecialidad is not null)
        {
            var tieneEspecialidad = await contexto.TrabajadoresEspecialidad.AnyAsync(
                te => te.IdTrabajador == solicitud.IdTrabajador && te.IdEspecialidad == solicitud.IdEspecialidad.Value,
                cancelacion);

            if (!tieneEspecialidad)
            {
                contexto.TrabajadoresEspecialidad.Add(new TrabajadorEspecialidad
                {
                    IdTrabajador = solicitud.IdTrabajador,
                    IdEspecialidad = solicitud.IdEspecialidad.Value
                });
                await contexto.SaveChangesAsync(cancelacion);
            }
        }

        var asignacion = new AsignacionTrabajadorObra
        {
            IdTrabajador = solicitud.IdTrabajador,
            IdObra = solicitud.IdObra,
            IdEspecialidad = solicitud.IdEspecialidad,
            TipoContratacion = solicitud.TipoContratacion,
            FechaInicio = solicitud.FechaInicio,
            FechaFin = solicitud.FechaFin,
            EsResponsable = solicitud.EsResponsable,
            Estado = solicitud.FechaFin is { } fin && fin < calendario.Hoy
                ? EstadosAsignacionObra.Finalizada
                : EstadosAsignacionObra.Activa
        };

        contexto.AsignacionesTrabajadorObra.Add(asignacion);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = asignacion.IdAsignacion.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerAsignacionPorIdAsync(asignacion.IdAsignacion, cancelacion))!;
    }

    public async Task<RespuestaAsignacionTrabajadorObra?> ObtenerAsignacionPorIdAsync(
        int idAsignacion,
        CancellationToken cancelacion = default) =>
        await (
            from a in contexto.AsignacionesTrabajadorObra.AsNoTracking()
            join t in contexto.Trabajadores.AsNoTracking() on a.IdTrabajador equals t.IdTrabajador
            join o in contexto.Obras.AsNoTracking() on a.IdObra equals o.IdObra
            join e in contexto.Especialidades.AsNoTracking() on a.IdEspecialidad equals e.IdEspecialidad into espJoin
            from esp in espJoin.DefaultIfEmpty()
            where a.IdAsignacion == idAsignacion
            select new RespuestaAsignacionTrabajadorObra(
                a.IdAsignacion,
                a.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                t.Cedula,
                a.IdObra,
                o.Codigo,
                a.IdEspecialidad,
                esp != null ? esp.Nombre : null,
                a.TipoContratacion,
                a.FechaInicio,
                a.FechaFin,
                a.EsResponsable,
                a.Estado))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<IReadOnlyList<RespuestaAsignacionTrabajadorObra>> ListarAsignacionesObraAsync(
        int idObra,
        bool soloVigentes,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.AsignacionesTrabajadorObra.AsNoTracking()
            .Where(a => a.IdObra == idObra);
        if (soloVigentes)
        {
            consulta = consulta.Where(a => a.Estado == EstadosAsignacionObra.Activa && a.FechaInicio <= calendario.Hoy && (a.FechaFin == null || a.FechaFin >= calendario.Hoy));
        }

        return await (
            from a in consulta
            join t in contexto.Trabajadores.AsNoTracking() on a.IdTrabajador equals t.IdTrabajador
            join o in contexto.Obras.AsNoTracking() on a.IdObra equals o.IdObra
            join e in contexto.Especialidades.AsNoTracking() on a.IdEspecialidad equals e.IdEspecialidad into espJoin
            from esp in espJoin.DefaultIfEmpty()
            orderby a.IdAsignacion
            select new RespuestaAsignacionTrabajadorObra(
                a.IdAsignacion,
                a.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                t.Cedula,
                a.IdObra,
                o.Codigo,
                a.IdEspecialidad,
                esp != null ? esp.Nombre : null,
                a.TipoContratacion,
                a.FechaInicio,
                a.FechaFin,
                a.EsResponsable,
                a.Estado))
            .Buscar(paginacion, a => a.NombreCompletoTrabajador, a => a.CedulaTrabajador, a => a.CodigoObra, a => a.NombreEspecialidad, a => a.TipoContratacion, a => a.Estado)
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    public async Task<RespuestaAsignacionTrabajadorObra?> FinalizarAsignacionObraAsync(
        int idAsignacion,
        DateOnly fechaFin,
        string nuevoEstado,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var asignacion = await contexto.AsignacionesTrabajadorObra
            .FirstOrDefaultAsync(a => a.IdAsignacion == idAsignacion, cancelacion);
        if (asignacion is null)
        {
            return null;
        }

        if (await contexto.TrabajadoresCuadrilla.AnyAsync(tc => tc.IdAsignacion == idAsignacion && tc.FechaInicio > fechaFin, cancelacion))
            throw new ExcepcionPersistencia(ErrorNegocio.Conflicto("cuadrilla_posterior_al_cierre",
                "Existe una participación en cuadrilla posterior a la fecha de cierre. Revísela antes de finalizar la asignación."));
        asignacion.FechaFin = fechaFin;
        asignacion.Estado = nuevoEstado;

        var cuadrillasAbiertas = await contexto.TrabajadoresCuadrilla
            .Where(tc => tc.IdAsignacion == idAsignacion && (tc.FechaFin == null || tc.FechaFin > fechaFin))
            .ToListAsync(cancelacion);

        foreach (var tc in cuadrillasAbiertas)
        {
            tc.FechaFin = fechaFin;
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerAsignacionPorIdAsync(idAsignacion, cancelacion);
    }

    public Task<bool> ExisteCuadrillaObraActivaAsync(
        int idCuadrilla,
        int idObra,
        CancellationToken cancelacion = default) =>
        contexto.CuadrillasObra.AnyAsync(
            co => co.IdCuadrilla == idCuadrilla
                  && co.IdObra == idObra
                  && co.Estado == EstadosAsignacionObra.Activa
                  && co.FechaFin == null,
            cancelacion);

    public async Task<RespuestaCuadrillaObra> AsignarCuadrillaAObraAsync(
        SolicitudAsignarCuadrillaObra solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var cuadrillaObra = new CuadrillaObra
        {
            IdCuadrilla = solicitud.IdCuadrilla,
            IdObra = solicitud.IdObra,
            FechaInicio = solicitud.FechaInicio,
            FechaFin = solicitud.FechaFin,
            Estado = solicitud.FechaFin is { } fin && fin < calendario.Hoy
                ? EstadosAsignacionObra.Finalizada
                : EstadosAsignacionObra.Activa
        };

        contexto.CuadrillasObra.Add(cuadrillaObra);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = cuadrillaObra.IdCuadrillaObra.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerCuadrillaObraPorIdAsync(cuadrillaObra.IdCuadrillaObra, cancelacion))!;
    }

    public async Task<RespuestaCuadrillaObra?> ObtenerCuadrillaObraPorIdAsync(
        int idCuadrillaObra,
        CancellationToken cancelacion = default) =>
        await (
            from co in contexto.CuadrillasObra.AsNoTracking()
            join c in contexto.Cuadrillas.AsNoTracking() on co.IdCuadrilla equals c.IdCuadrilla
            where co.IdCuadrillaObra == idCuadrillaObra
            select new RespuestaCuadrillaObra(
                co.IdCuadrillaObra,
                co.IdCuadrilla,
                c.Codigo,
                c.Nombre,
                co.IdObra,
                co.FechaInicio,
                co.FechaFin,
                co.Estado))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<IReadOnlyList<RespuestaCuadrillaObra>> ListarCuadrillasDeObraAsync(
        int idObra,
        CancellationToken cancelacion = default) =>
        await (
            from co in contexto.CuadrillasObra.AsNoTracking()
            join c in contexto.Cuadrillas.AsNoTracking() on co.IdCuadrilla equals c.IdCuadrilla
            where co.IdObra == idObra
            orderby co.IdCuadrillaObra
            select new RespuestaCuadrillaObra(
                co.IdCuadrillaObra,
                co.IdCuadrilla,
                c.Codigo,
                c.Nombre,
                co.IdObra,
                co.FechaInicio,
                co.FechaFin,
                co.Estado))
            .Buscar(paginacion, c => c.CodigoCuadrilla, c => c.NombreCuadrilla, c => c.Estado)
            .Paginar(paginacion).ToListAsync(cancelacion);

    public Task<bool> ExisteTrabajadorCuadrillaActivaAsync(
        int idAsignacion,
        CancellationToken cancelacion = default) =>
        contexto.TrabajadoresCuadrilla.AnyAsync(
            tc => tc.IdAsignacion == idAsignacion && tc.FechaFin == null,
            cancelacion);

    public async Task<RespuestaTrabajadorCuadrilla> AsignarTrabajadorACuadrillaAsync(
        SolicitudAsignarTrabajadorCuadrilla solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var relacion = new TrabajadorCuadrilla
        {
            IdAsignacion = solicitud.IdAsignacion,
            IdCuadrillaObra = solicitud.IdCuadrillaObra,
            FechaInicio = solicitud.FechaInicio,
            FechaFin = solicitud.FechaFin
        };

        contexto.TrabajadoresCuadrilla.Add(relacion);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = relacion.IdTrabajadorCuadrilla.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await (
            from tc in contexto.TrabajadoresCuadrilla.AsNoTracking()
            join a in contexto.AsignacionesTrabajadorObra.AsNoTracking() on tc.IdAsignacion equals a.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on a.IdTrabajador equals t.IdTrabajador
            join co in contexto.CuadrillasObra.AsNoTracking() on tc.IdCuadrillaObra equals co.IdCuadrillaObra
            join c in contexto.Cuadrillas.AsNoTracking() on co.IdCuadrilla equals c.IdCuadrilla
            where tc.IdTrabajadorCuadrilla == relacion.IdTrabajadorCuadrilla
            select new RespuestaTrabajadorCuadrilla(
                tc.IdTrabajadorCuadrilla,
                tc.IdAsignacion,
                a.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                tc.IdCuadrillaObra,
                co.IdCuadrilla,
                c.Nombre,
                co.IdObra,
                tc.FechaInicio,
                tc.FechaFin))
            .FirstAsync(cancelacion);
    }

    public async Task<IReadOnlyList<RespuestaTrabajadorCuadrilla>> ListarMiembrosCuadrillaObraAsync(
        int idObra,
        int? idCuadrillaObra = null,
        CancellationToken cancelacion = default)
    {
        var consulta =
            from tc in contexto.TrabajadoresCuadrilla.AsNoTracking()
            join a in contexto.AsignacionesTrabajadorObra.AsNoTracking() on tc.IdAsignacion equals a.IdAsignacion
            join t in contexto.Trabajadores.AsNoTracking() on a.IdTrabajador equals t.IdTrabajador
            join co in contexto.CuadrillasObra.AsNoTracking() on tc.IdCuadrillaObra equals co.IdCuadrillaObra
            join c in contexto.Cuadrillas.AsNoTracking() on co.IdCuadrilla equals c.IdCuadrilla
            where co.IdObra == idObra
                  && (idCuadrillaObra == null || tc.IdCuadrillaObra == idCuadrillaObra.Value)
            orderby tc.IdTrabajadorCuadrilla
            select new RespuestaTrabajadorCuadrilla(
                tc.IdTrabajadorCuadrilla,
                tc.IdAsignacion,
                a.IdTrabajador,
                t.PrimerNombre + " " + t.PrimerApellido,
                tc.IdCuadrillaObra,
                co.IdCuadrilla,
                c.Nombre,
                co.IdObra,
                tc.FechaInicio,
                tc.FechaFin);

        return await consulta.Buscar(paginacion, t => t.NombreCompletoTrabajador, t => t.NombreCuadrilla)
            .Paginar(paginacion).ToListAsync(cancelacion);
    }

    private static RespuestaTrabajador MapearTrabajador(
        Trabajador t,
        string nombreCargo,
        IReadOnlyList<RespuestaEspecialidadTrabajador> especialidades) =>
        new(
            t.IdTrabajador,
            t.IdCargo,
            nombreCargo,
            t.Cedula,
            t.PrimerNombre,
            t.SegundoNombre,
            t.PrimerApellido,
            t.SegundoApellido,
            t.Telefono,
            t.Correo,
            t.Direccion,
            t.FechaIngreso,
            t.Estado,
            especialidades);
}
