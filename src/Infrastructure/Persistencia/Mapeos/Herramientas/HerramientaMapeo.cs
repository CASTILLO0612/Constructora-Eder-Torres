using ConstructoraEderTorres.Dominio.Herramientas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Herramientas;

public sealed class HerramientaMapeo : IEntityTypeConfiguration<Herramienta>
{
    public void Configure(EntityTypeBuilder<Herramienta> entidad)
    {
        entidad.HasKey(registro => registro.IdHerramienta).HasName("herramientas_pkey");

        entidad.ToTable("herramientas", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_herramienta_condicion", "((condicion)::text = ANY ((ARRAY['NUEVA'::character varying, 'BUENA'::character varying, 'DESGASTADA'::character varying, 'DANADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_herramienta_estado", "((estado)::text = ANY ((ARRAY['DISPONIBLE'::character varying, 'ASIGNADA'::character varying, 'EN_REVISION'::character varying, 'PERDIDA'::character varying, 'RETIRADA'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.CodigoInterno, "herramientas_codigo_interno_key").IsUnique();

        entidad.HasIndex(registro => registro.NumeroSerie, "ux_herramienta_serie")
            .IsUnique()
            .HasFilter("(numero_serie IS NOT NULL)");

        entidad.Property(registro => registro.IdHerramienta).HasColumnName("id_herramienta");
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
        entidad.Property(registro => registro.IdTipoHerramienta).HasColumnName("id_tipo_herramienta");
        entidad.Property(registro => registro.Marca)
            .HasMaxLength(60)
            .HasColumnName("marca");
        entidad.Property(registro => registro.Modelo)
            .HasMaxLength(60)
            .HasColumnName("modelo");
        entidad.Property(registro => registro.NumeroSerie)
            .HasMaxLength(60)
            .HasColumnName("numero_serie");

        entidad.HasOne<TipoHerramienta>().WithMany()
            .HasForeignKey(registro => registro.IdTipoHerramienta)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_herramienta_tipo");
    }
}
