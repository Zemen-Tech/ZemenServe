using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZemenServe.Cashier.Data;
using ZemenServe.Cashier.Services;
using ZemenServe.Cashier.ViewModels;
using ZemenServe.Cashier.Views;

namespace ZemenServe.Cashier;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global crash handling so errors pop up explicitly instead of silent crashes
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Startup Error: {ex?.Message}\n\nDetails:\n{ex?.StackTrace}", "ZemenServe Cashier Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        try
        {
            var services = new ServiceCollection();

            // Database context factory
            services.AddDbContext<ZemenServeDbContext>(options =>
            {
                var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zemenserve.db");
                options.UseSqlite($"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate;");
            });

            services.AddSingleton<Func<ZemenServeDbContext>>(sp => () => new ZemenServeDbContext());

            // Services
            services.AddSingleton<SignalRHostService>();
            services.AddSingleton<InventoryService>();
            services.AddSingleton<ReportService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<OrderEntryViewModel>();
            services.AddTransient<DigitalMenuViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            // Initialize Database & Seed
            using (var scope = ServiceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ZemenServeDbContext>();
                await SeedDataService.SeedAsync(dbContext);
            }

            // Start embedded SignalR Host
            var signalRHost = ServiceProvider.GetRequiredService<SignalRHostService>();
            await signalRHost.StartAsync(5000);

            // Show MainWindow after ServiceProvider and host are initialized
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize application: {ex.Message}\n\n{ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (ServiceProvider != null)
        {
            var signalRHost = ServiceProvider.GetService<SignalRHostService>();
            if (signalRHost != null)
            {
                await signalRHost.StopAsync();
            }
        }
        base.OnExit(e);
    }
}
