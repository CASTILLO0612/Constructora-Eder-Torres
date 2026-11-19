using ConstructoraEderTorres.Dominio.Asistencia;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Asistencia;

public sealed class CorreccionAsistenciaMapeo : IEntityTypeConfiguration<CorreccionAsistencia>
{
    public void Configure(EntityTypeBuilder<CorreccionAsistencia> entidad)
    {
        entidad.HasKey(registro => registro.IdCorreccion).HasName("correcciones_asistencia_pkey");

        entidad.ToTable("correcciones_asistencia", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_ca_estado", "((estado)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'APROBADA'::character varying, 'RECHAZADA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_ca_horas", "((salida_nueva IS NULL) OR (entrada_nueva IS NULL) OR (salida_nueva >= entrada_nueva))");
        });

        entidad.Property(registro => registro.IdCorreccion).HasColumnName("id_correccion");
        entidad.Property(registro => registro.EntradaAnterior)
            .HasPrecision(0)
            .HasColumnName("entrada_anterior");
        entidad.Property(registro => registro.EntradaNueva)
            .HasPrecision(0)
            .HasColumnName("entrada_nueva");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(15)
            .HasColumnName("estado");
        entidad.Property(registro => registro.IdAsistencia).HasColumnName("id_asistencia");
        entidad.Property(registro => registro.IdUsuarioAprueba).HasColumnName("id_usuario_aprueba");
        entidad.Property(registro => registro.IdUsuarioSolicita).HasColumnName("id_usuario_solicita");
        entidad.Property(registro => registro.Motivo)
            .HasMaxLength(300)
            .HasColumnName("motivo");
        entidad.Property(registro => registro.ResueltoEn)
            .HasPrecision(0)
            .HasColumnName("resuelto_en");
        entidad.Property(registro => registro.SalidaAnterior)
            .HasPrecision(0)
            .HasColumnName("salida_anterior");
        entidad.Property(registro => registro.SalidaNueva)
            .HasPrecision(0)
            .HasColumnName("salida_nueva");
        entidad.Property(registro => registro.SolicitadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("solicitado_en");

        entidad.HasOne<RegistroAsistencia>().WithMany()
            .HasForeignKey(registro => registro.IdAsistencia)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ca_asistencia");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioAprueba)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ca_aprueba");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioSolicita)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ca_solicita");
    }
}
