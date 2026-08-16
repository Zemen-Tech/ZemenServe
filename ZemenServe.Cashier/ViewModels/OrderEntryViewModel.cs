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
    private string? _cashierNote;

    [ObservableProperty]
    private decimal _cartTotal;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<MenuItem> MenuItems { get; } = new();
    public ObservableCollection<OrderItemDto> CartItems { get; } = new();

    public OrderEntryViewModel(
        Func<ZemenServeDbContext> dbContextFactory,
        InventoryService inventoryService,
        SignalRHostService signalRHost)
    {
        _dbContextFactory = dbContextFactory;
        _inventoryService = inventoryService;
        _signalRHost = signalRHost;

        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        AddToCartCommand = new RelayCommand<MenuItem>(AddToCart);
        RemoveFromCartCommand = new RelayCommand<OrderItemDto>(RemoveFromCart);
        IncreaseQuantityCommand = new RelayCommand<OrderItemDto>(IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<OrderItemDto>(DecreaseQuantity);
        CheckoutCommand = new AsyncRelayCommand(CheckoutAsync);

        _ = LoadDataAsync();
    }

    public IAsyncRelayCommand LoadDataCommand { get; }
    public IRelayCommand<MenuItem> AddToCartCommand { get; }
    public IRelayCommand<OrderItemDto> RemoveFromCartCommand { get; }
    public IRelayCommand<OrderItemDto> IncreaseQuantityCommand { get; }
    public IRelayCommand<OrderItemDto> DecreaseQuantityCommand { get; }
    public IAsyncRelayCommand CheckoutCommand { get; }

    private async Task LoadDataAsync()
    {
        using var context = _dbContextFactory();
        var items = await context.MenuItems.Where(m => m.IsActive).ToListAsync();

        MenuItems.Clear();
        foreach (var item in items)
        {
            if (SelectedCategory == "All" || item.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
            {
                MenuItems.Add(item);
            }
        }

        var cats = items.Select(m => m.Category).Distinct().ToList();
        Categories.Clear();
        Categories.Add("All");
        foreach (var c in cats) Categories.Add(c);
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        _ = LoadDataAsync();
    }

    private void AddToCart(MenuItem? item)
    {
        if (item == null) return;

        var existing = CartItems.FirstOrDefault(c => c.MenuItemId == item.Id);
        if (existing != null)
        {
            existing.Quantity++;
            // Re-trigger notification
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
            return;
        }

        try
        {
            var cartList = CartItems.ToList();
            var (orderDto, lowStockAlerts) = await _inventoryService.SubmitOrderAsync(cartList, CashierNote);

            // Broadcast to SignalR Kitchen clients
            await _signalRHost.BroadcastNewOrderAsync(orderDto);

            CartItems.Clear();
            CashierNote = string.Empty;
            UpdateCartTotal();

            StatusMessage = $"Order #{orderDto.Id} submitted successfully! ({orderDto.TotalAmount:N2} ETB)";

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
}
