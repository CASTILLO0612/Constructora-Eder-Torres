using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class ActividadMapeo : IEntityTypeConfiguration<Actividad>
{
    public void Configure(EntityTypeBuilder<Actividad> entidad)
    {
        entidad.HasKey(registro => registro.IdActividad).HasName("actividades_pkey");

        entidad.ToTable("actividades", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_actividad_cantidad", "(cantidad_planificada >= (0)::numeric)");
            configuracionTabla.HasCheckConstraint("ck_actividad_estado", "((estado)::text = ANY ((ARRAY['PLANIFICADA'::character varying, 'EN_PROCESO'::character varying, 'COMPLETA'::character varying, 'RETRASADA'::character varying, 'CANCELADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_actividad_fechas", "(fecha_fin_plan >= fecha_inicio_plan)");
            configuracionTabla.HasTrigger("trg_impedir_cambio_obra_actividad");
            configuracionTabla.HasTrigger("trg_validar_responsable_actividad_obra");
        });

        entidad.HasIndex(registro => new { registro.IdObra, registro.Codigo }, "uq_actividad_codigo").IsUnique();

        entidad.Property(registro => registro.IdActividad).HasColumnName("id_actividad");
        entidad.Property(registro => registro.CantidadPlanificada)
            .HasPrecision(12, 2)
            .HasColumnName("cantidad_planificada");
        entidad.Property(registro => registro.Codigo)
            .HasMaxLength(30)
            .HasColumnName("codigo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(500)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaFinPlan).HasColumnName("fecha_fin_plan");
        entidad.Property(registro => registro.FechaInicioPlan).HasColumnName("fecha_inicio_plan");
        entidad.Property(registro => registro.IdAsignacionResponsable).HasColumnName("id_asignacion_responsable");
        entidad.Property(registro => registro.IdObra).HasColumnName("id_obra");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(120)
            .HasColumnName("nombre");
        entidad.Property(registro => registro.UnidadMedida)
            .HasMaxLength(20)
            .HasColumnName("unidad_medida");

        entidad.HasOne<AsignacionTrabajadorObra>().WithMany()
            .HasForeignKey(registro => registro.IdAsignacionResponsable)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_actividad_responsable");

        entidad.HasOne<Obra>().WithMany()
            .HasForeignKey(registro => registro.IdObra)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_actividad_obra");
    }
}
