using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class ActaIncidenciaMapeo : IEntityTypeConfiguration<ActaIncidencia>
{
    public void Configure(EntityTypeBuilder<ActaIncidencia> entidad)
    {
        entidad.HasKey(registro => new { registro.IdActa, registro.IdIncidencia }).HasName("pk_acta_incidencia");

        entidad.ToTable("actas_incidencia", "public", configuracionTabla =>
        {
            configuracionTabla.HasTrigger("trg_validar_acta_incidencia_obra");
        });

        entidad.Property(registro => registro.IdActa).HasColumnName("id_acta");
        entidad.Property(registro => registro.IdIncidencia).HasColumnName("id_incidencia");

        entidad.HasOne<Acta>().WithMany()
            .HasForeignKey(registro => registro.IdActa)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_acta_incidencia_acta");

        entidad.HasOne<Incidencia>().WithMany()
            .HasForeignKey(registro => registro.IdIncidencia)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_acta_incidencia_incidencia");
    }
}
