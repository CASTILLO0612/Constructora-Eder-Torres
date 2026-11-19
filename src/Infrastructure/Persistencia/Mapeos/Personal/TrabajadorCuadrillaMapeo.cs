using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class TrabajadorCuadrillaMapeo : IEntityTypeConfiguration<TrabajadorCuadrilla>
{
    public void Configure(EntityTypeBuilder<TrabajadorCuadrilla> entidad)
    {
        entidad.HasKey(registro => registro.IdTrabajadorCuadrilla).HasName("trabajadores_cuadrilla_pkey");

        entidad.ToTable("trabajadores_cuadrilla", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_tc_fechas", "((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio))");
            configuracionTabla.HasTrigger("trg_validar_trabajador_cuadrilla_obra");
        });

        entidad.HasIndex(registro => new { registro.IdAsignacion, registro.IdCuadrillaObra, registro.FechaInicio }, "uq_tc_periodo").IsUnique();

        entidad.HasIndex(registro => registro.IdAsignacion, "ux_trabajador_cuadrilla_activa")
            .IsUnique()
            .HasFilter("(fecha_fin IS NULL)");

        entidad.Property(registro => registro.IdTrabajadorCuadrilla).HasColumnName("id_trabajador_cuadrilla");
        entidad.Property(registro => registro.FechaFin).HasColumnName("fecha_fin");
        entidad.Property(registro => registro.FechaInicio).HasColumnName("fecha_inicio");
        entidad.Property(registro => registro.IdAsignacion).HasColumnName("id_asignacion");
        entidad.Property(registro => registro.IdCuadrillaObra).HasColumnName("id_cuadrilla_obra");

        entidad.HasOne<AsignacionTrabajadorObra>().WithOne()
            .HasForeignKey<TrabajadorCuadrilla>(registro => registro.IdAsignacion)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_tc_asignacion");

        entidad.HasOne<CuadrillaObra>().WithMany()
            .HasForeignKey(registro => registro.IdCuadrillaObra)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_tc_cuadrilla_obra");
    }
}
