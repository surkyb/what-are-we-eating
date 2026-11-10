using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Core.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("Documentos");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Nombre).IsRequired().HasMaxLength(255);
        builder.Property(d => d.Tipo).IsRequired().HasMaxLength(100);
        builder.Property(d => d.TamanoBytes).IsRequired();
        builder.Property(d => d.FechaSubida).IsRequired();
        builder.Property(d => d.PropietarioId).IsRequired();
        builder.Property(d => d.ClaveAlmacenamiento).IsRequired().HasMaxLength(64);
        builder.Property(d => d.Eliminado).IsRequired();

        builder.HasIndex(d => d.PropietarioId);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(d => d.PropietarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
