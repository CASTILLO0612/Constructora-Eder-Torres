using System.Net;

namespace ConstructoraEderTorres.Dominio.Seguridad;

public sealed class Auditoria
{
    public long IdAuditoria { get; set; }

    public int? IdUsuario { get; set; }

    public string Entidad { get; set; } = null!;

    public string IdRegistro { get; set; } = null!;

    public string Accion { get; set; } = null!;

    public string? Campo { get; set; }

    public string? ValorAnterior { get; set; }

    public string? ValorNuevo { get; set; }

    public DateTime FechaHora { get; set; }

    public IPAddress? IpOrigen { get; set; }

    public Guid UuidOperacion { get; set; }
}
