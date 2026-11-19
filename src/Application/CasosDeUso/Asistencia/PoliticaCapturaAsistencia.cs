using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Dominio.Obras;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;

public sealed class PoliticaCapturaAsistencia(
    IRelojSistema reloj, ICalendarioLaboral calendario, OpcionesCapturaAsistencia opciones)
{
    public Resultado ValidarInstantes(DateTime entrada, DateTime? salida)
    {
        if (entrada.Kind != DateTimeKind.Utc || salida is { Kind: not DateTimeKind.Utc })
            return Resultado.Fallo(ErrorNegocio.Validacion("instante_no_utc", "Los instantes de asistencia deben expresarse en UTC."));
        var ahora = reloj.AhoraUtc;
        if (EstaAdelantado(entrada, ahora) || salida is { } fin && EstaAdelantado(fin, ahora))
            return Resultado.Fallo(ErrorNegocio.Validacion("captura_futura",
                "La captura supera la tolerancia de reloj futuro configurada. Corrija el reloj del dispositivo."));
        if (salida < entrada)
            return Resultado.Fallo(ErrorNegocio.Validacion("horas_asistencia_invalidas", "La salida no puede preceder a la entrada."));
        return Resultado.Exito();
    }

    public Resultado ValidarJornada(DateOnly fechaJornada, DateTime entradaUtc) =>
        calendario.ObtenerFecha(entradaUtc) == fechaJornada ? Resultado.Exito()
            : Resultado.Fallo(ErrorNegocio.Validacion("fecha_jornada_inconsistente",
                "La fecha de jornada debe corresponder a la fecha local de la entrada en la zona horaria de la obra."));

    public static Resultado ValidarPrecision(decimal? precision)
    {
        if (precision is null)
            return Resultado.Fallo(ErrorNegocio.Validacion("precision_gps_obligatoria",
                "El dispositivo debe informar la precisión GPS en metros antes de marcar."));
        if (precision is < 0m or > 99_999_999.99m)
            return Resultado.Fallo(ErrorNegocio.Validacion("precision_gps_invalida",
                "La precisión GPS debe ser un número no negativo en metros dentro del límite de almacenamiento."));
        return Resultado.Exito();
    }

    // Redondear hacia arriba evita reducir artificialmente la incertidumbre informada por el dispositivo.
    public static decimal NormalizarPrecision(decimal precision) => decimal.Ceiling(precision * 100m) / 100m;

    public static Resultado ValidarUbicacion(GeocercaObra geocerca, decimal latitud, decimal longitud, decimal precision)
    {
        var distancia = geocerca.CalcularDistanciaMetros(latitud, longitud);
        if (distancia > (double)geocerca.RadioMetros)
            return Resultado.Fallo(ErrorNegocio.Validacion("ubicacion_fuera_de_obra", "La ubicación GPS está fuera del radio de la obra."));
        if (distancia + (double)precision > (double)geocerca.RadioMetros)
            return Resultado.Fallo(ErrorNegocio.Validacion("gps_impreciso",
                "La incertidumbre del GPS sobrepasa el radio de la obra. Obtenga una lectura más precisa dentro del radio."));
        return Resultado.Exito();
    }

    private bool EstaAdelantado(DateTime captura, DateTime ahora) =>
        captura > ahora && captura - ahora > opciones.ToleranciaRelojFuturo;
}
