using ConstructoraEderTorres.Aplicacion.Comun;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

internal static class PaginacionPostgreSql
{
    public static IQueryable<T> Paginar<T>(this IQueryable<T> consulta, PaginacionConsulta paginacion) =>
        consulta.Skip(paginacion.Omitir).Take(paginacion.TamanoPagina);
}
