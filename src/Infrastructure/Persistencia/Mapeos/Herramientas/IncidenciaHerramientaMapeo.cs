using ConstructoraEderTorres.Dominio.Herramientas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Herramientas;

public sealed class IncidenciaHerramientaMapeo : IEntityTypeConfiguration<IncidenciaHerramienta>
{
    public void Configure(EntityTypeBuilder<IncidenciaHerramienta> entidad)
    {
        entidad.HasKey(registro => registro.IdIncidenciaHerramienta).HasName("incidencias_herramienta_pkey");

        entidad.ToTable("incidencias_herramienta", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_ih_costo", "((costo_estimado IS NULL) OR (costo_estimado >= (0)::numeric))");
            configuracionTabla.HasCheckConstraint("ck_ih_estado", "((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'EN_PROCESO'::character varying, 'RESUELTA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_ih_tipo", "((tipo_incidencia)::text = ANY ((ARRAY['DANO'::character varying, 'PERDIDA'::character varying, 'DEMORA'::character varying, 'DEVOLUCION'::character varying, 'OTRA'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_incidencia_herramienta_uuid").IsUnique();

        entidad.Property(registro => registro.IdIncidenciaHerramienta).HasColumnName("id_incidencia_herramienta");
        entidad.Property(registro => registro.CostoEstimado)
            .HasPrecision(12, 2)
            .HasColumnName("costo_estimado");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(500)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasDefaultValueSql("'ABIERTA'::character varying")
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaHora)
            .HasPrecision(0)
            .HasColumnName("fecha_hora");
        entidad.Property(registro => registro.IdAsignacionHerramienta).HasColumnName("id_asignacion_herramienta");
        entidad.Property(registro => registro.IdUsuarioRegistro).HasColumnName("id_usuario_registro");
        entidad.Property(registro => registro.Resolucion)
            .HasMaxLength(500)
            .HasColumnName("resolucion");
        entidad.Property(registro => registro.TipoIncidencia)
            .HasMaxLength(25)
            .HasColumnName("tipo_incidencia");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<AsignacionHerramienta>().WithMany()
            .HasForeignKey(registro => registro.IdAsignacionHerramienta)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ih_asignacion");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioRegistro)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ih_usuario");
    }
}
