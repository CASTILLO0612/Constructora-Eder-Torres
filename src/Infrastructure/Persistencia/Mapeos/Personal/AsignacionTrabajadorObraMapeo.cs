using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class AsignacionTrabajadorObraMapeo : IEntityTypeConfiguration<AsignacionTrabajadorObra>
{
    public void Configure(EntityTypeBuilder<AsignacionTrabajadorObra> entidad)
    {
        entidad.HasKey(registro => registro.IdAsignacion).HasName("asignaciones_trabajador_obra_pkey");

        entidad.ToTable("asignaciones_trabajador_obra", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_ato_estado", "((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'FINALIZADA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_ato_fechas", "((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio))");
            configuracionTabla.HasCheckConstraint("ck_ato_tipo", "((tipo_contratacion)::text = ANY ((ARRAY['FIJO'::character varying, 'SUBCONTRATADO'::character varying, 'TEMPORAL'::character varying])::text[]))");
            configuracionTabla.HasTrigger("trg_impedir_cambio_obra_asignacion");
        });

        entidad.HasIndex(registro => new { registro.IdTrabajador, registro.IdObra, registro.FechaInicio }, "uq_ato_periodo").IsUnique();

        entidad.HasIndex(registro => new { registro.IdTrabajador, registro.IdObra }, "ux_ato_activa")
            .IsUnique()
            .HasFilter("((fecha_fin IS NULL) AND ((estado)::text = 'ACTIVA'::text))");

        entidad.Property(registro => registro.IdAsignacion).HasColumnName("id_asignacion");
        entidad.Property(registro => registro.EsResponsable)
            .HasDefaultValue(false)
            .HasColumnName("es_responsable");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(15)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaFin).HasColumnName("fecha_fin");
        entidad.Property(registro => registro.FechaInicio).HasColumnName("fecha_inicio");
        entidad.Property(registro => registro.IdEspecialidad).HasColumnName("id_especialidad");
        entidad.Property(registro => registro.IdObra).HasColumnName("id_obra");
        entidad.Property(registro => registro.IdTrabajador).HasColumnName("id_trabajador");
        entidad.Property(registro => registro.TipoContratacion)
            .HasMaxLength(20)
            .HasColumnName("tipo_contratacion");

        entidad.HasOne<Obra>().WithMany()
            .HasForeignKey(registro => registro.IdObra)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ato_obra");

        entidad.HasOne<Trabajador>().WithMany()
            .HasForeignKey(registro => registro.IdTrabajador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ato_trabajador");

        entidad.HasOne<TrabajadorEspecialidad>().WithMany()
            .HasForeignKey(registro => new { registro.IdTrabajador, registro.IdEspecialidad })
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ato_especialidad");
    }
}
