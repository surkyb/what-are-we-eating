using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Recetas.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class DespensaConfiguration : IEntityTypeConfiguration<Despensa>
{
    public void Configure(EntityTypeBuilder<Despensa> builder)
    {
        builder.ToTable("Despensas");

        builder.HasKey(d => d.Id);

        // Una despensa por usuario.
        builder.HasIndex(d => d.UsuarioId)
            .IsUnique();
    }
}
