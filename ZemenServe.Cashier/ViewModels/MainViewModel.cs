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
    public SettingsViewModel SettingsVM { get; }

    public MainViewModel(
        OrderEntryViewModel orderEntryVM,
        DigitalMenuViewModel digitalMenuVM,
        InventoryViewModel inventoryVM,
        ReportsViewModel reportsVM,
        SettingsViewModel settingsVM)
    {
        OrderEntryVM = orderEntryVM;
        DigitalMenuVM = digitalMenuVM;
        InventoryVM = inventoryVM;
        ReportsVM = reportsVM;
        SettingsVM = settingsVM;

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
            "Settings" => SettingsVM,
            _ => OrderEntryVM
        };
    }
}
