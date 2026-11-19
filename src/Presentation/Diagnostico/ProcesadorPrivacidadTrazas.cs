using System.Diagnostics;
using OpenTelemetry;

namespace ConstructoraEderTorres.Presentacion.Diagnostico;

public sealed class ProcesadorPrivacidadTrazas : BaseProcessor<Activity>
{
    private static readonly HashSet<string> EtiquetasPermitidas =
    ["http.request.method", "http.response.status_code", "http.route", "network.protocol.version",
        "error.type", "cet.resultado", "cet.aislamiento"];

    public override void OnEnd(Activity actividad)
    {
        // Los eventos y enlaces no se pueden editar de forma segura: se descarta esa traza antes del exportador.
        if (actividad.Events.Any() || actividad.Links.Any(e => e.Tags?.Any() == true))
            actividad.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        foreach (var etiqueta in actividad.TagObjects.ToArray())
            if (!EtiquetasPermitidas.Contains(etiqueta.Key)) actividad.SetTag(etiqueta.Key, null);
        foreach (var elemento in actividad.Baggage.ToArray()) actividad.SetBaggage(elemento.Key, null);
        actividad.TraceStateString = null;
        actividad.SetStatus(actividad.Status);
        if (actividad.Kind == ActivityKind.Server)
        {
            var metodo = actividad.GetTagItem("http.request.method") as string ?? "HTTP";
            var ruta = actividad.GetTagItem("http.route") as string ?? "/sin_ruta";
            actividad.DisplayName = metodo + " " + ruta;
        }
    }
}
