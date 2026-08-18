using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using ZemenServe.Cashier.Data;
using ZemenServe.Cashier.Services;
using ZemenServe.Shared.DTOs;
using ZemenServe.Shared.Enums;
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.ViewModels;

public partial class OrderEntryViewModel : ObservableObject
{
    private readonly Func<ZemenServeDbContext> _dbContextFactory;
    private readonly InventoryService _inventoryService;
    private readonly SignalRHostService _signalRHost;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private Waiter? _selectedWaiter;

    [ObservableProperty]
    private OrderDto? _selectedActiveOrder;

    public bool IsActiveOrderSelected => SelectedActiveOrder != null;

    partial void OnSelectedActiveOrderChanged(OrderDto? value)
    {
        OnPropertyChanged(nameof(IsActiveOrderSelected));
    }

    [ObservableProperty]
    private string? _cashierNote;

    [ObservableProperty]
    private decimal _cartTotal;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<MenuItem> MenuItems { get; } = new();
    public ObservableCollection<OrderItemDto> CartItems { get; } = new();
    public ObservableCollection<Waiter> Waiters { get; } = new();
    public ObservableCollection<OrderDto> ActiveOrders { get; } = new();

    public OrderEntryViewModel(
        Func<ZemenServeDbContext> dbContextFactory,
        InventoryService inventoryService,
        SignalRHostService signalRHost)
    {
        _dbContextFactory = dbContextFactory;
        _inventoryService = inventoryService;
        _signalRHost = signalRHost;

        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SelectCategoryCommand = new AsyncRelayCommand<string>(SelectCategoryAsync);
        AddToCartCommand = new RelayCommand<MenuItem>(AddToCart);
        RemoveFromCartCommand = new RelayCommand<OrderItemDto>(RemoveFromCart);
        IncreaseQuantityCommand = new RelayCommand<OrderItemDto>(IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<OrderItemDto>(DecreaseQuantity);
        CheckoutCommand = new AsyncRelayCommand(CheckoutAsync);
        SelectActiveOrderCommand = new RelayCommand<OrderDto>(SelectActiveOrder);
        ClearSelectedActiveOrderCommand = new RelayCommand(ClearSelectedActiveOrder);
        MarkPaidCommand = new AsyncRelayCommand<OrderDto>(MarkPaidAsync);
        RemoveItemFromActiveOrderCommand = new AsyncRelayCommand<OrderItemDto>(RemoveItemFromActiveOrderAsync);
        CancelActiveOrderCommand = new AsyncRelayCommand<OrderDto>(CancelActiveOrderAsync);

        _ = LoadDataAsync();
    }

    public IAsyncRelayCommand LoadDataCommand { get; }
    public IAsyncRelayCommand<string> SelectCategoryCommand { get; }
    public IRelayCommand<MenuItem> AddToCartCommand { get; }
    public IRelayCommand<OrderItemDto> RemoveFromCartCommand { get; }
    public IRelayCommand<OrderItemDto> IncreaseQuantityCommand { get; }
    public IRelayCommand<OrderItemDto> DecreaseQuantityCommand { get; }
    public IAsyncRelayCommand CheckoutCommand { get; }
    public IRelayCommand<OrderDto> SelectActiveOrderCommand { get; }
    public IRelayCommand ClearSelectedActiveOrderCommand { get; }
    public IAsyncRelayCommand<OrderDto> MarkPaidCommand { get; }
    public IAsyncRelayCommand<OrderItemDto> RemoveItemFromActiveOrderCommand { get; }
    public IAsyncRelayCommand<OrderDto> CancelActiveOrderCommand { get; }

    public async Task LoadDataAsync()
    {
        try
        {
            using var context = _dbContextFactory();

            // Load Categories from DB
            var dbCategories = await _inventoryService.GetCategoriesAsync();
            Categories.Clear();
            Categories.Add("All");
            foreach (var cat in dbCategories)
            {
                Categories.Add(cat.Name);
            }

            // Load Menu Items matching Category
            var items = await context.MenuItems.Where(m => m.IsActive).ToListAsync();
            MenuItems.Clear();
            foreach (var item in items)
            {
                if (SelectedCategory == "All" || item.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                {
                    MenuItems.Add(item);
                }
            }

            // Load Waiters
            var waiterList = await _inventoryService.GetWaitersAsync();
            Waiters.Clear();
            foreach (var w in waiterList)
            {
                Waiters.Add(w);
            }

            if (SelectedWaiter == null && Waiters.Any())
            {
                SelectedWaiter = Waiters.First();
            }

            // Load Active Unpaid Orders
            await RefreshActiveOrdersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading order entry data: {ex.Message}";
        }
    }

    private async Task SelectCategoryAsync(string? cat)
    {
        if (string.IsNullOrWhiteSpace(cat)) return;
        SelectedCategory = cat;
        await LoadDataAsync();
    }

    public async Task RefreshActiveOrdersAsync()
    {
        var orders = await _inventoryService.GetActiveOrdersAsync();
        ActiveOrders.Clear();
        foreach (var order in orders)
        {
            ActiveOrders.Add(order);
        }
    }

    private void SelectActiveOrder(OrderDto? order)
    {
        if (order == null) return;
        SelectedActiveOrder = order;

        if (SelectedWaiter == null || SelectedWaiter.Name != order.WaiterName)
        {
            SelectedWaiter = Waiters.FirstOrDefault(w => w.Name == order.WaiterName) ?? SelectedWaiter;
        }

        StatusMessage = $"Selected Order #{order.Id} (Waiter: {order.WaiterName ?? "N/A"}). New items added will append to this order!";
    }

    private void ClearSelectedActiveOrder()
    {
        SelectedActiveOrder = null;
        StatusMessage = "Creating new order.";
    }

    private void AddToCart(MenuItem? item)
    {
        if (item == null) return;

        var existing = CartItems.FirstOrDefault(c => c.MenuItemId == item.Id);
        if (existing != null)
        {
            existing.Quantity++;
            var index = CartItems.IndexOf(existing);
            CartItems[index] = existing;
        }
        else
        {
            CartItems.Add(new OrderItemDto
            {
                MenuItemId = item.Id,
                MenuItemName = item.Name,
                Quantity = 1,
                UnitPrice = item.Price
            });
        }
        UpdateCartTotal();
    }

    private void RemoveFromCart(OrderItemDto? item)
    {
        if (item == null) return;
        CartItems.Remove(item);
        UpdateCartTotal();
    }

    private void IncreaseQuantity(OrderItemDto? item)
    {
        if (item == null) return;
        item.Quantity++;
        var index = CartItems.IndexOf(item);
        CartItems[index] = item;
        UpdateCartTotal();
    }

    private void DecreaseQuantity(OrderItemDto? item)
    {
        if (item == null) return;
        if (item.Quantity > 1)
        {
            item.Quantity--;
            var index = CartItems.IndexOf(item);
            CartItems[index] = item;
        }
        else
        {
            CartItems.Remove(item);
        }
        UpdateCartTotal();
    }

    private void UpdateCartTotal()
    {
        CartTotal = CartItems.Sum(i => i.TotalPrice);
    }

    private async Task CheckoutAsync()
    {
        if (!CartItems.Any())
        {
            StatusMessage = "Cart is empty!";
            MessageBox.Show("Please add items to the cart before submitting.", "Empty Order", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedWaiter == null)
        {
            MessageBox.Show("Please select a waiter for this order.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var cartList = CartItems.ToList();
            int? activeOrderId = SelectedActiveOrder?.Id;

            var (orderDto, lowStockAlerts) = await _inventoryService.SubmitOrderAsync(
                cartList, 
                CashierNote, 
                SelectedWaiter.Id, 
                SelectedWaiter.Name, 
                activeOrderId
            );

            // Broadcast update to SignalR Kitchen clients
            await _signalRHost.BroadcastNewOrderAsync(orderDto);

            CartItems.Clear();
            CashierNote = string.Empty;
            UpdateCartTotal();
            SelectedActiveOrder = null;

            await RefreshActiveOrdersAsync();

            StatusMessage = activeOrderId.HasValue
                ? $"Appended items to Order #{orderDto.Id} successfully! Total: {orderDto.TotalAmount:N2} ETB"
                : $"New Order #{orderDto.Id} submitted to kitchen! (Waiter: {orderDto.WaiterName})";

            if (lowStockAlerts.Any())
            {
                var alertText = string.Join("\n", lowStockAlerts);
                MessageBox.Show(alertText, "Low Stock Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Checkout Error: {ex.Message}";
            MessageBox.Show($"Failed to complete checkout: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task MarkPaidAsync(OrderDto? order)
    {
        if (order == null) return;

        var result = MessageBox.Show(
            $"Process payment of {order.TotalAmount:N2} ETB for Order #{order.Id} (Waiter: {order.WaiterName ?? "N/A"})?",
            "Confirm Payment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _inventoryService.MarkOrderPaidAsync(order.Id);
                await _signalRHost.BroadcastOrderStatusChangedAsync(order.Id, OrderStatus.Paid);

                if (SelectedActiveOrder?.Id == order.Id)
                {
                    SelectedActiveOrder = null;
                }

                await RefreshActiveOrdersAsync();

                StatusMessage = $"Order #{order.Id} is fully PAID and closed!";
                MessageBox.Show($"Order #{order.Id} paid successfully!", "Payment Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to mark order paid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task RemoveItemFromActiveOrderAsync(OrderItemDto? item)
    {
        if (item == null || SelectedActiveOrder == null) return;

        var result = MessageBox.Show(
            $"Remove '{item.MenuItemName}' from Order #{SelectedActiveOrder.Id}?",
            "Confirm Item Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var updatedOrder = await _inventoryService.RemoveOrderItemAsync(SelectedActiveOrder.Id, item.MenuItemId);
                if (updatedOrder == null)
                {
                    // Order has no remaining items and was cancelled
                    await _signalRHost.BroadcastOrderStatusChangedAsync(SelectedActiveOrder.Id, OrderStatus.Cancelled);
                    StatusMessage = $"Order #{SelectedActiveOrder.Id} had all items removed and was CANCELLED.";
                    SelectedActiveOrder = null;
                }
                else
                {
                    await _signalRHost.BroadcastNewOrderAsync(updatedOrder);
                    SelectedActiveOrder = updatedOrder;
                    StatusMessage = $"Removed '{item.MenuItemName}'. Updated Order #{updatedOrder.Id} Total: {updatedOrder.TotalAmount:N2} ETB";
                }

                await RefreshActiveOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task CancelActiveOrderAsync(OrderDto? order)
    {
        if (order == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to CANCEL Order #{order.Id} (Waiter: {order.WaiterName ?? "N/A"})?\nThis will remove the order from the kitchen queue.",
            "Confirm Cancellation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _inventoryService.CancelOrderAsync(order.Id);
                await _signalRHost.BroadcastOrderStatusChangedAsync(order.Id, OrderStatus.Cancelled);

                if (SelectedActiveOrder?.Id == order.Id)
                {
                    SelectedActiveOrder = null;
                }

                await RefreshActiveOrdersAsync();

                StatusMessage = $"Order #{order.Id} was CANCELLED.";
                MessageBox.Show($"Order #{order.Id} cancelled successfully.", "Order Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to cancel order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
