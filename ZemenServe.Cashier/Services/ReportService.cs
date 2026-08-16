using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZemenServe.Cashier.Data;
using ZemenServe.Shared.DTOs;

namespace ZemenServe.Cashier.Services;

public class ReportService
{
    private readonly Func<ZemenServeDbContext> _dbContextFactory;

    public ReportService(Func<ZemenServeDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        // Configure QuestPDF Community License
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<DailyReportDto> GetDailyReportDataAsync(DateTime date)
    {
        using var context = _dbContextFactory();

        var startDate = date.Date;
        var endDate = startDate.AddDays(1);

        var orders = await context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                    .ThenInclude(m => m!.Recipes)
                        .ThenInclude(r => r.Ingredient)
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt < endDate)
            .AsNoTracking()
            .ToListAsync();

        var report = new DailyReportDto
        {
            ReportDate = startDate,
            TotalOrdersCount = orders.Count,
            TotalRevenue = orders.Sum(o => o.TotalAmount)
        };

        var itemSalesMap = new Dictionary<int, DailyReportItemBreakdownDto>();
        decimal totalCogs = 0;

        foreach (var order in orders)
        {
            foreach (var item in order.OrderItems)
            {
                if (item.MenuItem == null) continue;

                // Calculate item COGS based on ingredients
                decimal unitCogs = 0;
                foreach (var recipe in item.MenuItem.Recipes)
                {
                    if (recipe.Ingredient != null)
                    {
                        unitCogs += recipe.Ingredient.CostPerUnit * (decimal)recipe.QuantityRequired;
                    }
                }

                decimal itemCogs = unitCogs * item.Quantity;
                totalCogs += itemCogs;

                if (!itemSalesMap.TryGetValue(item.MenuItemId, out var breakdown))
                {
                    breakdown = new DailyReportItemBreakdownDto
                    {
                        ItemName = item.MenuItem.Name,
                        Category = item.MenuItem.Category,
                        QuantitySold = 0,
                        UnitPrice = item.UnitPriceAtSale,
                        TotalRevenue = 0,
                        EstimatedCogs = 0
                    };
                    itemSalesMap[item.MenuItemId] = breakdown;
                }

                breakdown.QuantitySold += item.Quantity;
                breakdown.TotalRevenue += item.Quantity * item.UnitPriceAtSale;
                breakdown.EstimatedCogs += itemCogs;
            }
        }

        report.TotalCogs = totalCogs;
        report.ItemsSold = itemSalesMap.Values.OrderByDescending(x => x.TotalRevenue).ToList();

        return report;
    }

    public async Task<string> GenerateDailyPdfReportAsync(DateTime date, string targetFolder)
    {
        var reportData = await GetDailyReportDataAsync(date);

        var fileName = $"ZemenServe_Daily_Report_{date:yyyy-MM-dd}.pdf";
        var filePath = Path.Combine(targetFolder, fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Segoe UI"));

                page.Header()
                    .Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ZemenServe F&B Management System")
                               .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                            col.Item().Text("Zemen Tech — Daily Performance & Profit Report")
                               .FontSize(11).Italic().FontColor(Colors.Grey.Darken2);
                        });

                        row.ConstantItem(140).Column(col =>
                        {
                            col.Item().Text($"Date: {reportData.ReportDate:yyyy-MM-dd}").Bold();
                            col.Item().Text($"Generated: {DateTime.Now:HH:mm:ss}");
                        });
                    });

                page.Content()
                    .PaddingVertical(20)
                    .Column(col =>
                    {
                        // Summary KPI Cards
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Revenue").FontSize(10).FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"{reportData.TotalRevenue:N2} ETB").FontSize(16).Bold().FontColor(Colors.Green.Darken2);
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                            {
                                c.Item().Text("Cost of Goods Sold (COGS)").FontSize(10).FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"{reportData.TotalCogs:N2} ETB").FontSize(16).Bold().FontColor(Colors.Orange.Darken2);
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                            {
                                c.Item().Text("Net Profit").FontSize(10).FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"{reportData.NetProfit:N2} ETB").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                            });
                        });

                        col.Item().PaddingTop(15).Text($"Total Orders Processed: {reportData.TotalOrdersCount}").Bold();

                        // Detailed Table
                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Item
                                columns.RelativeColumn(2); // Category
                                columns.RelativeColumn(1); // Qty
                                columns.RelativeColumn(2); // Revenue
                                columns.RelativeColumn(2); // COGS
                                columns.RelativeColumn(2); // Profit
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Item Name").Bold();
                                header.Cell().Element(CellStyle).Text("Category").Bold();
                                header.Cell().Element(CellStyle).AlignRight().Text("Qty").Bold();
                                header.Cell().Element(CellStyle).AlignRight().Text("Revenue").Bold();
                                header.Cell().Element(CellStyle).AlignRight().Text("COGS").Bold();
                                header.Cell().Element(CellStyle).AlignRight().Text("Profit").Bold();

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.Bold())
                                                    .BorderBottom(1)
                                                    .BorderColor(Colors.Black)
                                                    .PaddingVertical(5);
                                }
                            });

                            foreach (var item in reportData.ItemsSold)
                            {
                                table.Cell().Element(RowStyle).Text(item.ItemName);
                                table.Cell().Element(RowStyle).Text(item.Category);
                                table.Cell().Element(RowStyle).AlignRight().Text(item.QuantitySold.ToString());
                                table.Cell().Element(RowStyle).AlignRight().Text($"{item.TotalRevenue:N2}");
                                table.Cell().Element(RowStyle).AlignRight().Text($"{item.EstimatedCogs:N2}");
                                table.Cell().Element(RowStyle).AlignRight().Text($"{item.GrossProfit:N2}");

                                static IContainer RowStyle(IContainer container)
                                {
                                    return container.BorderBottom(1)
                                                    .BorderColor(Colors.Grey.Lighten3)
                                                    .PaddingVertical(5);
                                }
                            }
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" | ZemenServe POS — Confidential Management Document");
                    });
            });
        }).GeneratePdf(filePath);

        return filePath;
    }
}
