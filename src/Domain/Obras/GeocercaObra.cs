namespace ConstructoraEderTorres.Dominio.Obras;

public readonly record struct GeocercaObra(decimal Latitud, decimal Longitud, decimal RadioMetros)
{
    // Radio terrestre medio de la esfera de referencia IUGG, expresado en metros.
    private const double RadioTerrestreMetros = 6_371_008.8;

    public static bool SonCoordenadasValidas(decimal? latitud, decimal? longitud) =>
        latitud is >= -90m and <= 90m && longitud is >= -180m and <= 180m;

    public static bool EsConfiguracionValida(decimal? latitud, decimal? longitud, decimal? radioMetros) =>
        SonCoordenadasValidas(latitud, longitud) && radioMetros is > 0m and <= 99_999_999.99m;

    public double CalcularDistanciaMetros(decimal latitud, decimal longitud)
    {
        var latitudOrigen = ConvertirARadianes(Latitud);
        var latitudDestino = ConvertirARadianes(latitud);
        var diferenciaLatitud = latitudDestino - latitudOrigen;
        var diferenciaLongitud = ConvertirARadianes(longitud - Longitud);
        var haverseno = Math.Pow(Math.Sin(diferenciaLatitud / 2), 2)
            + Math.Cos(latitudOrigen) * Math.Cos(latitudDestino)
            * Math.Pow(Math.Sin(diferenciaLongitud / 2), 2);
        return 2 * RadioTerrestreMetros * Math.Asin(Math.Sqrt(Math.Clamp(haverseno, 0, 1)));
    }

    public bool Contiene(decimal latitud, decimal longitud) =>
        CalcularDistanciaMetros(latitud, longitud) <= (double)RadioMetros;

    private static double ConvertirARadianes(decimal grados) => (double)grados * Math.PI / 180;
}
