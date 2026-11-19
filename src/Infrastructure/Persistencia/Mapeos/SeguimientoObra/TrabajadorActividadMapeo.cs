using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class TrabajadorActividadMapeo : IEntityTypeConfiguration<TrabajadorActividad>
{
    public void Configure(EntityTypeBuilder<TrabajadorActividad> entidad)
    {
        entidad.HasKey(registro => new { registro.IdActividad, registro.IdAsignacion }).HasName("pk_trabajador_actividad");

        entidad.ToTable("trabajadores_actividad", "public", configuracionTabla =>
        {
            configuracionTabla.HasTrigger("trg_validar_trabajador_actividad_obra");
        });

        entidad.Property(registro => registro.IdActividad).HasColumnName("id_actividad");
        entidad.Property(registro => registro.IdAsignacion).HasColumnName("id_asignacion");
        entidad.Property(registro => registro.RolParticipacion)
            .HasMaxLength(50)
            .HasColumnName("rol_participacion");

        entidad.HasOne<Actividad>().WithMany()
            .HasForeignKey(registro => registro.IdActividad)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ta_actividad");

        entidad.HasOne<AsignacionTrabajadorObra>().WithMany()
            .HasForeignKey(registro => registro.IdAsignacion)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ta_asignacion");
    }
}
