using Microsoft.EntityFrameworkCore;
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.Data;

public class ZemenServeDbContext : DbContext
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<InventoryLog> InventoryLogs => Set<InventoryLog>();
    public DbSet<Category> Categories => Set<Category>();

    public string DbPath { get; }

    public ZemenServeDbContext()
    {
        DbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zemenserve.db");
    }

    public ZemenServeDbContext(DbContextOptions<ZemenServeDbContext> options)
        : base(options)
    {
        DbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zemenserve.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath};Cache=Shared;Mode=ReadWriteCreate;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        // MenuItem configuration
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.ToTable("menu_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        // Ingredient configuration
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.ToTable("ingredients");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CostPerUnit).HasPrecision(18, 2);
        });

        // Recipe configuration
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToTable("recipes");
            entity.HasKey(e => e.Id);
            entity.HasOne(r => r.MenuItem)
                  .WithMany(m => m.Recipes)
                  .HasForeignKey(r => r.MenuItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Ingredient)
                  .WithMany(i => i.Recipes)
                  .HasForeignKey(r => r.IngredientId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        // OrderItem configuration
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPriceAtSale).HasPrecision(18, 2);

            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.OrderItems)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.MenuItem)
                  .WithMany(m => m.OrderItems)
                  .HasForeignKey(oi => oi.MenuItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // InventoryLog configuration
        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.ToTable("inventory_log");
            entity.HasKey(e => e.Id);
            entity.HasOne(il => il.Ingredient)
                  .WithMany(i => i.InventoryLogs)
                  .HasForeignKey(il => il.IngredientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
