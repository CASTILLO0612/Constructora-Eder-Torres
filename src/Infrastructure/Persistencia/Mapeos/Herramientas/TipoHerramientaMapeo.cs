using ConstructoraEderTorres.Dominio.Herramientas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Herramientas;

public sealed class TipoHerramientaMapeo : IEntityTypeConfiguration<TipoHerramienta>
{
    public void Configure(EntityTypeBuilder<TipoHerramienta> entidad)
    {
        entidad.HasKey(registro => registro.IdTipoHerramienta).HasName("tipos_herramienta_pkey");

        entidad.ToTable("tipos_herramienta", "public");

        entidad.HasIndex(registro => new { registro.IdCategoria, registro.Nombre }, "uq_tipo_herramienta").IsUnique();

        entidad.Property(registro => registro.IdTipoHerramienta).HasColumnName("id_tipo_herramienta");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(250)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.IdCategoria).HasColumnName("id_categoria");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(100)
            .HasColumnName("nombre");

        entidad.HasOne<CategoriaHerramienta>().WithMany()
            .HasForeignKey(registro => registro.IdCategoria)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_tipo_herramienta_categoria");
    }
}
