using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Seguridad;

public sealed class RolPermisoMapeo : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> entidad)
    {
        entidad.HasKey(registro => new { registro.IdRol, registro.IdPermiso }).HasName("pk_rol_permiso");

        entidad.ToTable("roles_permiso", "public");

        entidad.Property(registro => registro.IdRol).HasColumnName("id_rol");
        entidad.Property(registro => registro.IdPermiso).HasColumnName("id_permiso");

        entidad.HasOne<Permiso>().WithMany()
            .HasForeignKey(registro => registro.IdPermiso)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_rol_permiso_permiso");

        entidad.HasOne<Rol>().WithMany()
            .HasForeignKey(registro => registro.IdRol)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_rol_permiso_rol");
    }
}
