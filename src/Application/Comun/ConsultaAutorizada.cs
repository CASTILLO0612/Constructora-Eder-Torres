namespace ConstructoraEderTorres.Aplicacion.Comun;

internal static class ConsultaAutorizada
{
    public static async Task<Resultado<IReadOnlyList<T>>> ListarAsync<T>(
        IContextoUsuarioActual contexto, IReadOnlyList<string> permisos, int? idObra,
        Func<IReadOnlySet<int>?, Task<IReadOnlyList<T>>> consultar)
    {
        var usuario = contexto.ObtenerUsuario();
        if (usuario is null)
            return Resultado<IReadOnlyList<T>>.Fallo(ErrorNegocio.NoAutenticado("sesion_requerida", "Debe iniciar sesión."));
        if (!permisos.Any(usuario.TienePermiso))
            return Resultado<IReadOnlyList<T>>.Fallo(ErrorNegocio.Prohibido("permiso_denegado", "No cuenta con permiso para consultar estos recursos."));
        if (idObra is { } obra && !usuario.TieneAccesoAObra(obra))
            return Resultado<IReadOnlyList<T>>.Fallo(ErrorNegocio.Prohibido("obra_fuera_de_alcance", "La obra no está autorizada."));
        return Resultado<IReadOnlyList<T>>.Exito(await consultar(usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas));
    }

    public static async Task<Resultado<T>> ObtenerAsync<T>(
        IContextoUsuarioActual contexto, IReadOnlyList<string> permisos,
        Func<Task<T?>> consultar, Func<T, int>? obtenerObra = null) where T : class
    {
        var usuario = contexto.ObtenerUsuario();
        if (usuario is null)
            return Resultado<T>.Fallo(ErrorNegocio.NoAutenticado("sesion_requerida", "Debe iniciar sesión."));
        if (!permisos.Any(usuario.TienePermiso))
            return Resultado<T>.Fallo(ErrorNegocio.Prohibido("permiso_denegado", "No cuenta con permiso para consultar este recurso."));
        var recurso = await consultar();
        if (recurso is null)
            return Resultado<T>.Fallo(ErrorNegocio.NoEncontrado("recurso_no_encontrado", "El recurso solicitado no existe."));
        if (obtenerObra is not null && !usuario.TieneAccesoAObra(obtenerObra(recurso)))
            return Resultado<T>.Fallo(ErrorNegocio.Prohibido("obra_fuera_de_alcance", "La obra del recurso no está autorizada."));
        return Resultado<T>.Exito(recurso);
    }
}
