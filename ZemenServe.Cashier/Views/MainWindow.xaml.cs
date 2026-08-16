using System.Windows;
using ZemenServe.Cashier.ViewModels;

namespace ZemenServe.Cashier.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
