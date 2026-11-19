using ConstructoraEderTorres.Aplicacion.Comun;

namespace ConstructoraEderTorres.Infraestructura.Configuracion;

public sealed class RelojSistemaUtc(TimeProvider tiempo) : IRelojSistema
{
    public DateTime AhoraUtc
    {
        get
        {
            var ahora = tiempo.GetUtcNow().UtcDateTime;
            return new DateTime(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        }
    }

}
