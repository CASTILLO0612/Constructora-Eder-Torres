namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public sealed class AprobacionActa
{
    public int IdAprobacion { get; set; }

    public int IdActa { get; set; }

    public int IdUsuario { get; set; }

    public string Decision { get; set; } = null!;

    public string? Comentario { get; set; }

    public DateTime FechaHora { get; set; }
}
