using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class IncidenciaMapeo : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> entidad)
    {
        entidad.HasKey(registro => registro.IdIncidencia).HasName("incidencias_pkey");

        entidad.ToTable("incidencias", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_incidencia_estado", "((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'RESUELTA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_incidencia_fechas", "((fin_en IS NULL) OR (fin_en >= inicio_en))");
            configuracionTabla.HasCheckConstraint("ck_incidencia_horas", "((horas_perdidas IS NULL) OR (horas_perdidas >= (0)::numeric))");
            configuracionTabla.HasCheckConstraint("ck_incidencia_tipo", "((tipo_incidencia)::text = ANY ((ARRAY['CLIMA'::character varying, 'MATERIAL_IMPORTADO'::character varying, 'TECNICA'::character varying, 'SEGURIDAD'::character varying, 'OTRA'::character varying])::text[]))");
            configuracionTabla.HasTrigger("trg_impedir_cambio_obra_incidencia");
        });

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_incidencia_uuid").IsUnique();

        entidad.Property(registro => registro.IdIncidencia).HasColumnName("id_incidencia");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(500)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FinEn)
            .HasPrecision(0)
            .HasColumnName("fin_en");
        entidad.Property(registro => registro.HorasPerdidas)
            .HasPrecision(8, 2)
            .HasColumnName("horas_perdidas");
        entidad.Property(registro => registro.IdObra).HasColumnName("id_obra");
        entidad.Property(registro => registro.IdUsuarioRegistro).HasColumnName("id_usuario_registro");
        entidad.Property(registro => registro.InicioEn)
            .HasPrecision(0)
            .HasColumnName("inicio_en");
        entidad.Property(registro => registro.Justificacion)
            .HasMaxLength(500)
            .HasColumnName("justificacion");
        entidad.Property(registro => registro.SincronizadoEn)
            .HasPrecision(0)
            .HasColumnName("sincronizado_en");
        entidad.Property(registro => registro.TipoIncidencia)
            .HasMaxLength(30)
            .HasColumnName("tipo_incidencia");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<Obra>().WithMany()
            .HasForeignKey(registro => registro.IdObra)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_incidencia_obra");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioRegistro)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_incidencia_usuario");
    }
}
