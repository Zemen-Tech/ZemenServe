using Microsoft.AspNetCore.SignalR;
using ZemenServe.Shared.DTOs;

namespace ZemenServe.Cashier.Hubs;

public class OrderHub : Hub<IOrderHubClient>, IOrderHubServer
{
    public async Task SubmitNewOrder(OrderDto order)
    {
        await Clients.All.ReceiveNewOrder(order);
    }

    public async Task UpdateOrderStatus(OrderStatusChangeDto statusChange)
    {
        await Clients.All.ReceiveOrderStatusChanged(statusChange);
    }

    public async Task NotifyConnectionRestored()
    {
        await Clients.All.ReceiveConnectionRestored();
    }
}
