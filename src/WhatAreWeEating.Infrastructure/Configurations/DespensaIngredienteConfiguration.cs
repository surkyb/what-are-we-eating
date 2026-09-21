using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Recetas.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class DespensaIngredienteConfiguration : IEntityTypeConfiguration<DespensaIngrediente>
{
    public void Configure(EntityTypeBuilder<DespensaIngrediente> builder)
    {
        builder.ToTable("DespensaIngredientes");

        builder.HasKey(di => new { di.DespensaId, di.IngredienteId });

        builder.Property(di => di.CantidadDisponible)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(di => di.Unidad)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(di => di.Despensa)
            .WithMany(d => d.DespensaIngredientes)
            .HasForeignKey(di => di.DespensaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(di => di.Ingrediente)
            .WithMany(i => i.DespensaIngredientes)
            .HasForeignKey(di => di.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
