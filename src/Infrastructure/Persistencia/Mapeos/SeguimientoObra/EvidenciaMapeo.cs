using ConstructoraEderTorres.Dominio.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class EvidenciaMapeo : IEntityTypeConfiguration<Evidencia>
{
    public void Configure(EntityTypeBuilder<Evidencia> entidad)
    {
        entidad.HasKey(registro => registro.IdEvidencia).HasName("evidencias_pkey");

        entidad.ToTable("evidencias", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_evidencia_lat", "((latitud IS NULL) OR ((latitud >= ('-90'::integer)::numeric) AND (latitud <= (90)::numeric)))");
            configuracionTabla.HasCheckConstraint("ck_evidencia_lon", "((longitud IS NULL) OR ((longitud >= ('-180'::integer)::numeric) AND (longitud <= (180)::numeric)))");
            configuracionTabla.HasCheckConstraint("ck_evidencia_tipo", "((tipo_evidencia)::text = ANY ((ARRAY['FOTOGRAFIA'::character varying, 'BITACORA'::character varying, 'DOCUMENTO'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_evidencia_uuid").IsUnique();

        entidad.Property(registro => registro.IdEvidencia).HasColumnName("id_evidencia");
        entidad.Property(registro => registro.CapturadaEn)
            .HasPrecision(0)
            .HasColumnName("capturada_en");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.HuellaArchivo)
            .HasMaxLength(64)
            .IsFixedLength()
            .HasColumnName("hash_archivo");
        entidad.Property(registro => registro.IdAvance).HasColumnName("id_avance");
        entidad.Property(registro => registro.IdUsuarioRegistro).HasColumnName("id_usuario_registro");
        entidad.Property(registro => registro.Latitud)
            .HasPrecision(9, 6)
            .HasColumnName("latitud");
        entidad.Property(registro => registro.Longitud)
            .HasPrecision(9, 6)
            .HasColumnName("longitud");
        entidad.Property(registro => registro.RutaArchivo)
            .HasMaxLength(500)
            .HasColumnName("ruta_archivo");
        entidad.Property(registro => registro.SincronizadoEn)
            .HasPrecision(0)
            .HasColumnName("sincronizado_en");
        entidad.Property(registro => registro.TipoEvidencia)
            .HasMaxLength(20)
            .HasColumnName("tipo_evidencia");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<Avance>().WithMany()
            .HasForeignKey(registro => registro.IdAvance)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_evidencia_avance");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioRegistro)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_evidencia_usuario");
    }
}
