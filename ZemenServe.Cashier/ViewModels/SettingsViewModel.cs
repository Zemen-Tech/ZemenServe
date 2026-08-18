using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZemenServe.Cashier.Services;
using ZemenServe.Shared.DTOs;
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;

    [ObservableProperty]
    private string _newWaiterName = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<Waiter> Waiters { get; } = new();
    public ObservableCollection<WaiterSalesReportDto> WaiterLeaderboard { get; } = new();

    public SettingsViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        LoadWaitersCommand = new AsyncRelayCommand(LoadWaitersAsync);
        AddWaiterCommand = new AsyncRelayCommand(AddWaiterAsync);
        DeleteWaiterCommand = new AsyncRelayCommand<Waiter>(DeleteWaiterAsync);

        _ = LoadWaitersAsync();
    }

    public IAsyncRelayCommand LoadWaitersCommand { get; }
    public IAsyncRelayCommand AddWaiterCommand { get; }
    public IAsyncRelayCommand<Waiter> DeleteWaiterCommand { get; }

    public async Task LoadWaitersAsync()
    {
        try
        {
            var list = await _inventoryService.GetWaitersAsync();
            Waiters.Clear();
            foreach (var w in list)
            {
                Waiters.Add(w);
            }

            var leaderboard = await _inventoryService.GetWaiterSalesLeaderboardAsync();
            WaiterLeaderboard.Clear();
            foreach (var item in leaderboard)
            {
                WaiterLeaderboard.Add(item);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load waiters: {ex.Message}";
        }
    }

    private async Task AddWaiterAsync()
    {
        if (string.IsNullOrWhiteSpace(NewWaiterName))
        {
            MessageBox.Show("Please enter a valid waiter name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _inventoryService.AddWaiterAsync(NewWaiterName);
            NewWaiterName = string.Empty;
            await LoadWaitersAsync();
            StatusMessage = "Waiter registered successfully!";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding waiter: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteWaiterAsync(Waiter? waiter)
    {
        if (waiter == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to remove waiter '{waiter.Name}'?",
            "Confirm Remove",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _inventoryService.DeleteWaiterAsync(waiter.Id);
                await LoadWaitersAsync();
                StatusMessage = $"Waiter '{waiter.Name}' removed.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove waiter: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
