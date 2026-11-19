using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Seguridad;

public sealed class RolMapeo : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> entidad)
    {
        entidad.HasKey(registro => registro.IdRol).HasName("roles_pkey");

        entidad.ToTable("roles", "public");

        entidad.HasIndex(registro => registro.Nombre, "roles_nombre_key").IsUnique();

        entidad.Property(registro => registro.IdRol).HasColumnName("id_rol");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(200)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(50)
            .HasColumnName("nombre");
    }
}
