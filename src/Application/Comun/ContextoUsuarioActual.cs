using System.Net;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.Comun;

public sealed record UsuarioAutenticado(
    int IdUsuario,
    string NombreUsuario,
    int? IdTrabajador,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permisos,
    IReadOnlySet<int> ObrasAsignadas,
    IPAddress? IpOrigen = null,
    string SelloCredenciales = "",
    DateTime? AutenticadoEnUtc = null)
{
    public bool TieneAlcanceGlobal =>
        Roles.Any(rol => NombresRol.ConAlcanceGlobalObras.Contains(rol));

    public bool TienePermiso(string codigoPermiso) =>
        Permisos.Contains(codigoPermiso);

    public bool TieneAccesoAObra(int idObra) =>
        TieneAlcanceGlobal || ObrasAsignadas.Contains(idObra);
}

public interface IContextoUsuarioActual
{
    UsuarioAutenticado? ObtenerUsuario();
}

public static class AutorizadorOperacion
{
    public static Resultado<UsuarioAutenticado> ExigirPermiso(
        IContextoUsuarioActual contextoUsuario,
        string codigoPermiso)
    {
        var usuario = contextoUsuario.ObtenerUsuario();
        if (usuario is null)
        {
            return Resultado<UsuarioAutenticado>.Fallo(
                ErrorNegocio.NoAutenticado(
                    "sesion_requerida",
                    "Debe iniciar sesión para ejecutar esta operación."));
        }

        if (!usuario.TienePermiso(codigoPermiso))
        {
            return Resultado<UsuarioAutenticado>.Fallo(
                ErrorNegocio.Prohibido(
                    "permiso_denegado",
                    "No cuenta con el permiso requerido para realizar esta operación."));
        }

        return Resultado<UsuarioAutenticado>.Exito(usuario);
    }

    public static Resultado<UsuarioAutenticado> ExigirPermisoYObra(
        IContextoUsuarioActual contextoUsuario,
        string codigoPermiso,
        int idObra)
    {
        var validacionPermiso = ExigirPermiso(contextoUsuario, codigoPermiso);
        if (!validacionPermiso.EsExitoso)
        {
            return validacionPermiso;
        }

        var usuario = validacionPermiso.Valor!;
        if (!usuario.TieneAccesoAObra(idObra))
        {
            return Resultado<UsuarioAutenticado>.Fallo(
                ErrorNegocio.Prohibido(
                    "obra_fuera_de_alcance",
                    "No tiene autorización para consultar o modificar registros de esta obra."));
        }

        return Resultado<UsuarioAutenticado>.Exito(usuario);
    }
}
