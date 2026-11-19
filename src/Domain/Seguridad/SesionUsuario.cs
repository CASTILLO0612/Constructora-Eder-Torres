namespace ConstructoraEderTorres.Dominio.Seguridad;

/// <summary>Persistencia técnica de la vigencia/revocación de cada sesión; no almacena cookies ni credenciales.</summary>
public sealed class SesionUsuario
{
    public Guid IdSesion { get; set; }
    public int IdUsuario { get; set; }
    public DateTime EmitidaEnUtc { get; set; }
    public DateTime ExpiraEnUtc { get; set; }
    public DateTime? RevocadaEnUtc { get; set; }
}
