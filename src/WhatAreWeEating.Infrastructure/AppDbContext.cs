using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Core.Entities;
using WhatAreWeEating.Recetas.Entities;

namespace WhatAreWeEating.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Core
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TokenUnUso> TokensUnUso => Set<TokenUnUso>();
    public DbSet<CorreoEnCola> CorreosEnCola => Set<CorreoEnCola>();

    // Recetas
    public DbSet<Receta> Recetas => Set<Receta>();
    public DbSet<Ingrediente> Ingredientes => Set<Ingrediente>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Despensa> Despensas => Set<Despensa>();
    public DbSet<RecetaIngrediente> RecetaIngredientes => Set<RecetaIngrediente>();
    public DbSet<DespensaIngrediente> DespensaIngredientes => Set<DespensaIngrediente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
