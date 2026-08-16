using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZemenServe.Cashier.Services;

namespace ZemenServe.Cashier.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject _currentView;

    public OrderEntryViewModel OrderEntryVM { get; }
    public DigitalMenuViewModel DigitalMenuVM { get; }
    public InventoryViewModel InventoryVM { get; }
    public ReportsViewModel ReportsVM { get; }

    public MainViewModel(
        OrderEntryViewModel orderEntryVM,
        DigitalMenuViewModel digitalMenuVM,
        InventoryViewModel inventoryVM,
        ReportsViewModel reportsVM)
    {
        OrderEntryVM = orderEntryVM;
        DigitalMenuVM = digitalMenuVM;
        InventoryVM = inventoryVM;
        ReportsVM = reportsVM;

        _currentView = orderEntryVM;

        NavigateCommand = new RelayCommand<string>(Navigate);
    }

    public IRelayCommand<string> NavigateCommand { get; }

    private void Navigate(string? destination)
    {
        CurrentView = destination switch
        {
            "DigitalMenu" => DigitalMenuVM,
            "Inventory" => InventoryVM,
            "Reports" => ReportsVM,
            _ => OrderEntryVM
        };
    }
}
