using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Seguridad;

public sealed class PermisoMapeo : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> entidad)
    {
        entidad.HasKey(registro => registro.IdPermiso).HasName("permisos_pkey");

        entidad.ToTable("permisos", "public");

        entidad.HasIndex(registro => registro.Codigo, "permisos_codigo_key").IsUnique();

        entidad.Property(registro => registro.IdPermiso).HasColumnName("id_permiso");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Codigo)
            .HasMaxLength(60)
            .HasColumnName("codigo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(200)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(80)
            .HasColumnName("nombre");
    }
}
