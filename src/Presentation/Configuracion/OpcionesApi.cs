namespace ConstructoraEderTorres.Presentacion.Configuracion;

public sealed class OpcionesApi
{
    public const string Seccion = "Api";
    public long MaximoBytesSolicitud { get; init; } = 24 * 1024 * 1024;
    public int TamanoPaginaPredeterminado { get; init; } = 100;
    public int TamanoPaginaMaximo { get; init; } = 500;
    public int MaximoElementosSolicitud { get; init; } = 200;
    public int IntentosInicioSesionPorMinuto { get; init; } = 10;
}
