namespace ConstructoraEderTorres.Aplicacion.Comun;

public interface ICalendarioLaboral
{
    DateOnly Hoy { get; }

    DateOnly ObtenerFecha(DateTime instanteUtc);
}
