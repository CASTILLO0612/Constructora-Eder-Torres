using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class SesionesRevocables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sesiones_usuario",
                schema: "public",
                columns: table => new
                {
                    id_sesion = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    emitida_en_utc = table.Column<DateTime>(type: "timestamp(6) with time zone", precision: 6, nullable: false),
                    expira_en_utc = table.Column<DateTime>(type: "timestamp(6) with time zone", precision: 6, nullable: false),
                    revocada_en_utc = table.Column<DateTime>(type: "timestamp(6) with time zone", precision: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sesiones_usuario_pkey", x => x.id_sesion);
                    table.CheckConstraint("ck_sesion_usuario_fechas", "expira_en_utc > emitida_en_utc AND (revocada_en_utc IS NULL OR revocada_en_utc >= emitida_en_utc)");
                    table.ForeignKey(
                        name: "fk_sesion_usuario_usuario",
                        column: x => x.id_usuario,
                        principalSchema: "public",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario");
                });

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_usuario_usuario_expira",
                schema: "public",
                table: "sesiones_usuario",
                columns: new[] { "id_usuario", "expira_en_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sesiones_usuario",
                schema: "public");
        }
    }
}
