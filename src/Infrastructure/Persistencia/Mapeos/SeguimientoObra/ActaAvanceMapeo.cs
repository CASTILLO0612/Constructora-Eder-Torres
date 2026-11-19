using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class ActaAvanceMapeo : IEntityTypeConfiguration<ActaAvance>
{
    public void Configure(EntityTypeBuilder<ActaAvance> entidad)
    {
        entidad.HasKey(registro => new { registro.IdActa, registro.IdAvance }).HasName("pk_acta_avance");

        entidad.ToTable("actas_avance", "public", configuracionTabla =>
        {
            configuracionTabla.HasTrigger("trg_validar_acta_avance_obra");
        });

        entidad.Property(registro => registro.IdActa).HasColumnName("id_acta");
        entidad.Property(registro => registro.IdAvance).HasColumnName("id_avance");

        entidad.HasOne<Acta>().WithMany()
            .HasForeignKey(registro => registro.IdActa)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_acta_avance_acta");

        entidad.HasOne<Avance>().WithMany()
            .HasForeignKey(registro => registro.IdAvance)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_acta_avance_avance");
    }
}
