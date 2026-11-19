using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.SeguimientoObra;

public sealed class TrabajadorActaMapeo : IEntityTypeConfiguration<TrabajadorActa>
{
    public void Configure(EntityTypeBuilder<TrabajadorActa> entidad)
    {
        entidad.HasKey(registro => new { registro.IdActa, registro.IdAsignacion }).HasName("pk_trabajador_acta");

        entidad.ToTable("trabajadores_acta", "public", configuracionTabla =>
        {
            configuracionTabla.HasTrigger("trg_validar_trabajador_acta_obra");
        });

        entidad.Property(registro => registro.IdActa).HasColumnName("id_acta");
        entidad.Property(registro => registro.IdAsignacion).HasColumnName("id_asignacion");
        entidad.Property(registro => registro.FirmadoEn)
            .HasPrecision(0)
            .HasColumnName("firmado_en");
        entidad.Property(registro => registro.RutaFirma)
            .HasMaxLength(500)
            .HasColumnName("ruta_firma");
        entidad.Property(registro => registro.TipoParticipacion)
            .HasMaxLength(30)
            .HasColumnName("tipo_participacion");

        entidad.HasOne<Acta>().WithMany()
            .HasForeignKey(registro => registro.IdActa)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_trabajador_acta_acta");

        entidad.HasOne<AsignacionTrabajadorObra>().WithMany()
            .HasForeignKey(registro => registro.IdAsignacion)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_trabajador_acta_asignacion");
    }
}
