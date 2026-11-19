using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AsistenciaPropiaYGeocerca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "latitud",
                schema: "public",
                table: "obras",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitud",
                schema: "public",
                table: "obras",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "radio_asistencia_metros",
                schema: "public",
                table: "obras",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_obra_geocerca",
                schema: "public",
                table: "obras",
                sql: "(((latitud IS NULL) AND (longitud IS NULL) AND (radio_asistencia_metros IS NULL)) OR ((latitud IS NOT NULL) AND (longitud IS NOT NULL) AND (radio_asistencia_metros IS NOT NULL) AND (latitud >= '-90'::numeric) AND (latitud <= (90)::numeric) AND (longitud >= '-180'::numeric) AND (longitud <= (180)::numeric) AND (radio_asistencia_metros > (0)::numeric)))");

            migrationBuilder.CreateIndex(
                name: "ux_asistencia_abierta_asignacion",
                schema: "public",
                table: "asistencias",
                column: "id_asignacion",
                unique: true,
                filter: "salida_at IS NULL AND estado <> 'ANULADA'");

            // Solo incorpora el nuevo rol y sus permisos; conserva la matriz personalizada existente.
            migrationBuilder.Sql("""
                INSERT INTO public.permisos(codigo, nombre, descripcion, activo) VALUES
                  ('asistencia.propia.registrar', 'Registrar asistencia propia', 'Marcar exclusivamente la asistencia del trabajador vinculado a la sesion.', true),
                  ('asistencia.propia.consultar', 'Consultar asistencia propia', 'Consultar exclusivamente asistencias y asignaciones propias.', true),
                  ('sincronizacion.operar', 'Sincronizar operaciones offline', 'Enviar operaciones capturadas sin conexion.', true)
                ON CONFLICT (codigo) DO NOTHING;
                INSERT INTO public.roles(nombre, descripcion, activo)
                VALUES ('TRABAJADOR', 'Marcacion individual con GPS y consulta de asistencia propia.', true)
                ON CONFLICT (nombre) DO NOTHING;
                INSERT INTO public.roles_permiso(id_rol, id_permiso)
                SELECT r.id_rol, p.id_permiso FROM public.roles r CROSS JOIN public.permisos p
                WHERE r.nombre = 'TRABAJADOR'
                  AND p.codigo IN ('asistencia.propia.registrar', 'asistencia.propia.consultar', 'sincronizacion.operar')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_obra_geocerca",
                schema: "public",
                table: "obras");

            migrationBuilder.DropIndex(
                name: "ux_asistencia_abierta_asignacion",
                schema: "public",
                table: "asistencias");

            migrationBuilder.DropColumn(
                name: "latitud",
                schema: "public",
                table: "obras");

            migrationBuilder.DropColumn(
                name: "longitud",
                schema: "public",
                table: "obras");

            migrationBuilder.DropColumn(
                name: "radio_asistencia_metros",
                schema: "public",
                table: "obras");
        }
    }
}
