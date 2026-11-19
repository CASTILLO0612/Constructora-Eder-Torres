using ConstructoraEderTorres.Dominio.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class AvanceMapeo : IEntityTypeConfiguration<Avance>
{
    public void Configure(EntityTypeBuilder<Avance> entidad)
    {
        entidad.HasKey(registro => registro.IdAvance).HasName("avances_pkey");

        entidad.ToTable("avances", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_avance_cantidad", "(cantidad_ejecutada >= (0)::numeric)");
            configuracionTabla.HasCheckConstraint("ck_avance_estado", "((estado)::text = ANY ((ARRAY['BORRADOR'::character varying, 'REGISTRADO'::character varying, 'ANULADO'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => new { registro.IdActividad, registro.FechaAvance }, "uq_avance_dia").IsUnique();

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_avance_uuid").IsUnique();

        entidad.Property(registro => registro.IdAvance).HasColumnName("id_avance");
        entidad.Property(registro => registro.CantidadEjecutada)
            .HasPrecision(12, 2)
            .HasColumnName("cantidad_ejecutada");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaAvance).HasColumnName("fecha_avance");
        entidad.Property(registro => registro.IdActividad).HasColumnName("id_actividad");
        entidad.Property(registro => registro.IdUsuarioRegistro).HasColumnName("id_usuario_registro");
        entidad.Property(registro => registro.Observaciones)
            .HasMaxLength(500)
            .HasColumnName("observaciones");
        entidad.Property(registro => registro.SincronizadoEn)
            .HasPrecision(0)
            .HasColumnName("sincronizado_en");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<Actividad>().WithMany()
            .HasForeignKey(registro => registro.IdActividad)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_avance_actividad");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioRegistro)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_avance_usuario");
    }
}
