using ConstructoraEderTorres.Dominio.EPP;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.EPP;

public sealed class EppMapeo : IEntityTypeConfiguration<Epp>
{
    public void Configure(EntityTypeBuilder<Epp> entidad)
    {
        entidad.HasKey(registro => registro.IdEpp).HasName("epps_pkey");

        entidad.ToTable("epps", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_epp_condicion", "((condicion)::text = ANY ((ARRAY['NUEVO'::character varying, 'BUENO'::character varying, 'DESGASTADO'::character varying, 'DANADO'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_epp_estado", "((estado)::text = ANY ((ARRAY['DISPONIBLE'::character varying, 'ENTREGADO'::character varying, 'EN_REVISION'::character varying, 'PERDIDO'::character varying, 'RETIRADO'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.CodigoInterno, "epps_codigo_interno_key").IsUnique();

        entidad.HasIndex(registro => registro.NumeroSerie, "ux_epp_numero_serie")
            .IsUnique()
            .HasFilter("(numero_serie IS NOT NULL)");

        entidad.Property(registro => registro.IdEpp).HasColumnName("id_epp");
        entidad.Property(registro => registro.CodigoInterno)
            .HasMaxLength(30)
            .HasColumnName("codigo_interno");
        entidad.Property(registro => registro.Condicion)
            .HasMaxLength(20)
            .HasColumnName("condicion");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaAdquisicion).HasColumnName("fecha_adquisicion");
        entidad.Property(registro => registro.IdTipoEpp).HasColumnName("id_tipo_epp");
        entidad.Property(registro => registro.NumeroSerie)
            .HasMaxLength(60)
            .HasColumnName("numero_serie");

        entidad.HasOne<TipoEpp>().WithMany()
            .HasForeignKey(registro => registro.IdTipoEpp)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_epp_tipo");
    }
}
