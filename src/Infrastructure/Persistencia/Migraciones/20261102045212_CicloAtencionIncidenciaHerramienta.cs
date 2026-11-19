using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CicloAtencionIncidenciaHerramienta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ih_estado",
                schema: "public",
                table: "incidencias_herramienta");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ih_estado",
                schema: "public",
                table: "incidencias_herramienta",
                sql: "((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'EN_PROCESO'::character varying, 'RESUELTA'::character varying, 'ANULADA'::character varying])::text[]))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ih_estado",
                schema: "public",
                table: "incidencias_herramienta");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ih_estado",
                schema: "public",
                table: "incidencias_herramienta",
                sql: "((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'RESUELTA'::character varying, 'ANULADA'::character varying])::text[]))");
        }
    }
}
