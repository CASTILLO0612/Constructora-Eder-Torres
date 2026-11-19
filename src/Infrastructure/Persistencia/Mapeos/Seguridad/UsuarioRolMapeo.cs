using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Seguridad;

public sealed class UsuarioRolMapeo : IEntityTypeConfiguration<UsuarioRol>
{
    public void Configure(EntityTypeBuilder<UsuarioRol> entidad)
    {
        entidad.HasKey(registro => registro.IdUsuarioRol).HasName("usuarios_rol_pkey");

        entidad.ToTable("usuarios_rol", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_usuario_rol_fechas", "((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio))");
        });

        entidad.HasIndex(registro => new { registro.IdUsuario, registro.IdRol, registro.FechaInicio }, "uq_usuario_rol_periodo").IsUnique();

        entidad.HasIndex(registro => new { registro.IdUsuario, registro.IdRol }, "ux_usuario_rol_activo")
            .IsUnique()
            .HasFilter("(fecha_fin IS NULL)");

        entidad.Property(registro => registro.IdUsuarioRol).HasColumnName("id_usuario_rol");
        entidad.Property(registro => registro.FechaFin).HasColumnName("fecha_fin");
        entidad.Property(registro => registro.FechaInicio).HasColumnName("fecha_inicio");
        entidad.Property(registro => registro.IdRol).HasColumnName("id_rol");
        entidad.Property(registro => registro.IdUsuario).HasColumnName("id_usuario");

        entidad.HasOne<Rol>().WithMany()
            .HasForeignKey(registro => registro.IdRol)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_usuario_rol_rol");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_usuario_rol_usuario");
    }
}
