using CommunityToolkit.Mvvm.ComponentModel;
using ZemenServe.Shared.Models;

namespace ZemenServe.Cashier.ViewModels;

public partial class RecipeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Ingredient _ingredient = null!;

    [ObservableProperty]
    private double _quantityRequired = 1.0;

    public decimal UnitCost => Ingredient?.CostPerUnit ?? 0;
    public string Unit => Ingredient?.Unit ?? "pcs";
    public decimal LineCost => (decimal)QuantityRequired * UnitCost;

    partial void OnQuantityRequiredChanged(double value)
    {
        OnPropertyChanged(nameof(LineCost));
    }
}
