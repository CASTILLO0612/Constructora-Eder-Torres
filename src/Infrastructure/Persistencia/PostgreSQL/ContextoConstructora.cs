using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

public sealed class ContextoConstructora(
    DbContextOptions<ContextoConstructora> opciones) : DbContext(opciones)
{
    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        base.OnModelCreating(constructorModelo);
        constructorModelo.ApplyConfigurationsFromAssembly(typeof(ContextoConstructora).Assembly);
    }
}
