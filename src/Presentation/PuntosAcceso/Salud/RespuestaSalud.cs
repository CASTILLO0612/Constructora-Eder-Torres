namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Salud;

public sealed record RespuestaSalud(
    string Estado,
    IReadOnlyDictionary<string, string> Componentes);
