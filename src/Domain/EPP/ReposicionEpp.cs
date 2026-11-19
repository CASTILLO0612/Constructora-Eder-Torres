namespace ConstructoraEderTorres.Dominio.EPP;

public sealed class ReposicionEpp
{
    public long IdReposicionEpp { get; set; }

    public long IdEntregaOrigen { get; set; }

    public long? IdEntregaNueva { get; set; }

    public string Motivo { get; set; } = null!;

    public string Descripcion { get; set; } = null!;

    public bool EquipoDevuelto { get; set; }

    public int IdUsuarioSolicita { get; set; }

    public DateTime SolicitadaEn { get; set; }

    public int? IdUsuarioAprueba { get; set; }

    public DateTime? ResueltaEn { get; set; }

    public string Estado { get; set; } = null!;

    public Guid UuidRegistro { get; set; }
}
