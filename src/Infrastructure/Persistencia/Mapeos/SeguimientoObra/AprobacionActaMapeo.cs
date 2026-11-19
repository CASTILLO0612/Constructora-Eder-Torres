using ConstructoraEderTorres.Dominio.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class AprobacionActaMapeo : IEntityTypeConfiguration<AprobacionActa>
{
    public void Configure(EntityTypeBuilder<AprobacionActa> entidad)
    {
        entidad.HasKey(registro => registro.IdAprobacion).HasName("aprobaciones_acta_pkey");

        entidad.ToTable("aprobaciones_acta", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_aprobacion_acta_decision", "((decision)::text = ANY ((ARRAY['APROBADA'::character varying, 'RECHAZADA'::character varying])::text[]))");
        });

        entidad.Property(registro => registro.IdAprobacion).HasColumnName("id_aprobacion");
        entidad.Property(registro => registro.Comentario)
            .HasMaxLength(500)
            .HasColumnName("comentario");
        entidad.Property(registro => registro.Decision)
            .HasMaxLength(15)
            .HasColumnName("decision");
        entidad.Property(registro => registro.FechaHora)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("fecha_hora");
        entidad.Property(registro => registro.IdActa).HasColumnName("id_acta");
        entidad.Property(registro => registro.IdUsuario).HasColumnName("id_usuario");

        entidad.HasOne<Acta>().WithMany()
            .HasForeignKey(registro => registro.IdActa)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_aprobacion_acta_acta");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_aprobacion_acta_usuario");
    }
}
