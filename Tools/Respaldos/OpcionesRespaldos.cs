namespace ConstructoraEderTorres.Respaldos;

public sealed class ExcepcionRespaldo(string codigo, string mensaje) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
}

public sealed record OpcionesRespaldos(string Accion, IReadOnlyDictionary<string,string> Valores)
{
    public TimeSpan TiempoMaximo => TimeSpan.FromMinutes(Entero("tiempo-maximo-minutos", 30));
    public string Exigir(string nombre) => Valores.TryGetValue(nombre, out var valor) && !string.IsNullOrWhiteSpace(valor)
        ? valor : throw new ExcepcionRespaldo("opcion_obligatoria", $"Falta --{nombre}.");
    public int Entero(string nombre, int? predeterminado = null)
    {
        if (!Valores.TryGetValue(nombre, out var valor) && predeterminado is { } numero) return numero;
        return int.TryParse(valor, out var resultado) && resultado > 0 && resultado <= 100_000
            ? resultado : throw new ExcepcionRespaldo("opcion_invalida", $"--{nombre} requiere un entero positivo.");
    }

    public static OpcionesRespaldos Leer(string[] argumentos)
    {
        if (argumentos.Length == 0 || argumentos[0] is not ("crear" or "verificar" or "restaurar" or "depurar"))
            throw new ExcepcionRespaldo("accion_invalida", "La acción de respaldo no es válida.");
        var opciones = new Dictionary<string,string>(StringComparer.Ordinal);
        var permitidas = new HashSet<string> { "destino", "evidencias", "binarios", "base-esperada", "carpeta",
            "destino-evidencias", "retencion-dias", "conservar-minimo", "tiempo-maximo-minutos" };
        for (var i = 1; i < argumentos.Length; i += 2)
        {
            var clave = argumentos[i];
            if (!clave.StartsWith("--", StringComparison.Ordinal) || !permitidas.Contains(clave[2..])
                || i + 1 >= argumentos.Length || !opciones.TryAdd(clave[2..], argumentos[i+1]))
                throw new ExcepcionRespaldo("opcion_invalida", "Hay opciones desconocidas, duplicadas o sin valor.");
        }
        return new(argumentos[0], opciones);
    }
}
