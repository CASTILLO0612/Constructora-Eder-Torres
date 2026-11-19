using System.Text.Json;
using ConstructoraEderTorres.Respaldos;

if (args.Length == 0 || args[0] == "--ayuda")
{
    Console.WriteLine("Acciones: crear, verificar, restaurar, depurar. Consulte docs/OperacionYRestauracion.md. Las conexiones se reciben por el entorno, nunca por argumentos.");
    return 0;
}
using var cancelacion = new CancellationTokenSource();
Console.CancelKeyPress += (_, evento) => { evento.Cancel = true; cancelacion.Cancel(); };
try
{
    var opciones = OpcionesRespaldos.Leer(args);
    cancelacion.CancelAfter(opciones.TiempoMaximo);
    var resultado = await new ServicioRespaldos().EjecutarAsync(opciones, cancelacion.Token);
    Console.WriteLine(JsonSerializer.Serialize(resultado, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (ExcepcionRespaldo error)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error.Codigo, error.Message }));
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("La operación se canceló; revise el directorio pendiente antes de reintentar.");
    return 130;
}
catch (Exception error)
{
    // No volcar excepciones que puedan contener detalles de conexión o contenido del servidor.
    Console.Error.WriteLine(JsonSerializer.Serialize(new { Codigo = "operacion_respaldo_fallida", Tipo = error.GetType().Name }));
    return 1;
}
