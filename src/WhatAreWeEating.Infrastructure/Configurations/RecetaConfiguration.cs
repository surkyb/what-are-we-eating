using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Recetas.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class RecetaConfiguration : IEntityTypeConfiguration<Receta>
{
    public void Configure(EntityTypeBuilder<Receta> builder)
    {
        builder.ToTable("Recetas");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Instrucciones)
            .IsRequired();

        builder.Property(r => r.ImagenUrl)
            .HasMaxLength(500);

        builder.Property(r => r.EstadoPublicacion)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne(r => r.Categoria)
            .WithMany(c => c.Recetas)
            .HasForeignKey(r => r.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
