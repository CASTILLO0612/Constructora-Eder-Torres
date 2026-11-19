using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PrecisionesGpsYCapturaLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "precision_gps_entrada_metros",
                schema: "public",
                table: "asistencias",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "precision_gps_salida_metros",
                schema: "public",
                table: "asistencias",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_asistencia_precision_gps",
                schema: "public",
                table: "asistencias",
                sql: "(((precision_gps_entrada_metros IS NULL) OR ((precision_gps_entrada_metros >= (0)::numeric) AND (precision_gps_entrada_metros <= 99999999.99) AND (latitud_entrada IS NOT NULL) AND (longitud_entrada IS NOT NULL))) AND ((precision_gps_salida_metros IS NULL) OR ((precision_gps_salida_metros >= (0)::numeric) AND (precision_gps_salida_metros <= 99999999.99) AND (latitud_salida IS NOT NULL) AND (longitud_salida IS NOT NULL) AND (salida_at IS NOT NULL))))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_asistencia_precision_gps",
                schema: "public",
                table: "asistencias");

            migrationBuilder.DropColumn(
                name: "precision_gps_entrada_metros",
                schema: "public",
                table: "asistencias");

            migrationBuilder.DropColumn(
                name: "precision_gps_salida_metros",
                schema: "public",
                table: "asistencias");
        }
    }
}
