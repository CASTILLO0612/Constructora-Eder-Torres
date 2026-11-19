using System.Security.Cryptography;
using System.Text;
using System.Net;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed class AdaptadorSeguridadPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj,
    PaginacionConsulta paginacion) : IPuertoSeguridad
{
    public async Task InicializarCatalogoSeguridadAsync(CancellationToken cancelacion = default)
    {
        var permisosExistentes = await contexto.Permisos
            .ToDictionaryAsync(p => p.Codigo, StringComparer.OrdinalIgnoreCase, cancelacion);

        var huboCambiosPermisos = false;
        foreach (var defPermiso in CodigosPermiso.CatalogoBase)
        {
            if (!permisosExistentes.ContainsKey(defPermiso.Codigo))
            {
                var nuevoPermiso = new Permiso
                {
                    Codigo = defPermiso.Codigo,
                    Nombre = defPermiso.Nombre,
                    Descripcion = defPermiso.Descripcion,
                    Activo = true
                };
                contexto.Permisos.Add(nuevoPermiso);
                permisosExistentes[defPermiso.Codigo] = nuevoPermiso;
                huboCambiosPermisos = true;
            }
        }

        if (huboCambiosPermisos)
        {
            await contexto.SaveChangesAsync(cancelacion);
        }

        var rolesExistentes = await contexto.Roles
            .ToDictionaryAsync(r => r.Nombre, StringComparer.OrdinalIgnoreCase, cancelacion);

        var huboCambiosRoles = false;
        foreach (var defRol in CodigosPermiso.RolesIniciales)
        {
            if (!rolesExistentes.ContainsKey(defRol.Nombre))
            {
                var nuevoRol = new Rol
                {
                    Nombre = defRol.Nombre,
                    Descripcion = defRol.Descripcion,
                    Activo = true
                };
                contexto.Roles.Add(nuevoRol);
                rolesExistentes[defRol.Nombre] = nuevoRol;
                huboCambiosRoles = true;
            }
        }

        if (huboCambiosRoles)
        {
            await contexto.SaveChangesAsync(cancelacion);
        }

        var relacionesActuales = await contexto.RolesPermiso
            .Select(rp => new { rp.IdRol, rp.IdPermiso })
            .ToListAsync(cancelacion);
        var conjuntoRelaciones = relacionesActuales
            .Select(r => (r.IdRol, r.IdPermiso))
            .ToHashSet();

        var huboCambiosMatriz = false;
        foreach (var defRol in CodigosPermiso.RolesIniciales)
        {
            var rol = rolesExistentes[defRol.Nombre];
            foreach (var codigoPermiso in defRol.CodigosPermiso)
            {
                if (permisosExistentes.TryGetValue(codigoPermiso, out var permiso)
                    && conjuntoRelaciones.Add((rol.IdRol, permiso.IdPermiso)))
                {
                    contexto.RolesPermiso.Add(new RolPermiso
                    {
                        IdRol = rol.IdRol,
                        IdPermiso = permiso.IdPermiso
                    });
                    huboCambiosMatriz = true;
                }
            }
        }

        if (huboCambiosMatriz)
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
    }

    public Task<bool> ExisteNombreUsuarioAsync(
        string nombreUsuario,
        int? excluirIdUsuario = null,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios.AnyAsync(
            u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower()
                 && (excluirIdUsuario == null || u.IdUsuario != excluirIdUsuario.Value),
            cancelacion);

    public Task<bool> TrabajadorTieneUsuarioAsync(
        int idTrabajador,
        int? excluirIdUsuario = null,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios.AnyAsync(
            u => u.IdTrabajador == idTrabajador
                 && (excluirIdUsuario == null || u.IdUsuario != excluirIdUsuario.Value),
            cancelacion);

    public Task<bool> ExisteTrabajadorActivoAsync(
        int idTrabajador,
        CancellationToken cancelacion = default) =>
        contexto.Trabajadores.AnyAsync(
            t => t.IdTrabajador == idTrabajador && t.Estado == EstadosTrabajador.Activo,
            cancelacion);

    public async Task<RespuestaUsuario> CrearUsuarioAsync(
        string nombreUsuario,
        string contrasenaHash,
        int? idTrabajador,
        DateTime creadoEnUtc,
        DateOnly fechaRoles,
        IReadOnlyList<int> idsRoles,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var usuario = new Usuario
        {
            NombreUsuario = nombreUsuario,
            ContrasenaHash = contrasenaHash,
            IdTrabajador = idTrabajador,
            Estado = EstadosUsuario.Activo,
            CreadoEn = creadoEnUtc
        };

        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync(cancelacion);

        if (idsRoles.Count > 0)
        {
            var rolesValidos = await contexto.Roles
                .Where(r => idsRoles.Contains(r.IdRol) && r.Activo)
                .Select(r => r.IdRol)
                .ToListAsync(cancelacion);

            foreach (var idRol in rolesValidos)
            {
                contexto.UsuariosRol.Add(new UsuarioRol
                {
                    IdUsuario = usuario.IdUsuario,
                    IdRol = idRol,
                    FechaInicio = fechaRoles
                });
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = usuario.IdUsuario.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerUsuarioPorIdAsync(usuario.IdUsuario, cancelacion))!;
    }

    public async Task<RespuestaUsuario?> ObtenerUsuarioPorIdAsync(
        int idUsuario,
        CancellationToken cancelacion = default)
    {
        var usuario = await contexto.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);

        if (usuario is null)
        {
            return null;
        }

        var roles = await (from ur in contexto.UsuariosRol.AsNoTracking()
                           join r in contexto.Roles.AsNoTracking() on ur.IdRol equals r.IdRol
                           where ur.IdUsuario == idUsuario
                           orderby ur.FechaInicio descending
                           select new RolAsignadoRespuesta(
                               ur.IdUsuarioRol,
                               ur.IdRol,
                               r.Nombre,
                               ur.FechaInicio,
                               ur.FechaFin)).ToListAsync(cancelacion);

        return new RespuestaUsuario(
            usuario.IdUsuario,
            usuario.IdTrabajador,
            usuario.NombreUsuario,
            usuario.Estado,
            usuario.CreadoEn,
            usuario.UltimoAcceso,
            roles);
    }

    public async Task<IReadOnlyList<RespuestaUsuario>> ListarUsuariosAsync(
        string? estado = null,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Usuarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado))
        {
            consulta = consulta.Where(u => u.Estado == estado);
        }

        var usuarios = await consulta
            .OrderBy(u => u.IdUsuario)
            .Paginar(paginacion).ToListAsync(cancelacion);

        var ids = usuarios.Select(u => u.IdUsuario).ToArray();
        var rolesPorUsuario = await (from ur in contexto.UsuariosRol.AsNoTracking()
                                     join r in contexto.Roles.AsNoTracking() on ur.IdRol equals r.IdRol
                                     where ids.Contains(ur.IdUsuario)
                                     select new
                                     {
                                         ur.IdUsuario,
                                         Rol = new RolAsignadoRespuesta(
                                             ur.IdUsuarioRol,
                                             ur.IdRol,
                                             r.Nombre,
                                             ur.FechaInicio,
                                             ur.FechaFin)
                                     }).ToListAsync(cancelacion);

        var mapaRoles = rolesPorUsuario
            .GroupBy(x => x.IdUsuario)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RolAsignadoRespuesta>)g.Select(x => x.Rol).ToList());

        return usuarios.Select(u => new RespuestaUsuario(
            u.IdUsuario,
            u.IdTrabajador,
            u.NombreUsuario,
            u.Estado,
            u.CreadoEn,
            u.UltimoAcceso,
            mapaRoles.GetValueOrDefault(u.IdUsuario, []))).ToList();
    }

    public async Task<RespuestaUsuario?> ActualizarUsuarioAsync(
        int idUsuario,
        string nombreUsuario,
        int? idTrabajador,
        string? nuevaContrasenaHash,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var usuario = await contexto.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return null;
        }

        usuario.NombreUsuario = nombreUsuario;
        usuario.IdTrabajador = idTrabajador;
        if (!string.IsNullOrWhiteSpace(nuevaContrasenaHash))
        {
            usuario.ContrasenaHash = nuevaContrasenaHash;
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return await ObtenerUsuarioPorIdAsync(idUsuario, cancelacion);
    }

    public async Task<RespuestaUsuario?> CambiarEstadoUsuarioAsync(
        int idUsuario,
        string nuevoEstado,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var usuario = await contexto.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return null;
        }

        usuario.Estado = nuevoEstado;
        if (nuevoEstado != EstadosUsuario.Activo)
        {
            var ahora = reloj.AhoraUtc;
            await contexto.SesionesUsuario
                .Where(s => s.IdUsuario == idUsuario && s.RevocadaEnUtc == null)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(s => s.RevocadaEnUtc, ahora), cancelacion);
        }
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return await ObtenerUsuarioPorIdAsync(idUsuario, cancelacion);
    }

    public async Task<IReadOnlyList<RespuestaPermiso>> ListarPermisosAsync(
        CancellationToken cancelacion = default) =>
        await contexto.Permisos.AsNoTracking()
            .OrderBy(p => p.Codigo)
            .Select(p => new RespuestaPermiso(p.IdPermiso, p.Codigo, p.Nombre, p.Descripcion, p.Activo))
            .Paginar(paginacion).ToListAsync(cancelacion);

    public async Task<IReadOnlyList<RespuestaRol>> ListarRolesAsync(
        CancellationToken cancelacion = default)
    {
        var roles = await contexto.Roles.AsNoTracking().OrderBy(r => r.Nombre).Paginar(paginacion).ToListAsync(cancelacion);
        var idsRoles = roles.Select(r => r.IdRol).ToArray();
        var permisosPorRol = await (from rp in contexto.RolesPermiso.AsNoTracking()
                                    join p in contexto.Permisos.AsNoTracking() on rp.IdPermiso equals p.IdPermiso
                                    where p.Activo && idsRoles.Contains(rp.IdRol)
                                    select new { rp.IdRol, p.Codigo }).ToListAsync(cancelacion);

        var mapa = permisosPorRol
            .GroupBy(x => x.IdRol)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Codigo).Order().ToList());

        return roles.Select(r => new RespuestaRol(
            r.IdRol,
            r.Nombre,
            r.Descripcion,
            r.Activo,
            mapa.GetValueOrDefault(r.IdRol, []))).ToList();
    }

    public async Task<RespuestaRol?> ObtenerRolPorIdAsync(
        int idRol,
        CancellationToken cancelacion = default)
    {
        var rol = await contexto.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.IdRol == idRol, cancelacion);
        if (rol is null)
        {
            return null;
        }

        var codigos = await (from rp in contexto.RolesPermiso.AsNoTracking()
                             join p in contexto.Permisos.AsNoTracking() on rp.IdPermiso equals p.IdPermiso
                             where rp.IdRol == idRol && p.Activo
                             orderby p.Codigo
                             select p.Codigo).ToListAsync(cancelacion);

        return new RespuestaRol(rol.IdRol, rol.Nombre, rol.Descripcion, rol.Activo, codigos);
    }

    public Task<bool> ExisteRolPorNombreAsync(
        string nombre,
        int? excluirIdRol = null,
        CancellationToken cancelacion = default) =>
        contexto.Roles.AnyAsync(
            r => r.Nombre.ToLower() == nombre.ToLower()
                 && (excluirIdRol == null || r.IdRol != excluirIdRol.Value),
            cancelacion);

    public async Task<RespuestaRol> CrearRolAsync(
        string nombre,
        string? descripcion,
        IReadOnlyList<string> codigosPermiso,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var rol = new Rol
        {
            Nombre = nombre,
            Descripcion = descripcion,
            Activo = true
        };
        contexto.Roles.Add(rol);
        await contexto.SaveChangesAsync(cancelacion);

        if (codigosPermiso.Count > 0)
        {
            var permisos = await contexto.Permisos
                .Where(p => codigosPermiso.Contains(p.Codigo) && p.Activo)
                .Select(p => p.IdPermiso)
                .ToListAsync(cancelacion);

            foreach (var idPermiso in permisos)
            {
                contexto.RolesPermiso.Add(new RolPermiso
                {
                    IdRol = rol.IdRol,
                    IdPermiso = idPermiso
                });
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = rol.IdRol.ToString() },
            reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return (await ObtenerRolPorIdAsync(rol.IdRol, cancelacion))!;
    }

    public async Task<RespuestaRol?> ActualizarPermisosDeRolAsync(
        int idRol,
        IReadOnlyList<string> codigosPermiso,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var rol = await contexto.Roles.FirstOrDefaultAsync(r => r.IdRol == idRol, cancelacion);
        if (rol is null)
        {
            return null;
        }

        var actuales = await contexto.RolesPermiso
            .Where(rp => rp.IdRol == idRol)
            .ToListAsync(cancelacion);
        contexto.RolesPermiso.RemoveRange(actuales);

        if (codigosPermiso.Count > 0)
        {
            var permisos = await contexto.Permisos
                .Where(p => codigosPermiso.Contains(p.Codigo) && p.Activo)
                .Select(p => p.IdPermiso)
                .ToListAsync(cancelacion);

            foreach (var idPermiso in permisos)
            {
                contexto.RolesPermiso.Add(new RolPermiso
                {
                    IdRol = idRol,
                    IdPermiso = idPermiso
                });
            }
        }

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return await ObtenerRolPorIdAsync(idRol, cancelacion);
    }

    public async Task<RolAsignadoRespuesta?> AsignarRolAUsuarioAsync(
        int idUsuario,
        int idRol,
        DateOnly fechaInicio,
        DateOnly? fechaFin,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var rol = await contexto.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.IdRol == idRol, cancelacion);
        if (rol is null)
        {
            return null;
        }

        var existenteActivo = await contexto.UsuariosRol
            .FirstOrDefaultAsync(
                ur => ur.IdUsuario == idUsuario && ur.IdRol == idRol && ur.FechaFin == null,
                cancelacion);

        if (existenteActivo is not null)
        {
            existenteActivo.FechaFin = fechaFin;
            contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
            await contexto.SaveChangesAsync(cancelacion);

            return new RolAsignadoRespuesta(
                existenteActivo.IdUsuarioRol,
                existenteActivo.IdRol,
                rol.Nombre,
                existenteActivo.FechaInicio,
                existenteActivo.FechaFin);
        }

        var nuevo = new UsuarioRol
        {
            IdUsuario = idUsuario,
            IdRol = idRol,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin
        };
        contexto.UsuariosRol.Add(nuevo);
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return new RolAsignadoRespuesta(
            nuevo.IdUsuarioRol,
            nuevo.IdRol,
            rol.Nombre,
            nuevo.FechaInicio,
            nuevo.FechaFin);
    }

    public async Task<CredencialesUsuarioAlmacenadas?> ObtenerCredencialesPorNombreUsuarioAsync(
        string nombreUsuario,
        CancellationToken cancelacion = default) =>
        await contexto.Usuarios.AsNoTracking()
            .Where(u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower())
            .Select(u => new CredencialesUsuarioAlmacenadas(
                u.IdUsuario,
                u.NombreUsuario,
                u.ContrasenaHash,
                u.Estado))
            .FirstOrDefaultAsync(cancelacion);

    public async Task RegistrarUltimoAccesoAsync(
        int idUsuario,
        DateTime instanteUtc,
        CancellationToken cancelacion = default)
    {
        var usuario = await contexto.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is not null)
        {
            usuario.UltimoAcceso = instanteUtc;
            await contexto.SaveChangesAsync(cancelacion);
        }
    }

    public async Task<UsuarioAutenticado?> ConstruirContextoUsuarioActivoAsync(
        int idUsuario,
        DateOnly fechaVigencia,
        IPAddress? ipOrigen = null,
        CancellationToken cancelacion = default,
        Guid? idSesion = null,
        DateTime? ahoraUtc = null)
    {
        var usuario = await contexto.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario && u.Estado == EstadosUsuario.Activo
                && (idSesion == null || contexto.SesionesUsuario.Any(s => s.IdSesion == idSesion && s.IdUsuario == idUsuario
                    && s.RevocadaEnUtc == null && s.ExpiraEnUtc > ahoraUtc)), cancelacion);

        if (usuario is null)
        {
            return null;
        }

        var rolesVigentes = await (from ur in contexto.UsuariosRol.AsNoTracking()
                                   join r in contexto.Roles.AsNoTracking() on ur.IdRol equals r.IdRol
                                   where ur.IdUsuario == idUsuario
                                         && r.Activo
                                         && ur.FechaInicio <= fechaVigencia
                                         && (ur.FechaFin == null || ur.FechaFin >= fechaVigencia)
                                   select new { r.IdRol, r.Nombre }).ToListAsync(cancelacion);

        if (rolesVigentes.Count == 0 || usuario.IdTrabajador is int trabajadorVinculado
            && !await contexto.Trabajadores.AnyAsync(t => t.IdTrabajador == trabajadorVinculado && t.Estado == EstadosTrabajador.Activo, cancelacion))
            return null;

        var idsRoles = rolesVigentes.Select(r => r.IdRol).Distinct().ToArray();
        var nombresRoles = rolesVigentes
            .Select(r => r.Nombre)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (nombresRoles.Contains(NombresRol.Trabajador) && usuario.IdTrabajador is null)
            return null;

        var permisos = idsRoles.Length == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await (from rp in contexto.RolesPermiso.AsNoTracking()
                      join p in contexto.Permisos.AsNoTracking() on rp.IdPermiso equals p.IdPermiso
                      where idsRoles.Contains(rp.IdRol) && p.Activo
                      select p.Codigo).ToListAsync(cancelacion))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var obrasAsignadas = new HashSet<int>();
        if (usuario.IdTrabajador is int idTrabajador)
        {
            var idsObras = await contexto.AsignacionesTrabajadorObra.AsNoTracking()
                .Where(a => a.IdTrabajador == idTrabajador
                            && a.Estado == EstadosAsignacionObra.Activa
                            && a.FechaInicio <= fechaVigencia
                            && (a.FechaFin == null || a.FechaFin >= fechaVigencia))
                .Select(a => a.IdObra)
                .ToListAsync(cancelacion);

            obrasAsignadas.UnionWith(idsObras);
        }

        return new UsuarioAutenticado(
            usuario.IdUsuario,
            usuario.NombreUsuario,
            usuario.IdTrabajador,
            nombresRoles,
            permisos,
            obrasAsignadas,
            ipOrigen,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(usuario.ContrasenaHash))));
    }

    public async Task<IReadOnlyList<RespuestaAuditoria>> ConsultarAuditoriasAsync(
        FiltroAuditoria filtro,
        CancellationToken cancelacion = default)
    {
        var consulta = from a in contexto.Auditorias.AsNoTracking()
                       join u in contexto.Usuarios.AsNoTracking() on a.IdUsuario equals u.IdUsuario into usuariosJoin
                       from u in usuariosJoin.DefaultIfEmpty()
                       select new { Auditoria = a, NombreUsuario = u != null ? u.NombreUsuario : null };

        if (!string.IsNullOrWhiteSpace(filtro.Entidad))
        {
            var entidad = filtro.Entidad.Trim().ToLowerInvariant();
            consulta = consulta.Where(x => x.Auditoria.Entidad.ToLower() == entidad);
        }

        if (!string.IsNullOrWhiteSpace(filtro.IdRegistro))
        {
            var idReg = filtro.IdRegistro.Trim();
            consulta = consulta.Where(x => x.Auditoria.IdRegistro == idReg);
        }

        if (filtro.IdUsuario is int idUsuario)
        {
            consulta = consulta.Where(x => x.Auditoria.IdUsuario == idUsuario);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Accion))
        {
            var accion = filtro.Accion.Trim().ToUpperInvariant();
            consulta = consulta.Where(x => x.Auditoria.Accion == accion);
        }

        if (filtro.DesdeUtc is DateTime desde)
        {
            consulta = consulta.Where(x => x.Auditoria.FechaHora >= desde);
        }

        if (filtro.HastaUtc is DateTime hasta)
        {
            consulta = consulta.Where(x => x.Auditoria.FechaHora <= hasta);
        }

        var filas = await consulta
            .OrderByDescending(x => x.Auditoria.FechaHora)
            .ThenByDescending(x => x.Auditoria.IdAuditoria)
            .Take(filtro.Limite)
            .ToListAsync(cancelacion);

        return filas.Select(x => new RespuestaAuditoria(
            x.Auditoria.IdAuditoria,
            x.Auditoria.IdUsuario,
            x.NombreUsuario,
            x.Auditoria.Entidad,
            x.Auditoria.IdRegistro,
            x.Auditoria.Accion,
            x.Auditoria.Campo,
            x.Auditoria.ValorAnterior,
            x.Auditoria.ValorNuevo,
            x.Auditoria.FechaHora,
            x.Auditoria.IpOrigen?.ToString(),
            x.Auditoria.UuidOperacion)).ToList();
    }
}
