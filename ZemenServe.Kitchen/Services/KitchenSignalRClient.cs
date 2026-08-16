using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using ZemenServe.Shared.Configuration;
using ZemenServe.Shared.DTOs;
using ZemenServe.Shared.Enums;

namespace ZemenServe.Kitchen.Services;

public class KitchenSignalRClient
{
    private HubConnection? _connection;
    public AppSettings Settings { get; private set; } = new();
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<OrderDto>? OnNewOrderReceived;
    public event Action<OrderStatusChangeDto>? OnOrderStatusChangedReceived;
    public event Action<string>? OnConnectionStatusChanged;

    public KitchenSignalRClient()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) Settings = loaded;
            }
            else
            {
                SaveSettings();
            }
        }
        catch
        {
            // Fallback to default
        }
    }

    public void SaveSettings()
    {
        try
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    public async Task StartAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        OnConnectionStatusChanged?.Invoke($"Connecting to {Settings.ServerUrl}...");

        _connection = new HubConnectionBuilder()
            .WithUrl(Settings.ServerUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<OrderDto>("ReceiveNewOrder", (order) =>
        {
            OnNewOrderReceived?.Invoke(order);
        });

        _connection.On<OrderStatusChangeDto>("ReceiveOrderStatusChanged", (statusChange) =>
        {
            OnOrderStatusChangedReceived?.Invoke(statusChange);
        });

        _connection.Reconnecting += (ex) =>
        {
            OnConnectionStatusChanged?.Invoke("Reconnecting to Cashier Server...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            OnConnectionStatusChanged?.Invoke("Connected");
            return Task.CompletedTask;
        };

        _connection.Closed += (ex) =>
        {
            OnConnectionStatusChanged?.Invoke("Disconnected");
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
            OnConnectionStatusChanged?.Invoke("Connected");
        }
        catch (Exception ex)
        {
            OnConnectionStatusChanged?.Invoke($"Connection Failed: {ex.Message}");
        }
    }

    public async Task UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
    {
        if (_connection == null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to Cashier server.");
        }

        var payload = new OrderStatusChangeDto
        {
            OrderId = orderId,
            NewStatus = newStatus,
            UpdatedAt = DateTime.Now
        };

        await _connection.InvokeAsync("UpdateOrderStatus", payload);
    }
}
