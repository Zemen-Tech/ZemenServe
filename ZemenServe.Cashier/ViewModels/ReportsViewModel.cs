using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZemenServe.Cashier.Services;
using ZemenServe.Shared.DTOs;

namespace ZemenServe.Cashier.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly ReportService _reportService;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private DailyReportDto? _currentReport;

    [ObservableProperty]
    private string? _statusMessage;

    public ReportsViewModel(ReportService reportService)
    {
        _reportService = reportService;

        LoadReportCommand = new AsyncRelayCommand(LoadReportAsync);
        GeneratePdfCommand = new AsyncRelayCommand(GeneratePdfAsync);

        _ = LoadReportAsync();
    }

    public IAsyncRelayCommand LoadReportCommand { get; }
    public IAsyncRelayCommand GeneratePdfCommand { get; }

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = LoadReportAsync();
    }

    private async Task LoadReportAsync()
    {
        try
        {
            CurrentReport = await _reportService.GetDailyReportDataAsync(SelectedDate);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading report data: {ex.Message}";
        }
    }

    private async Task GeneratePdfAsync()
    {
        try
        {
            var desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = await _reportService.GenerateDailyPdfReportAsync(SelectedDate, desktopFolder);

            StatusMessage = $"PDF generated: {filePath}";

            var result = MessageBox.Show(
                $"Daily Report PDF generated successfully!\nFile location:\n{filePath}\n\nWould you like to open it now?",
                "Report Generated",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to generate PDF report: {ex.Message}", "PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
