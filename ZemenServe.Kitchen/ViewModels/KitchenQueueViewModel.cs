using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZemenServe.Kitchen.Services;
using ZemenServe.Shared.DTOs;
using ZemenServe.Shared.Enums;

namespace ZemenServe.Kitchen.ViewModels;

public partial class KitchenQueueViewModel : ObservableObject
{
    private readonly KitchenSignalRClient _client;

    [ObservableProperty]
    private string _connectionStatus = "Connecting...";

    [ObservableProperty]
    private string _cashierIp;

    [ObservableProperty]
    private int _cashierPort;

    public ObservableCollection<OrderDto> ActiveOrders { get; } = new();

    public KitchenQueueViewModel(KitchenSignalRClient client)
    {
        _client = client;
        _cashierIp = _client.Settings.ServerHost;
        _cashierPort = _client.Settings.ServerPort;

        _client.OnConnectionStatusChanged += (status) =>
        {
            Application.Current.Dispatcher.Invoke(() => ConnectionStatus = status);
        };

        _client.OnNewOrderReceived += (order) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = ActiveOrders.FirstOrDefault(o => o.Id == order.Id);
                if (existing == null)
                {
                    ActiveOrders.Add(order);
                }
                else
                {
                    var index = ActiveOrders.IndexOf(existing);
                    ActiveOrders[index] = order;
                }
            });
        };

        _client.OnOrderStatusChangedReceived += (change) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var target = ActiveOrders.FirstOrDefault(o => o.Id == change.OrderId);
                if (target != null)
                {
                    if (change.NewStatus == OrderStatus.Paid || change.NewStatus == OrderStatus.Cancelled)
                    {
                        ActiveOrders.Remove(target);
                    }
                    else
                    {
                        target.Status = change.NewStatus;
                        var index = ActiveOrders.IndexOf(target);
                        ActiveOrders[index] = target;
                    }
                }
            });
        };

        StartPreparingCommand = new AsyncRelayCommand<OrderDto>(StartPreparingAsync);
        MarkReadyCommand = new AsyncRelayCommand<OrderDto>(MarkReadyAsync);
        ServeCommand = new AsyncRelayCommand<OrderDto>(ServeAsync);
        SaveSettingsAndConnectCommand = new AsyncRelayCommand(SaveSettingsAndConnectAsync);

        _ = _client.StartAsync();
    }

    public IAsyncRelayCommand<OrderDto> StartPreparingCommand { get; }
    public IAsyncRelayCommand<OrderDto> MarkReadyCommand { get; }
    public IAsyncRelayCommand<OrderDto> ServeCommand { get; }
    public IAsyncRelayCommand SaveSettingsAndConnectCommand { get; }

    private async Task StartPreparingAsync(OrderDto? order)
    {
        if (order == null) return;
        await UpdateStatusAsync(order, OrderStatus.Preparing);
    }

    private async Task MarkReadyAsync(OrderDto? order)
    {
        if (order == null) return;
        await UpdateStatusAsync(order, OrderStatus.Ready);
    }

    private async Task ServeAsync(OrderDto? order)
    {
        if (order == null) return;
        await UpdateStatusAsync(order, OrderStatus.Served);
    }

    private async Task UpdateStatusAsync(OrderDto order, OrderStatus newStatus)
    {
        try
        {
            await _client.UpdateOrderStatusAsync(order.Id, newStatus);

            if (newStatus == OrderStatus.Served)
            {
                ActiveOrders.Remove(order);
            }
            else
            {
                order.Status = newStatus;
                var index = ActiveOrders.IndexOf(order);
                if (index >= 0) ActiveOrders[index] = order;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update order state: {ex.Message}", "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveSettingsAndConnectAsync()
    {
        _client.Settings.ServerHost = CashierIp;
        _client.Settings.ServerPort = CashierPort;
        _client.SaveSettings();
        await _client.StartAsync();
    }
}
