using Microsoft.EntityFrameworkCore;
using WhatAreWeEating.Recetas.Entities;

namespace WhatAreWeEating.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

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
