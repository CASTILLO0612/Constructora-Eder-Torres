using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Seguridad;

public sealed class SesionUsuarioMapeo : IEntityTypeConfiguration<SesionUsuario>
{
    public void Configure(EntityTypeBuilder<SesionUsuario> entidad)
    {
        entidad.ToTable("sesiones_usuario", "public", tabla =>
        {
            tabla.HasCheckConstraint("ck_sesion_usuario_fechas", "expira_en_utc > emitida_en_utc AND (revocada_en_utc IS NULL OR revocada_en_utc >= emitida_en_utc)");
        });
        entidad.HasKey(s => s.IdSesion).HasName("sesiones_usuario_pkey");
        entidad.Property(s => s.IdSesion).HasColumnName("id_sesion").ValueGeneratedNever();
        entidad.Property(s => s.IdUsuario).HasColumnName("id_usuario");
        entidad.Property(s => s.EmitidaEnUtc).HasColumnName("emitida_en_utc").HasPrecision(6);
        entidad.Property(s => s.ExpiraEnUtc).HasColumnName("expira_en_utc").HasPrecision(6);
        entidad.Property(s => s.RevocadaEnUtc).HasColumnName("revocada_en_utc").HasPrecision(6);
        entidad.HasIndex(s => new { s.IdUsuario, s.ExpiraEnUtc }, "ix_sesiones_usuario_usuario_expira");
        entidad.HasOne<Usuario>().WithMany().HasForeignKey(s => s.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_sesion_usuario_usuario");
    }
}
