using System.Windows;
using ZemenServe.Kitchen.ViewModels;

namespace ZemenServe.Kitchen.Views;

public partial class MainWindow : Window
{
    public MainWindow(KitchenQueueViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
