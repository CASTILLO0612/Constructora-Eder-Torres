using ConstructoraEderTorres.Dominio.Herramientas;
using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Herramientas;

public sealed class AsignacionHerramientaMapeo : IEntityTypeConfiguration<AsignacionHerramienta>
{
    public void Configure(EntityTypeBuilder<AsignacionHerramienta> entidad)
    {
        entidad.HasKey(registro => registro.IdAsignacionHerramienta).HasName("asignaciones_herramienta_pkey");

        entidad.ToTable("asignaciones_herramienta", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_ah_estado", "((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'DEVUELTA'::character varying, 'PERDIDA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_ah_fecha", "((devuelta_en IS NULL) OR (devuelta_en >= entregada_en))");
        });

        entidad.HasIndex(registro => registro.IdHerramienta, "ux_asignacion_herramienta_activa")
            .IsUnique()
            .HasFilter("((estado)::text = 'ACTIVA'::text)");

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_asignacion_herramienta_uuid").IsUnique();

        entidad.Property(registro => registro.IdAsignacionHerramienta).HasColumnName("id_asignacion_herramienta");
        entidad.Property(registro => registro.CondicionDevolucion)
            .HasMaxLength(20)
            .HasColumnName("condicion_devolucion");
        entidad.Property(registro => registro.CondicionEntrega)
            .HasMaxLength(20)
            .HasColumnName("condicion_entrega");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.DevueltaEn)
            .HasPrecision(0)
            .HasColumnName("devuelta_en");
        entidad.Property(registro => registro.EntregadaEn)
            .HasPrecision(0)
            .HasColumnName("entregada_en");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.IdAsignacionTrabajadorObra).HasColumnName("id_asignacion_trabajador_obra");
        entidad.Property(registro => registro.IdHerramienta).HasColumnName("id_herramienta");
        entidad.Property(registro => registro.IdUsuarioEntrega).HasColumnName("id_usuario_entrega");
        entidad.Property(registro => registro.SincronizadoEn)
            .HasPrecision(0)
            .HasColumnName("sincronizado_en");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<AsignacionTrabajadorObra>().WithMany()
            .HasForeignKey(registro => registro.IdAsignacionTrabajadorObra)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ah_asignacion");

        entidad.HasOne<Herramienta>().WithOne()
            .HasForeignKey<AsignacionHerramienta>(registro => registro.IdHerramienta)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ah_herramienta");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioEntrega)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ah_usuario");
    }
}
