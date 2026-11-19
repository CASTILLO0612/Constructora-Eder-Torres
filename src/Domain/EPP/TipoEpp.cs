namespace ConstructoraEderTorres.Dominio.EPP;

public sealed class TipoEpp
{
    public int IdTipoEpp { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public int? VidaUtilDias { get; set; }

    public int ExistenciaMinima { get; set; }

    public bool Activo { get; set; }
}
