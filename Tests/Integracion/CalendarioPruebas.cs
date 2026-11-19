using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;

namespace ConstructoraEderTorres.Pruebas.Integracion;

internal static class CalendarioPruebas
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(new OpcionesCapturaAsistencia().ZonaHorariaJornada);

    public static DateOnly Hoy => Fecha(DateTime.UtcNow);

    public static DateOnly Fecha(DateTime instanteUtc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(instanteUtc, Zona));

    public static DateTime InstanteUtc(DateOnly fecha, TimeOnly hora) =>
        TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(hora, DateTimeKind.Unspecified), Zona);
}
