using System;
using System.Collections.Generic;
using ZemenServe.Shared.Enums;

namespace ZemenServe.Shared.Models;

public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs"; // g, ml, pcs, etc.
    public decimal CostPerUnit { get; set; }
    public double StockQuantity { get; set; }
    public double LowStockThreshold { get; set; }

    public bool IsLowStock => StockQuantity <= LowStockThreshold;
    public string StockStatus => IsLowStock ? "⚠️ LOW STOCK" : "NORMAL";

    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();
}

public class Recipe
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public virtual MenuItem? MenuItem { get; set; }

    public int IngredientId { get; set; }
    public virtual Ingredient? Ingredient { get; set; }

    public double QuantityRequired { get; set; }
}

public class Waiter
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public string? CashierNote { get; set; }
    public int? WaiterId { get; set; }
    public string? WaiterName { get; set; }
    public bool IsPaid { get; set; } = false;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public virtual Order? Order { get; set; }

    public int MenuItemId { get; set; }
    public virtual MenuItem? MenuItem { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPriceAtSale { get; set; }
}

public class InventoryLog
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public virtual Ingredient? Ingredient { get; set; }

    public double ChangeAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
