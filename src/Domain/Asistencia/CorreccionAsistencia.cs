namespace ConstructoraEderTorres.Dominio.Asistencia;

public sealed class CorreccionAsistencia
{
    public long IdCorreccion { get; set; }

    public long IdAsistencia { get; set; }

    public int IdUsuarioSolicita { get; set; }

    public int? IdUsuarioAprueba { get; set; }

    public DateTime? EntradaAnterior { get; set; }

    public DateTime? SalidaAnterior { get; set; }

    public DateTime? EntradaNueva { get; set; }

    public DateTime? SalidaNueva { get; set; }

    public string Motivo { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public DateTime SolicitadoEn { get; set; }

    public DateTime? ResueltoEn { get; set; }
}
