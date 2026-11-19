using ConstructoraEderTorres.Dominio.Obras;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class GeocercaObraPruebas
{
    [Theory]
    [InlineData(0, 0, 0, 0.0008, true)]
    [InlineData(0, 0, 0, 0.001, false)]
    [InlineData(0, 179.9999, 0, -179.9999, true)]
    [InlineData(89.9999, 0, 89.9999, 180, true)]
    [InlineData(0, 0, 0, 180, false)]
    public void DistanciaEsfericaResuelveBordePolosYAntimeridiano(
        double latitudObra, double longitudObra, double latitudLectura, double longitudLectura, bool dentro)
    {
        var geocerca = new GeocercaObra((decimal)latitudObra, (decimal)longitudObra, 100m);
        Assert.Equal(dentro, geocerca.Contiene((decimal)latitudLectura, (decimal)longitudLectura));
        Assert.True(double.IsFinite(geocerca.CalcularDistanciaMetros((decimal)latitudLectura, (decimal)longitudLectura)));
    }

    [Fact]
    public void DistanciaCoincideConArcoConocidoYElLimiteEsInclusivo()
    {
        var geocerca = new GeocercaObra(0, 0, 100);
        Assert.InRange(geocerca.CalcularDistanciaMetros(0, 0.00089932m), 99.99, 100.01);
        Assert.Equal(0, geocerca.CalcularDistanciaMetros(0, 0));
        Assert.True(geocerca.Contiene(0, 0.000899m));
        Assert.False(geocerca.Contiene(0, 0.000900m));
    }
}
