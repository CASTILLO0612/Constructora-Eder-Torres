using Microsoft.EntityFrameworkCore.Migrations;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Migraciones;

public partial class LineaBaseExistente : Migration
{
    protected override void Up(MigrationBuilder constructorMigracion)
    {
        // El esquema conciliado ya existe; esta línea base solo establece su punto de partida.
    }

    protected override void Down(MigrationBuilder constructorMigracion)
    {
        // Retirar la marca de línea base no debe eliminar las tablas existentes.
    }
}
