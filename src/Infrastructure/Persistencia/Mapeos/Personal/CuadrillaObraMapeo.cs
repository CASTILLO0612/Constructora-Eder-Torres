using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class CuadrillaObraMapeo : IEntityTypeConfiguration<CuadrillaObra>
{
    public void Configure(EntityTypeBuilder<CuadrillaObra> entidad)
    {
        entidad.HasKey(registro => registro.IdCuadrillaObra).HasName("cuadrillas_obra_pkey");

        entidad.ToTable("cuadrillas_obra", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_co_estado", "((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'FINALIZADA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_co_fechas", "((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio))");
            configuracionTabla.HasTrigger("trg_impedir_cambio_obra_cuadrilla");
        });

        entidad.HasIndex(registro => new { registro.IdCuadrilla, registro.IdObra, registro.FechaInicio }, "uq_co_periodo").IsUnique();

        entidad.HasIndex(registro => new { registro.IdCuadrilla, registro.IdObra }, "ux_cuadrilla_obra_activa")
            .IsUnique()
            .HasFilter("((fecha_fin IS NULL) AND ((estado)::text = 'ACTIVA'::text))");

        entidad.Property(registro => registro.IdCuadrillaObra).HasColumnName("id_cuadrilla_obra");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(15)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaFin).HasColumnName("fecha_fin");
        entidad.Property(registro => registro.FechaInicio).HasColumnName("fecha_inicio");
        entidad.Property(registro => registro.IdCuadrilla).HasColumnName("id_cuadrilla");
        entidad.Property(registro => registro.IdObra).HasColumnName("id_obra");

        entidad.HasOne<Cuadrilla>().WithMany()
            .HasForeignKey(registro => registro.IdCuadrilla)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_co_cuadrilla");

        entidad.HasOne<Obra>().WithMany()
            .HasForeignKey(registro => registro.IdObra)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_co_obra");
    }
}
