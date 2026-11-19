using ConstructoraEderTorres.Dominio.EPP;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.EPP;

public sealed class TipoEppMapeo : IEntityTypeConfiguration<TipoEpp>
{
    public void Configure(EntityTypeBuilder<TipoEpp> entidad)
    {
        entidad.HasKey(registro => registro.IdTipoEpp).HasName("tipos_epp_pkey");

        entidad.ToTable("tipos_epp", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_tipo_epp_stock", "(stock_minimo >= 0)");
            configuracionTabla.HasCheckConstraint("ck_tipo_epp_vida", "((vida_util_dias IS NULL) OR (vida_util_dias > 0))");
        });

        entidad.HasIndex(registro => registro.Nombre, "tipos_epp_nombre_key").IsUnique();

        entidad.Property(registro => registro.IdTipoEpp).HasColumnName("id_tipo_epp");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(250)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(80)
            .HasColumnName("nombre");
        entidad.Property(registro => registro.ExistenciaMinima)
            .HasDefaultValue(0)
            .HasColumnName("stock_minimo");
        entidad.Property(registro => registro.VidaUtilDias).HasColumnName("vida_util_dias");
    }
}
