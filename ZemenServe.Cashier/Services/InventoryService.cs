using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ZemenServe.Cashier.Data;
using ZemenServe.Shared.DTOs;
using ZemenServe.Shared.Enums;
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.Services;

public class InventoryService
{
    private readonly Func<ZemenServeDbContext> _dbContextFactory;

    public InventoryService(Func<ZemenServeDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<(OrderDto OrderDto, List<string> LowStockAlerts)> SubmitOrderAsync(
        List<OrderItemDto> cartItems, string? cashierNote)
    {
        using var context = _dbContextFactory();
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var lowStockAlerts = new List<string>();
            decimal totalAmount = cartItems.Sum(item => item.TotalPrice);

            var order = new Order
            {
                CreatedAt = DateTime.Now,
                Status = OrderStatus.Pending,
                TotalAmount = totalAmount,
                CashierNote = cashierNote
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var orderItemEntities = new List<OrderItem>();

            foreach (var cartItem in cartItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    MenuItemId = cartItem.MenuItemId,
                    Quantity = cartItem.Quantity,
                    UnitPriceAtSale = cartItem.UnitPrice
                };
                context.OrderItems.Add(orderItem);
                orderItemEntities.Add(orderItem);

                // Fetch recipe for this menu item
                var recipes = await context.Recipes
                    .Include(r => r.Ingredient)
                    .Where(r => r.MenuItemId == cartItem.MenuItemId)
                    .ToListAsync();

                foreach (var recipe in recipes)
                {
                    if (recipe.Ingredient != null)
                    {
                        double qtyDeducted = recipe.QuantityRequired * cartItem.Quantity;
                        recipe.Ingredient.StockQuantity -= qtyDeducted;

                        // Create inventory log
                        var log = new InventoryLog
                        {
                            IngredientId = recipe.Ingredient.Id,
                            ChangeAmount = -qtyDeducted,
                            Reason = $"Order #{order.Id} Sale ({cartItem.MenuItemName} x{cartItem.Quantity})",
                            Timestamp = DateTime.Now
                        };
                        context.InventoryLogs.Add(log);

                        // Check low stock threshold
                        if (recipe.Ingredient.StockQuantity <= recipe.Ingredient.LowStockThreshold)
                        {
                            lowStockAlerts.Add(
                                $"LOW STOCK ALERT: {recipe.Ingredient.Name} stock is {recipe.Ingredient.StockQuantity:F2} {recipe.Ingredient.Unit} (Threshold: {recipe.Ingredient.LowStockThreshold} {recipe.Ingredient.Unit})"
                            );
                        }
                    }
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            var orderDto = new OrderDto
            {
                Id = order.Id,
                CreatedAt = order.CreatedAt,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CashierNote = order.CashierNote,
                Items = cartItems
            };

            return (orderDto, lowStockAlerts);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
    {
        using var context = _dbContextFactory();
        var order = await context.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.Status = newStatus;
            await context.SaveChangesAsync();
        }
    }

    public async Task AddNewIngredientAsync(string name, string unit, decimal costPerUnit, double initialStock, double lowStockThreshold, string? note)
    {
        using var context = _dbContextFactory();

        var nameTrimmed = name.Trim();
        var exists = await context.Ingredients.AnyAsync(i => i.Name.ToLower() == nameTrimmed.ToLower());
        if (exists)
        {
            throw new InvalidOperationException($"An ingredient with the name '{nameTrimmed}' already exists in inventory.");
        }

        var ingredient = new Ingredient
        {
            Name = nameTrimmed,
            Unit = unit,
            CostPerUnit = costPerUnit,
            StockQuantity = initialStock,
            LowStockThreshold = lowStockThreshold
        };

        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();

        if (initialStock > 0 || !string.IsNullOrWhiteSpace(note))
        {
            context.InventoryLogs.Add(new InventoryLog
            {
                IngredientId = ingredient.Id,
                ChangeAmount = initialStock,
                Reason = string.IsNullOrWhiteSpace(note) ? "Initial Stock Registration" : note,
                Timestamp = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateIngredientAsync(int id, string name, string unit, decimal costPerUnit, double lowStockThreshold)
    {
        using var context = _dbContextFactory();

        var nameTrimmed = name.Trim();
        var existsOther = await context.Ingredients.AnyAsync(i => i.Id != id && i.Name.ToLower() == nameTrimmed.ToLower());
        if (existsOther)
        {
            throw new InvalidOperationException($"An ingredient with the name '{nameTrimmed}' already exists in inventory.");
        }

        var ingredient = await context.Ingredients.FindAsync(id);
        if (ingredient != null)
        {
            ingredient.Name = nameTrimmed;
            ingredient.Unit = unit;
            ingredient.CostPerUnit = costPerUnit;
            ingredient.LowStockThreshold = lowStockThreshold;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteIngredientAsync(int id)
    {
        using var context = _dbContextFactory();
        var isUsedInRecipe = await context.Recipes.AnyAsync(r => r.IngredientId == id);
        if (isUsedInRecipe)
        {
            throw new InvalidOperationException("Cannot delete ingredient because it is currently linked to active food recipes.");
        }

        var ingredient = await context.Ingredients.FindAsync(id);
        if (ingredient != null)
        {
            context.Ingredients.Remove(ingredient);
            await context.SaveChangesAsync();
        }
    }

    public async Task RestockIngredientAsync(int ingredientId, double addQuantity, string reason)
    {
        using var context = _dbContextFactory();
        var ingredient = await context.Ingredients.FindAsync(ingredientId);
        if (ingredient != null)
        {
            ingredient.StockQuantity += addQuantity;
            context.InventoryLogs.Add(new InventoryLog
            {
                IngredientId = ingredientId,
                ChangeAmount = addQuantity,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Restock" : reason,
                Timestamp = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Ingredient>> GetIngredientsAsync()
    {
        using var context = _dbContextFactory();
        return await context.Ingredients.AsNoTracking().ToListAsync();
    }

    public async Task<List<InventoryLogDto>> GetInventoryLogsAsync()
    {
        using var context = _dbContextFactory();
        var logs = await context.InventoryLogs
            .Include(l => l.Ingredient)
            .OrderByDescending(l => l.Timestamp)
            .AsNoTracking()
            .ToListAsync();

        return logs.Select(l => new InventoryLogDto
        {
            Id = l.Id,
            IngredientId = l.IngredientId,
            IngredientName = l.Ingredient?.Name ?? "Unknown Ingredient",
            ChangeAmount = l.ChangeAmount,
            Reason = l.Reason,
            Timestamp = l.Timestamp
        }).ToList();
    }

    // --- Category CRUD ---
    public async Task<List<Category>> GetCategoriesAsync()
    {
        using var context = _dbContextFactory();
        return await context.Categories.Where(c => c.IsActive).AsNoTracking().ToListAsync();
    }

    public async Task AddCategoryAsync(string name)
    {
        using var context = _dbContextFactory();
        var nameTrimmed = name.Trim();
        var exists = await context.Categories.AnyAsync(c => c.Name.ToLower() == nameTrimmed.ToLower());
        if (exists)
        {
            throw new InvalidOperationException($"Category '{nameTrimmed}' already exists.");
        }
        context.Categories.Add(new Category { Name = nameTrimmed, IsActive = true });
        await context.SaveChangesAsync();
    }

    public async Task UpdateCategoryAsync(int id, string newName)
    {
        using var context = _dbContextFactory();
        var category = await context.Categories.FindAsync(id);
        if (category != null)
        {
            category.Name = newName.Trim();
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteCategoryAsync(int id)
    {
        using var context = _dbContextFactory();
        var category = await context.Categories.FindAsync(id);
        if (category != null)
        {
            category.IsActive = false; // Soft delete
            await context.SaveChangesAsync();
        }
    }

    // --- Menu Item CRUD ---
    public async Task SaveMenuItemWithRecipesAsync(
        int? menuItemId, string name, string category, decimal sellingPrice, List<(int IngredientId, double QtyRequired)> recipeItems)
    {
        using var context = _dbContextFactory();
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            MenuItem menuItem;

            if (menuItemId.HasValue && menuItemId.Value > 0)
            {
                menuItem = await context.MenuItems.Include(m => m.Recipes).FirstAsync(m => m.Id == menuItemId.Value);
                menuItem.Name = name;
                menuItem.Category = category;
                menuItem.Price = sellingPrice;

                // Remove existing recipes
                context.Recipes.RemoveRange(menuItem.Recipes);
            }
            else
            {
                menuItem = new MenuItem
                {
                    Name = name,
                    Category = category,
                    Price = sellingPrice,
                    IsActive = true
                };
                context.MenuItems.Add(menuItem);
                await context.SaveChangesAsync(); // generate ID
            }

            // Add new recipes
            foreach (var (ingredientId, qtyRequired) in recipeItems)
            {
                context.Recipes.Add(new Recipe
                {
                    MenuItemId = menuItem.Id,
                    IngredientId = ingredientId,
                    QuantityRequired = qtyRequired
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteMenuItemAsync(int menuItemId)
    {
        using var context = _dbContextFactory();
        var item = await context.MenuItems.Include(m => m.Recipes).FirstOrDefaultAsync(m => m.Id == menuItemId);
        if (item != null)
        {
            context.Recipes.RemoveRange(item.Recipes);
            context.MenuItems.Remove(item);
            await context.SaveChangesAsync();
        }
    }
}
