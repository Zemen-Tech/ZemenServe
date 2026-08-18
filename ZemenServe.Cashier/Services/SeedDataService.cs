using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ZemenServe.Cashier.Data;
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.Services;

public static class SeedDataService
{
    public static async Task SeedAsync(ZemenServeDbContext context)
    {
        // Ensure database exists and SQLite WAL mode is enabled
        try
        {
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            // Ignore existing table schema creation errors if the SQLite file already exists
            System.Diagnostics.Debug.WriteLine($"EnsureCreated Note: {ex.Message}");
        }

        try { await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;"); } catch { }

        // Ensure categories table exists even if database was created prior to Category entity addition
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS categories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1
            );
        ");

        // Ensure waiters table exists
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS waiters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1
            );
        ");

        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE waiters ADD COLUMN is_active INTEGER NOT NULL DEFAULT 1;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE waiters ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;"); } catch { }

        // Ensure orders table columns exist
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE orders ADD COLUMN WaiterId INTEGER NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE orders ADD COLUMN WaiterName TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE orders ADD COLUMN IsPaid INTEGER NOT NULL DEFAULT 0;"); } catch { }

        // Seed Categories if empty
        if (!await context.Categories.AnyAsync())
        {
            context.Categories.AddRange(
                new Category { Name = "Traditional Dishes", IsActive = true },
                new Category { Name = "Western / Fast Food", IsActive = true },
                new Category { Name = "Beverages", IsActive = true },
                new Category { Name = "Desserts & Snacks", IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        // Seed Waiters if empty
        if (!await context.Waiters.AnyAsync())
        {
            context.Waiters.AddRange(
                new Waiter { Name = "Abebe Kebede", IsActive = true },
                new Waiter { Name = "Tigist Haile", IsActive = true },
                new Waiter { Name = "Dawit Yilma", IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        if (await context.MenuItems.AnyAsync())
        {
            return; // Data already seeded
        }

        // 1. Ingredients
        var beef = new Ingredient { Name = "Beef Meat", Unit = "kg", CostPerUnit = 450.00m, StockQuantity = 25.0, LowStockThreshold = 5.0 };
        var chicken = new Ingredient { Name = "Whole Chicken", Unit = "pcs", CostPerUnit = 600.00m, StockQuantity = 15.0, LowStockThreshold = 3.0 };
        var onions = new Ingredient { Name = "Onions", Unit = "kg", CostPerUnit = 60.00m, StockQuantity = 50.0, LowStockThreshold = 10.0 };
        var berbere = new Ingredient { Name = "Berbere Spice", Unit = "kg", CostPerUnit = 350.00m, StockQuantity = 10.0, LowStockThreshold = 2.0 };
        var kibbeh = new Ingredient { Name = "Niter Kibbeh (Clarified Butter)", Unit = "kg", CostPerUnit = 700.00m, StockQuantity = 8.0, LowStockThreshold = 2.0 };
        var shiroPowder = new Ingredient { Name = "Shiro Powder", Unit = "kg", CostPerUnit = 250.00m, StockQuantity = 20.0, LowStockThreshold = 4.0 };
        var burgerBun = new Ingredient { Name = "Burger Buns", Unit = "pcs", CostPerUnit = 25.00m, StockQuantity = 40.0, LowStockThreshold = 10.0 };
        var cheese = new Ingredient { Name = "Cheese Slices", Unit = "pcs", CostPerUnit = 35.00m, StockQuantity = 50.0, LowStockThreshold = 10.0 };
        var coffeeBeans = new Ingredient { Name = "Ethiopian Roasted Coffee Beans", Unit = "kg", CostPerUnit = 500.00m, StockQuantity = 12.0, LowStockThreshold = 2.5 };
        var milk = new Ingredient { Name = "Fresh Milk", Unit = "L", CostPerUnit = 75.00m, StockQuantity = 30.0, LowStockThreshold = 5.0 };
        var softDrinkBottle = new Ingredient { Name = "Soft Drink 330ml Bottle", Unit = "pcs", CostPerUnit = 30.00m, StockQuantity = 120.0, LowStockThreshold = 20.0 };
        var amboWaterBottle = new Ingredient { Name = "Ambo Mineral Water 500ml", Unit = "pcs", CostPerUnit = 25.00m, StockQuantity = 100.0, LowStockThreshold = 15.0 };

        context.Ingredients.AddRange(
            beef, chicken, onions, berbere, kibbeh, shiroPowder,
            burgerBun, cheese, coffeeBeans, milk, softDrinkBottle, amboWaterBottle
        );
        await context.SaveChangesAsync();

        // 2. Menu Items
        var tibs = new MenuItem { Name = "Special Beef Tibs", Category = "Traditional Dishes", Price = 480.00m, IsActive = true };
        var doroWot = new MenuItem { Name = "Doro Wot", Category = "Traditional Dishes", Price = 550.00m, IsActive = true };
        var shiro = new MenuItem { Name = "Shiro Tegabeno", Category = "Traditional Dishes", Price = 250.00m, IsActive = true };
        var beyaynetu = new MenuItem { Name = "Veggie Beyaynetu", Category = "Traditional Dishes", Price = 280.00m, IsActive = true };
        var burger = new MenuItem { Name = "Special Cheese Burger", Category = "Western / Fast Food", Price = 380.00m, IsActive = true };
        var clubSandwich = new MenuItem { Name = "Club Sandwich", Category = "Western / Fast Food", Price = 340.00m, IsActive = true };
        var buna = new MenuItem { Name = "Ethiopian Coffee (Buna)", Category = "Beverages", Price = 35.00m, IsActive = true };
        var macchiato = new MenuItem { Name = "Macchiato", Category = "Beverages", Price = 50.00m, IsActive = true };
        var softDrink = new MenuItem { Name = "Coca Cola / Fanta / Sprite", Category = "Beverages", Price = 45.00m, IsActive = true };
        var ambo = new MenuItem { Name = "Ambo Mineral Water", Category = "Beverages", Price = 35.00m, IsActive = true };

        context.MenuItems.AddRange(
            tibs, doroWot, shiro, beyaynetu, burger, clubSandwich, buna, macchiato, softDrink, ambo
        );
        await context.SaveChangesAsync();

        // 3. Recipes (Mapping food item consumption of raw ingredients)
        context.Recipes.AddRange(
            new Recipe { MenuItemId = tibs.Id, IngredientId = beef.Id, QuantityRequired = 0.35 },
            new Recipe { MenuItemId = tibs.Id, IngredientId = onions.Id, QuantityRequired = 0.10 },
            new Recipe { MenuItemId = tibs.Id, IngredientId = kibbeh.Id, QuantityRequired = 0.05 },

            new Recipe { MenuItemId = doroWot.Id, IngredientId = chicken.Id, QuantityRequired = 0.25 },
            new Recipe { MenuItemId = doroWot.Id, IngredientId = onions.Id, QuantityRequired = 0.20 },
            new Recipe { MenuItemId = doroWot.Id, IngredientId = berbere.Id, QuantityRequired = 0.04 },
            new Recipe { MenuItemId = doroWot.Id, IngredientId = kibbeh.Id, QuantityRequired = 0.04 },

            new Recipe { MenuItemId = shiro.Id, IngredientId = shiroPowder.Id, QuantityRequired = 0.12 },
            new Recipe { MenuItemId = shiro.Id, IngredientId = onions.Id, QuantityRequired = 0.05 },
            new Recipe { MenuItemId = shiro.Id, IngredientId = kibbeh.Id, QuantityRequired = 0.03 },

            new Recipe { MenuItemId = burger.Id, IngredientId = beef.Id, QuantityRequired = 0.20 },
            new Recipe { MenuItemId = burger.Id, IngredientId = burgerBun.Id, QuantityRequired = 1.0 },
            new Recipe { MenuItemId = burger.Id, IngredientId = cheese.Id, QuantityRequired = 1.0 },

            new Recipe { MenuItemId = buna.Id, IngredientId = coffeeBeans.Id, QuantityRequired = 0.02 },

            new Recipe { MenuItemId = macchiato.Id, IngredientId = coffeeBeans.Id, QuantityRequired = 0.02 },
            new Recipe { MenuItemId = macchiato.Id, IngredientId = milk.Id, QuantityRequired = 0.15 },

            new Recipe { MenuItemId = softDrink.Id, IngredientId = softDrinkBottle.Id, QuantityRequired = 1.0 },

            new Recipe { MenuItemId = ambo.Id, IngredientId = amboWaterBottle.Id, QuantityRequired = 1.0 }
        );

        await context.SaveChangesAsync();
    }
}
