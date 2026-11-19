using ConstructoraEderTorres.Dominio.Herramientas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Herramientas;

public sealed class CategoriaHerramientaMapeo : IEntityTypeConfiguration<CategoriaHerramienta>
{
    public void Configure(EntityTypeBuilder<CategoriaHerramienta> entidad)
    {
        entidad.HasKey(registro => registro.IdCategoria).HasName("categorias_herramienta_pkey");

        entidad.ToTable("categorias_herramienta", "public");

        entidad.HasIndex(registro => registro.Nombre, "categorias_herramienta_nombre_key").IsUnique();

        entidad.Property(registro => registro.IdCategoria).HasColumnName("id_categoria");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(250)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(80)
            .HasColumnName("nombre");
    }
}
