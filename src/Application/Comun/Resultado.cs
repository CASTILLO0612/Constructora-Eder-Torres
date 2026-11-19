namespace ConstructoraEderTorres.Aplicacion.Comun;

public enum TipoError
{
    Validacion,
    NoAutenticado,
    Prohibido,
    NoEncontrado,
    Conflicto,
    NoDisponible
}

public sealed record ErrorNegocio(
    string Codigo,
    string Mensaje,
    TipoError Tipo)
{
    public static ErrorNegocio Validacion(string codigo, string mensaje) =>
        new(codigo, mensaje, TipoError.Validacion);

    public static ErrorNegocio NoAutenticado(string codigo, string mensaje) =>
        new(codigo, mensaje, TipoError.NoAutenticado);

    public static ErrorNegocio Prohibido(string codigo, string mensaje) =>
        new(codigo, mensaje, TipoError.Prohibido);

    public static ErrorNegocio NoEncontrado(string codigo, string mensaje) =>
        new(codigo, mensaje, TipoError.NoEncontrado);

    public static ErrorNegocio Conflicto(string codigo, string mensaje) =>
        new(codigo, mensaje, TipoError.Conflicto);
}

public sealed class Resultado
{
    private Resultado(bool esExitoso, ErrorNegocio? error)
    {
        EsExitoso = esExitoso;
        Error = error;
    }

    public bool EsExitoso { get; }

    public ErrorNegocio? Error { get; }

    public static Resultado Exito() => new(true, null);

    public static Resultado Fallo(ErrorNegocio error) =>
        new(false, error ?? throw new ArgumentNullException(nameof(error)));
}

public sealed class Resultado<T>
{
    private Resultado(bool esExitoso, T? valor, ErrorNegocio? error)
    {
        EsExitoso = esExitoso;
        Valor = valor;
        Error = error;
    }

    public bool EsExitoso { get; }

    public T? Valor { get; }

    public ErrorNegocio? Error { get; }

    public static Resultado<T> Exito(T valor) => new(true, valor, null);

    public static Resultado<T> Fallo(ErrorNegocio error) =>
        new(false, default, error ?? throw new ArgumentNullException(nameof(error)));
}
