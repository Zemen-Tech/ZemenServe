using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZemenServe.Cashier.Hubs;
using ZemenServe.Shared.DTOs;

namespace ZemenServe.Cashier.Services;

public class SignalRHostService
{
    private IHost? _host;
    public bool IsRunning { get; private set; }

    public async Task StartAsync(int port = 5000)
    {
        if (IsRunning) return;

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .SetIsOriginAllowed(_ => true)
                      .AllowCredentials();
            });
        });

        builder.Services.AddSignalR();

        var app = builder.Build();

        app.UseCors();
        app.MapHub<OrderHub>("/orderhub");

        _host = app;
        await _host.StartAsync();
        IsRunning = true;
    }

    public async Task BroadcastNewOrderAsync(OrderDto order)
    {
        if (_host == null) return;
        var hubContext = _host.Services.GetRequiredService<IHubContext<OrderHub, IOrderHubClient>>();
        await hubContext.Clients.All.ReceiveNewOrder(order);
    }

    public async Task BroadcastOrderStatusChangedAsync(OrderStatusChangeDto statusChange)
    {
        if (_host == null) return;
        var hubContext = _host.Services.GetRequiredService<IHubContext<OrderHub, IOrderHubClient>>();
        await hubContext.Clients.All.ReceiveOrderStatusChanged(statusChange);
    }

    public async Task BroadcastOrderStatusChangedAsync(int orderId, ZemenServe.Shared.Enums.OrderStatus newStatus)
    {
        await BroadcastOrderStatusChangedAsync(new OrderStatusChangeDto
        {
            OrderId = orderId,
            NewStatus = newStatus,
            UpdatedAt = DateTime.Now
        });
    }

    public async Task StopAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
            IsRunning = false;
        }
    }
}
