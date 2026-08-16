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
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.ViewModels;

public partial class DigitalMenuViewModel : ObservableObject
{
    private readonly Func<ZemenServeDbContext> _dbContextFactory;
    private readonly InventoryService _inventoryService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isAddingNewItem;

    [ObservableProperty]
    private bool _isManagingCategories;

    // Editing Dish ID (null if creating new)
    private int? _editingMenuItemId;

    // Form fields for New/Edit Food Item & Recipe
    [ObservableProperty]
    private string _newDishName = string.Empty;

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private decimal _newSellingPrice;

    [ObservableProperty]
    private Ingredient? _selectedIngredientToAdd;

    [ObservableProperty]
    private double _newQuantityRequired = 1.0;

    [ObservableProperty]
    private decimal _totalCalculatedCogs;

    [ObservableProperty]
    private decimal _estimatedProfit;

    [ObservableProperty]
    private double _profitMarginPercentage;

    // Category Management Fields
    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private Category? _selectedCategoryToEdit;

    public ObservableCollection<MenuItem> MenuCatalog { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Ingredient> AvailableIngredients { get; } = new();
    public ObservableCollection<RecipeItemViewModel> RecipeItems { get; } = new();

    public DigitalMenuViewModel(Func<ZemenServeDbContext> dbContextFactory, InventoryService inventoryService)
    {
        _dbContextFactory = dbContextFactory;
        _inventoryService = inventoryService;

        LoadCatalogCommand = new AsyncRelayCommand(LoadCatalogAsync);
        ToggleActiveCommand = new AsyncRelayCommand<MenuItem>(ToggleActiveAsync);
        OpenAddFormCommand = new AsyncRelayCommand(OpenAddFormAsync);
        EditMenuItemCommand = new AsyncRelayCommand<MenuItem>(EditMenuItemAsync);
        DeleteMenuItemCommand = new AsyncRelayCommand<MenuItem>(DeleteMenuItemAsync);
        CancelAddFormCommand = new RelayCommand(CancelAddForm);
        AddRecipeLineCommand = new RelayCommand(AddRecipeLine);
        RemoveRecipeLineCommand = new RelayCommand<RecipeItemViewModel>(RemoveRecipeLine);
        SaveFoodItemCommand = new AsyncRelayCommand(SaveFoodItemAsync);

        // Category Commands
        OpenCategoryManagerCommand = new AsyncRelayCommand(OpenCategoryManagerAsync);
        CloseCategoryManagerCommand = new RelayCommand(() => IsManagingCategories = false);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        DeleteCategoryCommand = new AsyncRelayCommand<Category>(DeleteCategoryAsync);

        _ = LoadCatalogAsync();
    }

    public IAsyncRelayCommand LoadCatalogCommand { get; }
    public IAsyncRelayCommand<MenuItem> ToggleActiveCommand { get; }
    public IAsyncRelayCommand OpenAddFormCommand { get; }
    public IAsyncRelayCommand<MenuItem> EditMenuItemCommand { get; }
    public IAsyncRelayCommand<MenuItem> DeleteMenuItemCommand { get; }
    public IRelayCommand CancelAddFormCommand { get; }
    public IRelayCommand AddRecipeLineCommand { get; }
    public IRelayCommand<RecipeItemViewModel> RemoveRecipeLineCommand { get; }
    public IAsyncRelayCommand SaveFoodItemCommand { get; }

    public IAsyncRelayCommand OpenCategoryManagerCommand { get; }
    public IRelayCommand CloseCategoryManagerCommand { get; }
    public IAsyncRelayCommand AddCategoryCommand { get; }
    public IAsyncRelayCommand<Category> DeleteCategoryCommand { get; }

    private async Task LoadCatalogAsync()
    {
        using var context = _dbContextFactory();
        var items = await context.MenuItems
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredient)
            .AsNoTracking()
            .ToListAsync();

        MenuCatalog.Clear();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) ||
                item.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            {
                MenuCatalog.Add(item);
            }
        }

        var cats = await _inventoryService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadCatalogAsync();
    }

    partial void OnNewSellingPriceChanged(decimal value)
    {
        RecalculateFinancials();
    }

    private async Task OpenAddFormAsync()
    {
        _editingMenuItemId = null;
        await PrepareFormIngredientsAndCategoriesAsync();

        NewDishName = string.Empty;
        NewSellingPrice = 0;
        RecipeItems.Clear();
        RecalculateFinancials();

        IsAddingNewItem = true;
    }

    private async Task EditMenuItemAsync(MenuItem? item)
    {
        if (item == null) return;

        _editingMenuItemId = item.Id;
        await PrepareFormIngredientsAndCategoriesAsync();

        NewDishName = item.Name;
        SelectedCategory = Categories.FirstOrDefault(c => c.Name.Equals(item.Category, StringComparison.OrdinalIgnoreCase))
                           ?? Categories.FirstOrDefault();
        NewSellingPrice = item.Price;

        RecipeItems.Clear();
        foreach (var recipe in item.Recipes)
        {
            if (recipe.Ingredient != null)
            {
                RecipeItems.Add(new RecipeItemViewModel
                {
                    Ingredient = AvailableIngredients.FirstOrDefault(i => i.Id == recipe.IngredientId) ?? recipe.Ingredient,
                    QuantityRequired = recipe.QuantityRequired
                });
            }
        }

        RecalculateFinancials();
        IsAddingNewItem = true;
    }

    private async Task PrepareFormIngredientsAndCategoriesAsync()
    {
        var ingredients = await _inventoryService.GetIngredientsAsync();
        AvailableIngredients.Clear();
        foreach (var ing in ingredients) AvailableIngredients.Add(ing);

        if (AvailableIngredients.Any()) SelectedIngredientToAdd = AvailableIngredients.First();

        var cats = await _inventoryService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);

        if (Categories.Any()) SelectedCategory = Categories.First();
    }

    private void CancelAddForm()
    {
        IsAddingNewItem = false;
    }

    private void AddRecipeLine()
    {
        if (SelectedIngredientToAdd == null || NewQuantityRequired <= 0) return;

        var existing = RecipeItems.FirstOrDefault(r => r.Ingredient.Id == SelectedIngredientToAdd.Id);
        if (existing != null)
        {
            existing.QuantityRequired += NewQuantityRequired;
        }
        else
        {
            var line = new RecipeItemViewModel
            {
                Ingredient = SelectedIngredientToAdd,
                QuantityRequired = NewQuantityRequired
            };
            RecipeItems.Add(line);
        }

        RecalculateFinancials();
    }

    private void RemoveRecipeLine(RecipeItemViewModel? line)
    {
        if (line == null) return;
        RecipeItems.Remove(line);
        RecalculateFinancials();
    }

    private void RecalculateFinancials()
    {
        TotalCalculatedCogs = RecipeItems.Sum(item => item.LineCost);
        EstimatedProfit = NewSellingPrice - TotalCalculatedCogs;
        ProfitMarginPercentage = NewSellingPrice > 0 ? (double)(EstimatedProfit / NewSellingPrice) * 100 : 0;
    }

    private async Task SaveFoodItemAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDishName))
        {
            MessageBox.Show("Please enter a food dish name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedCategory == null)
        {
            MessageBox.Show("Please select a food category.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (NewSellingPrice <= 0)
        {
            MessageBox.Show("Please enter a valid selling price.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var recipeData = RecipeItems.Select(r => (r.Ingredient.Id, r.QuantityRequired)).ToList();

            await _inventoryService.SaveMenuItemWithRecipesAsync(
                _editingMenuItemId, NewDishName, SelectedCategory.Name, NewSellingPrice, recipeData
            );

            IsAddingNewItem = false;
            await LoadCatalogAsync();

            MessageBox.Show(
                $"Food item '{NewDishName}' saved successfully!\nCalculated Recipe COGS: {TotalCalculatedCogs:N2} ETB\nSelling Price: {NewSellingPrice:N2} ETB\nProfit: {EstimatedProfit:N2} ETB ({ProfitMarginPercentage:F1}%)",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save menu item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteMenuItemAsync(MenuItem? item)
    {
        if (item == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete food item '{item.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _inventoryService.DeleteMenuItemAsync(item.Id);
                await LoadCatalogAsync();
                MessageBox.Show($"Food item '{item.Name}' deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // --- Category Management Commands ---
    private async Task OpenCategoryManagerAsync()
    {
        var cats = await _inventoryService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
        NewCategoryName = string.Empty;
        IsManagingCategories = true;
    }

    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName)) return;

        try
        {
            await _inventoryService.AddCategoryAsync(NewCategoryName.Trim());
            NewCategoryName = string.Empty;
            var cats = await _inventoryService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to add category: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteCategoryAsync(Category? cat)
    {
        if (cat == null) return;

        try
        {
            await _inventoryService.DeleteCategoryAsync(cat.Id);
            var cats = await _inventoryService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete category: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ToggleActiveAsync(MenuItem? item)
    {
        if (item == null) return;

        using var context = _dbContextFactory();
        var entity = await context.MenuItems.FindAsync(item.Id);
        if (entity != null)
        {
            entity.IsActive = !entity.IsActive;
            await context.SaveChangesAsync();
            await LoadCatalogAsync();
        }
    }
}
