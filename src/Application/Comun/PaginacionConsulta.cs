namespace ConstructoraEderTorres.Aplicacion.Comun;

public sealed record PaginacionConsulta(int Pagina = 1, int TamanoPagina = 100, string? Buscar = null)
{
    public int Omitir => checked((Pagina - 1) * TamanoPagina);
}
