using ConstructoraEderTorres.Dominio.EPP;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.EPP;

public sealed class MovimientoExistenciasEppMapeo : IEntityTypeConfiguration<MovimientoExistenciasEpp>
{
    public void Configure(EntityTypeBuilder<MovimientoExistenciasEpp> entidad)
    {
        entidad.HasKey(registro => registro.IdMovimientoEpp).HasName("movimientos_stock_epp_pkey");

        entidad.ToTable("movimientos_stock_epp", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_movimiento_epp_tipo", "((tipo_movimiento)::text = ANY ((ARRAY['ENTRADA'::character varying, 'ENTREGA'::character varying, 'DEVOLUCION'::character varying, 'PERDIDA'::character varying, 'RETIRO'::character varying, 'AJUSTE'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_movimiento_epp_uuid").IsUnique();

        entidad.Property(registro => registro.IdMovimientoEpp).HasColumnName("id_movimiento_epp");
        entidad.Property(registro => registro.FechaHora)
            .HasPrecision(0)
            .HasColumnName("fecha_hora");
        entidad.Property(registro => registro.IdEntregaEpp).HasColumnName("id_entrega_epp");
        entidad.Property(registro => registro.IdEpp).HasColumnName("id_epp");
        entidad.Property(registro => registro.IdUsuario).HasColumnName("id_usuario");
        entidad.Property(registro => registro.Observacion)
            .HasMaxLength(300)
            .HasColumnName("observacion");
        entidad.Property(registro => registro.TipoMovimiento)
            .HasMaxLength(25)
            .HasColumnName("tipo_movimiento");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<EntregaEpp>().WithMany()
            .HasForeignKey(registro => registro.IdEntregaEpp)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_movimiento_epp_entrega");

        entidad.HasOne<Epp>().WithMany()
            .HasForeignKey(registro => registro.IdEpp)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_movimiento_epp_epp");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_movimiento_epp_usuario");
    }
}
