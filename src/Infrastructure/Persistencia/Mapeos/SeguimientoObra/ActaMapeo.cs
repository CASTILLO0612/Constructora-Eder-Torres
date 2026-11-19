using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class ActaMapeo : IEntityTypeConfiguration<Acta>
{
    public void Configure(EntityTypeBuilder<Acta> entidad)
    {
        entidad.HasKey(registro => registro.IdActa).HasName("actas_pkey");

        entidad.ToTable("actas", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_acta_estado", "((estado)::text = ANY ((ARRAY['BORRADOR'::character varying, 'PENDIENTE'::character varying, 'APROBADA'::character varying, 'RECHAZADA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_acta_periodo", "(periodo_fin >= periodo_inicio)");
            configuracionTabla.HasTrigger("trg_impedir_cambio_obra_acta");
        });

        entidad.HasIndex(registro => new { registro.IdObra, registro.NumeroActa }, "uq_acta_numero").IsUnique();

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_acta_uuid").IsUnique();

        entidad.Property(registro => registro.IdActa).HasColumnName("id_acta");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.IdObra).HasColumnName("id_obra");
        entidad.Property(registro => registro.IdUsuarioCreador).HasColumnName("id_usuario_creador");
        entidad.Property(registro => registro.MejorasPropuestas)
            .HasMaxLength(500)
            .HasColumnName("mejoras_propuestas");
        entidad.Property(registro => registro.NumeroActa)
            .HasMaxLength(30)
            .HasColumnName("numero_acta");
        entidad.Property(registro => registro.Observaciones)
            .HasMaxLength(1000)
            .HasColumnName("observaciones");
        entidad.Property(registro => registro.PeriodoFin).HasColumnName("periodo_fin");
        entidad.Property(registro => registro.PeriodoInicio).HasColumnName("periodo_inicio");
        entidad.Property(registro => registro.SolicitudesCambio)
            .HasMaxLength(500)
            .HasColumnName("solicitudes_cambio");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<Obra>().WithMany()
            .HasForeignKey(registro => registro.IdObra)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_acta_obra");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioCreador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_acta_usuario");
    }
}
