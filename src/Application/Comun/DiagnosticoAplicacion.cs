using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConstructoraEderTorres.Aplicacion.Comun;

/// <summary>Solo etiquetas de catálogo; nunca identidades, GPS, UUID, archivos ni contenido de solicitudes.</summary>
public static class DiagnosticoAplicacion
{
    public const string Nombre = "ConstructoraEderTorres.Api";
    public static readonly ActivitySource Fuente = new(Nombre);
    private static readonly Meter Medidor = new(Nombre);
    public static readonly Histogram<double> DuracionApi = Medidor.CreateHistogram<double>("cet.api.duracion", "s");
    public static readonly Counter<long> RespuestasApi = Medidor.CreateCounter<long>("cet.api.respuestas");
    public static readonly Histogram<double> DuracionTransaccion = Medidor.CreateHistogram<double>("cet.persistencia.duracion", "s");
    public static readonly Counter<long> ReintentosTransaccion = Medidor.CreateCounter<long>("cet.persistencia.reintentos");
    public static readonly Counter<long> OperacionesSincronizacion = Medidor.CreateCounter<long>("cet.sincronizacion.operaciones");
    public static readonly Counter<long> FallosEvidencias = Medidor.CreateCounter<long>("cet.evidencias.fallos");
}
