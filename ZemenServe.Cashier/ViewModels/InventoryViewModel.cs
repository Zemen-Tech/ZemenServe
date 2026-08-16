using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZemenServe.Cashier.Services;
using ZemenServe.Shared.DTOs;
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private List<Ingredient> _allIngredients = new();

    [ObservableProperty]
    private string _ingredientSearchQuery = string.Empty;

    [ObservableProperty]
    private Ingredient? _selectedIngredient;

    [ObservableProperty]
    private double _restockQuantity;

    [ObservableProperty]
    private string _restockReason = "Routine Restock";

    // New Ingredient Fields
    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newUnit = "kg";

    [ObservableProperty]
    private decimal _newCostPerUnit;

    [ObservableProperty]
    private double _newInitialStock;

    [ObservableProperty]
    private double _newLowStockThreshold = 5.0;

    [ObservableProperty]
    private string _newNote = "Initial Purchase";

    // Edit Ingredient Fields
    [ObservableProperty]
    private bool _isEditingIngredient;

    [ObservableProperty]
    private int _editIngredientId;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editUnit = "kg";

    [ObservableProperty]
    private decimal _editCostPerUnit;

    [ObservableProperty]
    private double _editLowStockThreshold;

    public ObservableCollection<string> UnitsList { get; } = new() { "kg", "L", "pcs", "unit-less" };
    public ObservableCollection<Ingredient> Ingredients { get; } = new();
    public ObservableCollection<InventoryLogDto> InventoryLogs { get; } = new();

    public InventoryViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        RestockCommand = new AsyncRelayCommand(RestockAsync);
        AddNewIngredientCommand = new AsyncRelayCommand(AddNewIngredientAsync);

        StartEditIngredientCommand = new RelayCommand<Ingredient>(StartEditIngredient);
        SaveEditIngredientCommand = new AsyncRelayCommand(SaveEditIngredientAsync);
        CancelEditIngredientCommand = new RelayCommand(CancelEditIngredient);
        DeleteIngredientCommand = new AsyncRelayCommand<Ingredient>(DeleteIngredientAsync);

        _ = RefreshAsync();
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RestockCommand { get; }
    public IAsyncRelayCommand AddNewIngredientCommand { get; }
    public IRelayCommand<Ingredient> StartEditIngredientCommand { get; }
    public IAsyncRelayCommand SaveEditIngredientCommand { get; }
    public IRelayCommand CancelEditIngredientCommand { get; }
    public IAsyncRelayCommand<Ingredient> DeleteIngredientCommand { get; }

    public async Task RefreshAsync()
    {
        _allIngredients = await _inventoryService.GetIngredientsAsync();
        ApplyIngredientFilter();

        var logs = await _inventoryService.GetInventoryLogsAsync();
        InventoryLogs.Clear();
        foreach (var log in logs)
        {
            InventoryLogs.Add(log);
        }
    }

    partial void OnIngredientSearchQueryChanged(string value)
    {
        ApplyIngredientFilter();
    }

    private void ApplyIngredientFilter()
    {
        Ingredients.Clear();
        foreach (var item in _allIngredients)
        {
            if (string.IsNullOrWhiteSpace(IngredientSearchQuery) ||
                item.Name.Contains(IngredientSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                item.Unit.Contains(IngredientSearchQuery, StringComparison.OrdinalIgnoreCase))
            {
                Ingredients.Add(item);
            }
        }
    }

    private async Task RestockAsync()
    {
        if (SelectedIngredient == null || RestockQuantity <= 0)
        {
            MessageBox.Show("Please select an ingredient and enter a positive restock quantity.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _inventoryService.RestockIngredientAsync(SelectedIngredient.Id, RestockQuantity, RestockReason);
            RestockQuantity = 0;
            await RefreshAsync();
            MessageBox.Show("Stock updated and transaction logged successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AddNewIngredientAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            MessageBox.Show("Please enter an ingredient name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (NewCostPerUnit <= 0)
        {
            MessageBox.Show("Please enter a valid cost per unit.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _inventoryService.AddNewIngredientAsync(
                NewName, NewUnit, NewCostPerUnit, NewInitialStock, NewLowStockThreshold, NewNote
            );

            // Clear inputs
            NewName = string.Empty;
            NewInitialStock = 0;
            NewNote = "Initial Purchase";

            await RefreshAsync();
            MessageBox.Show($"Ingredient '{NewName}' registered successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StartEditIngredient(Ingredient? ingredient)
    {
        if (ingredient == null) return;

        EditIngredientId = ingredient.Id;
        EditName = ingredient.Name;
        EditUnit = ingredient.Unit;
        EditCostPerUnit = ingredient.CostPerUnit;
        EditLowStockThreshold = ingredient.LowStockThreshold;

        IsEditingIngredient = true;
    }

    private void CancelEditIngredient()
    {
        IsEditingIngredient = false;
    }

    private async Task SaveEditIngredientAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            MessageBox.Show("Please enter an ingredient name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _inventoryService.UpdateIngredientAsync(
                EditIngredientId, EditName, EditUnit, EditCostPerUnit, EditLowStockThreshold
            );

            IsEditingIngredient = false;
            await RefreshAsync();
            MessageBox.Show("Ingredient updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteIngredientAsync(Ingredient? ingredient)
    {
        if (ingredient == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete ingredient '{ingredient.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _inventoryService.DeleteIngredientAsync(ingredient.Id);
                await RefreshAsync();
                MessageBox.Show($"Ingredient '{ingredient.Name}' deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot Delete Ingredient", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
