using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;

namespace ConstructoraEderTorres.Infraestructura.Configuracion;

public sealed class CalendarioLaboral(OpcionesCapturaAsistencia opciones, IRelojSistema reloj) : ICalendarioLaboral
{
    private readonly TimeZoneInfo _zona = TimeZoneInfo.FindSystemTimeZoneById(opciones.ZonaHorariaJornada);

    public DateOnly Hoy => ObtenerFecha(reloj.AhoraUtc);

    public DateOnly ObtenerFecha(DateTime instanteUtc)
    {
        if (instanteUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("El instante debe estar expresado en UTC.", nameof(instanteUtc));
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(instanteUtc, _zona));
    }
}
