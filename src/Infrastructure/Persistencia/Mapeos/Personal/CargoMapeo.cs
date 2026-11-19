using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class CargoMapeo : IEntityTypeConfiguration<Cargo>
{
    public void Configure(EntityTypeBuilder<Cargo> entidad)
    {
        entidad.HasKey(registro => registro.IdCargo).HasName("cargos_pkey");

        entidad.ToTable("cargos", "public");

        entidad.HasIndex(registro => registro.Nombre, "cargos_nombre_key").IsUnique();

        entidad.Property(registro => registro.IdCargo).HasColumnName("id_cargo");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(200)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(60)
            .HasColumnName("nombre");
    }
}
