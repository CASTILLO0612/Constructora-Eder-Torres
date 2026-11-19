using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class ActividadIncidenciaMapeo : IEntityTypeConfiguration<ActividadIncidencia>
{
    public void Configure(EntityTypeBuilder<ActividadIncidencia> entidad)
    {
        entidad.HasKey(registro => new { registro.IdIncidencia, registro.IdActividad }).HasName("pk_actividad_incidencia");

        entidad.ToTable("actividades_incidencia", "public", configuracionTabla =>
        {
            configuracionTabla.HasTrigger("trg_validar_actividad_incidencia_obra");
        });

        entidad.Property(registro => registro.IdIncidencia).HasColumnName("id_incidencia");
        entidad.Property(registro => registro.IdActividad).HasColumnName("id_actividad");

        entidad.HasOne<Actividad>().WithMany()
            .HasForeignKey(registro => registro.IdActividad)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ai_actividad");

        entidad.HasOne<Incidencia>().WithMany()
            .HasForeignKey(registro => registro.IdIncidencia)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_ai_incidencia");
    }
}
