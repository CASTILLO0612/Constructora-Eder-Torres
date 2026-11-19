using Xunit;

[assembly: CollectionBehavior(MaxParallelThreads = 2)]

namespace ConstructoraEderTorres.Pruebas.Integracion;

/// <summary>La medición de latencia no compite con otras baterías de la misma ejecución.</summary>
[CollectionDefinition(ColeccionPruebasRendimiento.Nombre, DisableParallelization = true)]
public sealed class ColeccionPruebasRendimiento
{
    public const string Nombre = "MedicionRendimientoApi";
}
