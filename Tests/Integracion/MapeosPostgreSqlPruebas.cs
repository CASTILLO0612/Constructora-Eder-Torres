using System.Text.Json;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class MapeosPostgreSqlPruebas
{
    private static readonly JsonElement Inventario = LeerInventario();

    public static IEnumerable<object[]> TablasDelEsquema => Inventario.GetProperty("tablas")
        .EnumerateArray().Select(tabla => new object[] { tabla.GetProperty("nombre").GetString()! });

    [Theory]
    [MemberData(nameof(TablasDelEsquema))]
    public void ElMapeoCoincideConLaTablaConciliada(string nombreTabla)
    {
        using var contexto = CrearContexto();
        var modelo = contexto.GetService<IDesignTimeModel>().Model;
        var esquema = Inventario.GetProperty("esquema").GetString()!;
        var esperado = Inventario.GetProperty("tablas").EnumerateArray()
            .Single(tabla => tabla.GetProperty("nombre").GetString() == nombreTabla);
        var tabla = modelo.GetRelationalModel().FindTable(nombreTabla, esquema);
        Assert.NotNull(tabla);

        var columnas = esperado.GetProperty("columnas").EnumerateArray().ToArray();
        Assert.Equal(columnas.Select(columna => columna.GetProperty("nombre").GetString()).Order(),
            tabla.Columns.Select(columna => columna.Name).Order());
        foreach (var columnaEsperada in columnas)
        {
            var columna = tabla.Columns.Single(columna => columna.Name == columnaEsperada.GetProperty("nombre").GetString());
            Assert.Equal(columnaEsperada.GetProperty("tipo").GetString(), columna.StoreType);
            Assert.Equal(!columnaEsperada.GetProperty("obligatoria").GetBoolean(), columna.IsNullable);
            var identidad = columnaEsperada.GetProperty("identidad").GetString();
            Assert.Equal(identidad == "d" ? NpgsqlValueGenerationStrategy.IdentityByDefaultColumn : NpgsqlValueGenerationStrategy.None,
                columna.PropertyMappings.Single().Property.GetValueGenerationStrategy());
            var valorPredeterminado = columnaEsperada.GetProperty("valorPredeterminado").GetString();
            if (columna.DefaultValueSql is not null)
            {
                Assert.Equal(valorPredeterminado, columna.DefaultValueSql);
            }
            else if (valorPredeterminado is not null)
            {
                Assert.Equal(valorPredeterminado.Trim('(', ')'), JsonSerializer.Serialize(columna.DefaultValue));
            }
        }

        var restricciones = esperado.GetProperty("restricciones").EnumerateArray().ToArray();
        var primaria = restricciones.Single(restriccion => restriccion.GetProperty("tipo").GetString() == "p");
        Assert.NotNull(tabla.PrimaryKey);
        Assert.Equal(primaria.GetProperty("nombre").GetString(), tabla.PrimaryKey.Name);
        Assert.Equal(Nombres(primaria, "columnas"), tabla.PrimaryKey.Columns.Select(columna => columna.Name));

        var foraneas = restricciones.Where(restriccion => restriccion.GetProperty("tipo").GetString() == "f").ToArray();
        Assert.Equal(foraneas.Length, tabla.ForeignKeyConstraints.Count());
        foreach (var foraneaEsperada in foraneas)
        {
            var foranea = tabla.ForeignKeyConstraints.Single(foranea => foranea.Name == foraneaEsperada.GetProperty("nombre").GetString());
            Assert.Equal(Nombres(foraneaEsperada, "columnas"), foranea.Columns.Select(columna => columna.Name));
            Assert.Equal(Nombres(foraneaEsperada, "columnasReferenciadas"), foranea.PrincipalColumns.Select(columna => columna.Name));
            Assert.Equal(foraneaEsperada.GetProperty("tablaReferenciada").GetString(), $"{foranea.PrincipalTable.Schema}.{foranea.PrincipalTable.Name}");
            Assert.Equal(ReferentialAction.NoAction, foranea.OnDeleteAction);
        }

        var indices = esperado.GetProperty("indices").EnumerateArray()
            .Where(indice => !indice.GetProperty("primario").GetBoolean()).ToArray();
        Assert.Equal(indices.Select(indice => indice.GetProperty("nombre").GetString()).Order(), tabla.Indexes.Select(indice => indice.Name).Order());
        foreach (var indiceEsperado in indices)
        {
            var indice = tabla.Indexes.Single(indice => indice.Name == indiceEsperado.GetProperty("nombre").GetString());
            Assert.Equal(indiceEsperado.GetProperty("unico").GetBoolean(), indice.IsUnique);
        }

        foreach (var unica in restricciones.Where(restriccion => restriccion.GetProperty("tipo").GetString() == "u"))
        {
            var indice = tabla.Indexes.Single(indice => indice.Name == unica.GetProperty("nombre").GetString());
            Assert.True(indice.IsUnique);
            Assert.Equal(Nombres(unica, "columnas"), indice.Columns.Select(columna => columna.Name));
        }

        var comprobaciones = restricciones.Where(restriccion => restriccion.GetProperty("tipo").GetString() == "c").ToArray();
        Assert.Equal(comprobaciones.Length, tabla.CheckConstraints.Count());
        foreach (var comprobacion in comprobaciones)
        {
            var definicion = comprobacion.GetProperty("definicion").GetString()!;
            Assert.Equal(definicion[7..^1], tabla.CheckConstraints.Single(restriccion => restriccion.Name == comprobacion.GetProperty("nombre").GetString()).Sql);
        }

        var entidad = modelo.GetEntityTypes().Single(entidad => entidad.GetTableName() == nombreTabla);
        var disparadores = esperado.GetProperty("disparadores").EnumerateArray();
        Assert.Equal(disparadores.Select(disparador => disparador.GetProperty("nombre").GetString()).Order(),
            entidad.GetDeclaredTriggers().Select(disparador => disparador.GetDatabaseName()).Order());
    }

    [Fact]
    public void LasTreintaYOchoEntidadesSonExplicitasYSinPropiedadesOcultas()
    {
        using var contexto = CrearContexto();
        Assert.Equal(Inventario.GetProperty("tablas").GetArrayLength(),
            contexto.Model.GetEntityTypes().Count(entidad => entidad.ClrType != typeof(SesionUsuario)));
        Assert.Equal(Inventario.GetProperty("tablas").GetArrayLength() + 1, contexto.Model.GetEntityTypes().Count());
        Assert.All(contexto.Model.GetEntityTypes(), entidad =>
        {
            Assert.Equal(typeof(Usuario).Assembly, entidad.ClrType.Assembly);
            Assert.DoesNotContain(entidad.GetProperties(), propiedad => propiedad.IsShadowProperty());
        });
        Assert.DoesNotContain(typeof(Usuario).Assembly.GetReferencedAssemblies(), referencia =>
            referencia.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || referencia.Name.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    [Fact]
    public void LaLineaBaseNoCreaNiEliminaObjetosDeNegocio()
    {
        using var contexto = CrearContexto();
        var ensamblado = contexto.GetService<IMigrationsAssembly>();
        var tipo = Assert.Single(ensamblado.Migrations, m => m.Key.EndsWith("_LineaBaseExistente", StringComparison.Ordinal)).Value;
        var migracion = ensamblado.CreateMigration(tipo, contexto.Database.ProviderName!);
        Assert.Empty(migracion.UpOperations);
        Assert.Empty(migracion.DownOperations);
    }

    [Fact]
    public void LaInstantaneaCorrespondeAlModeloActual()
    {
        using var contexto = CrearContexto();
        Assert.False(contexto.Database.HasPendingModelChanges());
    }

    private static ContextoConstructora CrearContexto() => new(
        new DbContextOptionsBuilder<ContextoConstructora>().UseNpgsql().Options);

    private static IEnumerable<string?> Nombres(JsonElement elemento, string propiedad) =>
        elemento.GetProperty(propiedad).EnumerateArray().Select(nombre => nombre.GetString());

    private static JsonElement LeerInventario()
    {
        using var recurso = typeof(MapeosPostgreSqlPruebas).Assembly.GetManifestResourceStream("EsquemaPostgreSql.json")!;
        using var documento = JsonDocument.Parse(recurso);
        return documento.RootElement.Clone();
    }
}
