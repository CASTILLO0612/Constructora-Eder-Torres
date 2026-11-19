using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class CuadrillaMapeo : IEntityTypeConfiguration<Cuadrilla>
{
    public void Configure(EntityTypeBuilder<Cuadrilla> entidad)
    {
        entidad.HasKey(registro => registro.IdCuadrilla).HasName("cuadrillas_pkey");

        entidad.ToTable("cuadrillas", "public");

        entidad.HasIndex(registro => registro.Codigo, "cuadrillas_codigo_key").IsUnique();

        entidad.Property(registro => registro.IdCuadrilla).HasColumnName("id_cuadrilla");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Codigo)
            .HasMaxLength(20)
            .HasColumnName("codigo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(200)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(80)
            .HasColumnName("nombre");
    }
}
