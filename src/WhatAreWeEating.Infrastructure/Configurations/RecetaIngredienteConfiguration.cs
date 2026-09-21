using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Recetas.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class RecetaIngredienteConfiguration : IEntityTypeConfiguration<RecetaIngrediente>
{
    public void Configure(EntityTypeBuilder<RecetaIngrediente> builder)
    {
        builder.ToTable("RecetaIngredientes");

        builder.HasKey(ri => new { ri.RecetaId, ri.IngredienteId });

        builder.Property(ri => ri.Cantidad)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(ri => ri.Unidad)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(ri => ri.Receta)
            .WithMany(r => r.RecetaIngredientes)
            .HasForeignKey(ri => ri.RecetaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ri => ri.Ingrediente)
            .WithMany(i => i.RecetaIngredientes)
            .HasForeignKey(ri => ri.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
