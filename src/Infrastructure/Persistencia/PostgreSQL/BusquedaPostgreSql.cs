using System.Linq.Expressions;
using ConstructoraEderTorres.Aplicacion.Comun;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

internal static class BusquedaPostgreSql
{
    [DbFunction("cet_texto_busqueda", "public")]
    public static string Normalizar(string?[] valores) => throw new NotSupportedException("Función SQL de búsqueda.");
    public static string? ComoTexto(string? valor) => throw new NotSupportedException("Conversión SQL a text.");

    // Cada lista declara sus campos públicos; nunca se inspeccionan contraseñas, sesiones ni identificadores internos.
    public static IQueryable<T> Buscar<T>(this IQueryable<T> consulta, PaginacionConsulta paginacion,
        params Expression<Func<T, string?>>[] campos)
    {
        var palabras = BusquedaTexto.Palabras(paginacion.Buscar);
        if (palabras.Length == 0) return consulta;
        var parametro = Expression.Parameter(typeof(T), "fila");
        var conversion = typeof(BusquedaPostgreSql).GetMethod(nameof(ComoTexto))!;
        var valores = Expression.NewArrayInit(typeof(string), campos.Select(campo =>
            Expression.Call(conversion, new Sustituir(campo.Parameters[0], parametro).Visit(campo.Body)!)));
        Expression? criterio = null;
        foreach (var palabra in palabras)
        {
            var patron = "%" + palabra.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            // Capturar el patrón permite parametrizarlo; los comodines escritos por el usuario son literales.
            Expression<Func<string?[], bool>> comparar = texto => EF.Functions.Like(Normalizar(texto), patron, "\\");
            var prueba = new Sustituir(comparar.Parameters[0], valores).Visit(comparar.Body)!;
            criterio = criterio is null ? prueba : Expression.AndAlso(criterio, prueba);
        }
        if (criterio is null) return consulta;

        // EF no puede filtrar propiedades de ciertos records construidos en Select. Componer sobre su origen
        // conserva la consulta en SQL, incluidos joins y agrupaciones, antes de aplicar Skip/Take.
        if (consulta.Expression is MethodCallExpression seleccion && seleccion.Method.Name == nameof(Queryable.Select)
            && seleccion.Method.DeclaringType == typeof(Queryable))
        {
            var proyeccion = (LambdaExpression)((UnaryExpression)seleccion.Arguments[1]).Operand;
            var cuerpo = new ReducirConstrucciones().Visit(new Sustituir(parametro, proyeccion.Body).Visit(criterio))!;
            var filtro = Expression.Lambda(cuerpo, proyeccion.Parameters);
            var donde = Expression.Call(typeof(Queryable), nameof(Queryable.Where),
                [proyeccion.Parameters[0].Type], seleccion.Arguments[0], Expression.Quote(filtro));
            return consulta.Provider.CreateQuery<T>(seleccion.Update(null, [donde, seleccion.Arguments[1]]));
        }
        if (consulta.Expression is MethodCallExpression union
            && union.Method.DeclaringType == typeof(Queryable)
            && union.Method.Name is nameof(Queryable.Join) or nameof(Queryable.SelectMany))
        {
            var resultado = (LambdaExpression)((UnaryExpression)union.Arguments[^1]).Operand;
            var tipoPar = typeof(FilaCombinada<,>).MakeGenericType(resultado.Parameters.Select(p => p.Type).ToArray());
            var izquierda = tipoPar.GetProperty(nameof(FilaCombinada<int, int>.Izquierda))!;
            var derecha = tipoPar.GetProperty(nameof(FilaCombinada<int, int>.Derecha))!;
            var par = Expression.MemberInit(Expression.New(tipoPar),
                Expression.Bind(izquierda, resultado.Parameters[0]), Expression.Bind(derecha, resultado.Parameters[1]));
            var argumentos = union.Arguments.ToArray();
            argumentos[^1] = Expression.Quote(Expression.Lambda(par, resultado.Parameters));
            var tipos = union.Method.GetGenericArguments();
            tipos[^1] = tipoPar;
            var origen = Expression.Call(union.Method.GetGenericMethodDefinition().MakeGenericMethod(tipos), argumentos);
            var fila = Expression.Parameter(tipoPar, "combinada");
            var proyeccion = new Sustituir(resultado.Parameters[1], Expression.Property(fila, derecha)).Visit(
                new Sustituir(resultado.Parameters[0], Expression.Property(fila, izquierda)).Visit(resultado.Body))!;
            var filtro = new ReducirConstrucciones().Visit(new Sustituir(parametro, proyeccion).Visit(criterio))!;
            var donde = Expression.Call(typeof(Queryable), nameof(Queryable.Where), [tipoPar], origen,
                Expression.Quote(Expression.Lambda(filtro, fila)));
            return consulta.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), nameof(Queryable.Select),
                [tipoPar, typeof(T)], donde, Expression.Quote(Expression.Lambda(proyeccion, fila))));
        }
        return consulta.Where(Expression.Lambda<Func<T, bool>>(criterio, parametro));
    }

    private sealed class FilaCombinada<TIzquierda, TDerecha>
    {
        public TIzquierda Izquierda { get; set; } = default!;
        public TDerecha Derecha { get; set; } = default!;
    }

    private sealed class Sustituir(Expression origen, Expression destino) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? nodo) => nodo == origen ? destino : base.Visit(nodo);
    }

    private sealed class ReducirConstrucciones : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression nodo)
        {
            var origen = Visit(nodo.Expression);
            if (origen is NewExpression construccion)
            {
                var nombres = construccion.Members?.Select(m => m.Name).ToArray()
                    ?? construccion.Constructor!.GetParameters().Select(p => p.Name!).ToArray();
                var indice = Array.FindIndex(nombres, n => string.Equals(n, nodo.Member.Name, StringComparison.OrdinalIgnoreCase));
                if (indice >= 0) return Visit(construccion.Arguments[indice]);
            }
            return nodo.Update(origen);
        }
    }
}
