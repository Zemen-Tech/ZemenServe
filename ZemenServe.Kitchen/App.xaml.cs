using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ZemenServe.Kitchen.Services;
using ZemenServe.Kitchen.ViewModels;
using ZemenServe.Kitchen.Views;

namespace ZemenServe.Kitchen;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Startup Error: {ex?.Message}\n\nDetails:\n{ex?.StackTrace}", "ZemenServe Kitchen Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<KitchenSignalRClient>();
            services.AddSingleton<KitchenQueueViewModel>();
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize Kitchen app: {ex.Message}\n\n{ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
