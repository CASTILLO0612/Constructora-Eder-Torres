using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

/// <summary>Conserva UTC y la precisión de segundos del esquema timestamptz(0) conciliado.</summary>
public sealed class ConvertidorFechaHoraUtc : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader lector, Type tipo, JsonSerializerOptions opciones)
    {
        if (!lector.TryGetDateTime(out var fecha) || fecha.Kind != DateTimeKind.Utc)
            throw new JsonException("La fecha y hora deben expresarse en UTC con sufijo Z.");
        return new DateTime(fecha.Ticks - fecha.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter escritor, DateTime valor, JsonSerializerOptions opciones) =>
        escritor.WriteStringValue(valor);
}
