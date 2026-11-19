namespace ConstructoraEderTorres.Infraestructura.Configuracion;

public sealed class OpcionesTransacciones
{
    public const string Seccion = "Persistencia:Transacciones";
    public int MaximoIntentos { get; init; } = 3;
    public TimeSpan DemoraBaseReintento { get; init; } = TimeSpan.FromMilliseconds(20);
    public TimeSpan TiempoMaximoReversion { get; init; } = TimeSpan.FromSeconds(5);

    public void Validar()
    {
        if (MaximoIntentos < 1 || DemoraBaseReintento < TimeSpan.Zero || TiempoMaximoReversion <= TimeSpan.Zero)
            throw new InvalidOperationException("La configuración de intentos, demora y reversión de transacciones no es válida.");
    }
}
