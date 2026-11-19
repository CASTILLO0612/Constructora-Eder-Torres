using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Seguridad;

public sealed class UsuarioMapeo : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> entidad)
    {
        entidad.HasKey(registro => registro.IdUsuario).HasName("usuarios_pkey");

        entidad.ToTable("usuarios", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_usuario_estado", "((estado)::text = ANY ((ARRAY['ACTIVO'::character varying, 'BLOQUEADO'::character varying, 'INACTIVO'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.NombreUsuario, "usuarios_nombre_usuario_key").IsUnique();

        entidad.HasIndex(registro => registro.IdTrabajador, "ux_usuario_trabajador")
            .IsUnique()
            .HasFilter("(id_trabajador IS NOT NULL)");

        entidad.Property(registro => registro.IdUsuario).HasColumnName("id_usuario");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(15)
            .HasColumnName("estado");
        entidad.Property(registro => registro.IdTrabajador).HasColumnName("id_trabajador");
        entidad.Property(registro => registro.NombreUsuario)
            .HasMaxLength(60)
            .HasColumnName("nombre_usuario");
        entidad.Property(registro => registro.ContrasenaHash)
            .HasMaxLength(255)
            .HasColumnName("password_hash");
        entidad.Property(registro => registro.UltimoAcceso)
            .HasPrecision(0)
            .HasColumnName("ultimo_acceso");

        entidad.HasOne<Trabajador>().WithOne()
            .HasForeignKey<Usuario>(registro => registro.IdTrabajador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_usuario_trabajador");
    }
}
