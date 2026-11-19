namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public sealed class TrabajadorActa
{
    public int IdActa { get; set; }

    public int IdAsignacion { get; set; }

    public string TipoParticipacion { get; set; } = null!;

    public string? RutaFirma { get; set; }

    public DateTime? FirmadoEn { get; set; }
}
