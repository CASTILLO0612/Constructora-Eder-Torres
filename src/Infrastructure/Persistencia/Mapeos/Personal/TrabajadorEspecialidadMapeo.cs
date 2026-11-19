using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class TrabajadorEspecialidadMapeo : IEntityTypeConfiguration<TrabajadorEspecialidad>
{
    public void Configure(EntityTypeBuilder<TrabajadorEspecialidad> entidad)
    {
        entidad.HasKey(registro => new { registro.IdTrabajador, registro.IdEspecialidad }).HasName("pk_trabajador_especialidad");

        entidad.ToTable("trabajadores_especialidad", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_te_fechas", "((fecha_vencimiento IS NULL) OR (fecha_certificacion IS NULL) OR (fecha_vencimiento >= fecha_certificacion))");
            configuracionTabla.HasCheckConstraint("ck_te_nivel", "((nivel IS NULL) OR ((nivel)::text = ANY ((ARRAY['BASICO'::character varying, 'INTERMEDIO'::character varying, 'AVANZADO'::character varying, 'CERTIFICADO'::character varying])::text[])))");
        });

        entidad.Property(registro => registro.IdTrabajador).HasColumnName("id_trabajador");
        entidad.Property(registro => registro.IdEspecialidad).HasColumnName("id_especialidad");
        entidad.Property(registro => registro.FechaCertificacion).HasColumnName("fecha_certificacion");
        entidad.Property(registro => registro.FechaVencimiento).HasColumnName("fecha_vencimiento");
        entidad.Property(registro => registro.Nivel)
            .HasMaxLength(20)
            .HasColumnName("nivel");

        entidad.HasOne<Especialidad>().WithMany()
            .HasForeignKey(registro => registro.IdEspecialidad)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_te_especialidad");

        entidad.HasOne<Trabajador>().WithMany()
            .HasForeignKey(registro => registro.IdTrabajador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_te_trabajador");
    }
}
