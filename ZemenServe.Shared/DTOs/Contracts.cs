using System;
using System.Collections.Generic;
using ZemenServe.Shared.Enums;

namespace ZemenServe.Shared.DTOs;

public class OrderItemDto
{
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
}

public class OrderDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string? CashierNote { get; set; }
    public int? WaiterId { get; set; }
    public string? WaiterName { get; set; }
    public bool IsPaid { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderStatusChangeDto
{
    public int OrderId { get; set; }
    public OrderStatus NewStatus { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class InventoryLogDto
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public double ChangeAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class DailyReportItemBreakdownDto
{
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal EstimatedCogs { get; set; }
    public decimal GrossProfit => TotalRevenue - EstimatedCogs;
}

public class WaiterSalesReportDto
{
    public int WaiterId { get; set; }
    public string WaiterName { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int Rank { get; set; }
    public string Badge => Rank switch
    {
        1 => "🥇 1st Place",
        2 => "🥈 2nd Place",
        3 => "🥉 3rd Place",
        _ => $"#{Rank}"
    };
}

public class DailyReportDto
{
    public DateTime ReportDate { get; set; }
    public int TotalOrdersCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCogs { get; set; }
    public decimal NetProfit => TotalRevenue - TotalCogs;
    public List<DailyReportItemBreakdownDto> ItemsSold { get; set; } = new();
    public List<WaiterSalesReportDto> WaiterSales { get; set; } = new();
}

public interface IOrderHubClient
{
    Task ReceiveNewOrder(OrderDto order);
    Task ReceiveOrderStatusChanged(OrderStatusChangeDto statusChange);
    Task ReceiveConnectionRestored();
}

public interface IOrderHubServer
{
    Task SubmitNewOrder(OrderDto order);
    Task UpdateOrderStatus(OrderStatusChangeDto statusChange);
    Task NotifyConnectionRestored();
}
