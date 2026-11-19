using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class BusquedasPaginadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,");
            migrationBuilder.Sql("""
                CREATE FUNCTION public.cet_texto_busqueda(valores text[]) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $$
                  SELECT translate(lower(normalize(array_to_string(valores, ' '), NFC)), 'áéíóúüñÁÉÍÓÚÜÑ', 'aeiouunaeiouun')
                $$;
                """);
            foreach (var (tabla, columnas) in CamposIndexados)
            {
                var textos = string.Join(", ", columnas.Split(',').Select(columna => columna.Trim() + "::text"));
                migrationBuilder.Sql($"CREATE INDEX ix_busqueda_{tabla} ON public.{tabla} USING gin (public.cet_texto_busqueda(ARRAY[{textos}]) gin_trgm_ops);");
            }
            migrationBuilder.Sql("CREATE INDEX ix_auditoria_fecha_orden ON public.auditorias (fecha_hora DESC, id_auditoria DESC);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (tabla, _) in CamposIndexados)
                migrationBuilder.Sql($"DROP INDEX public.ix_busqueda_{tabla};");
            migrationBuilder.Sql("DROP INDEX public.ix_auditoria_fecha_orden; DROP FUNCTION public.cet_texto_busqueda(text[]);");
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,");
        }

        // Índices sobre catálogos y datos de identificación que cambian con poca frecuencia.
        // Las consultas operativas conservan primero su alcance de obra y período.
        private static readonly (string Tabla, string Columnas)[] CamposIndexados =
        [
            ("obras", "codigo, nombre, descripcion, direccion, estado"),
            ("trabajadores", "primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, cedula, telefono, correo"),
            ("cargos", "nombre, descripcion"),
            ("especialidades", "nombre, descripcion"),
            ("cuadrillas", "codigo, nombre, descripcion"),
            ("usuarios", "nombre_usuario, estado"),
            ("roles", "nombre, descripcion"),
            ("permisos", "codigo, nombre, descripcion"),
            ("actividades", "codigo, nombre, descripcion, unidad_medida, estado"),
            ("incidencias", "tipo_incidencia, descripcion, justificacion, estado"),
            ("actas", "numero_acta, estado"),
            ("tipos_epp", "nombre, descripcion"),
            ("categorias_herramienta", "nombre, descripcion")
        ];
    }
}
