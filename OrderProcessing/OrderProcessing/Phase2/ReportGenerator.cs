using System.Text.Json;

namespace OrderProcessing.Phase2;

public class ReportGenerator
{
    private readonly OrderRepository _orders;
    private readonly ProductRepository _products;

    public ReportGenerator(OrderRepository orders, ProductRepository products)
    {
        _orders = orders;
        _products = products;
    }

    public async Task<Report> GenerateAsync(CancellationToken ct)
    {
        var orders = await _orders.GetAllAsync(ct);
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();

        var top = completed
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ProductId)
            .FirstOrDefault();

        var topName = "";
        var topQuantity = 0;
        if (top is not null)
        {
            var product = await _products.GetByIdAsync(top.ProductId, ct);
            topName = product?.Name ?? $"Product #{top.ProductId}";
            topQuantity = top.Quantity;
        }

        var revenue = PricingCalculator.Round(completed.Sum(o => o.TotalAmount));
        var average = completed.Count == 0
            ? 0m
            : PricingCalculator.Round(revenue / completed.Count);

        var report = new Report
        {
            TotalOrders = orders.Count,
            TotalRevenue = revenue,
            CompletedCount = completed.Count,
            RejectedCount = orders.Count(o => o.Status == OrderStatus.Rejected),
            FailedCount = orders.Count(o => o.Status == OrderStatus.Failed),
            PaymentFailedCount = orders.Count(o => o.Status == OrderStatus.PaymentFailed),
            TopSellingProduct = topName,
            TopSellingQuantity = topQuantity,
            AverageOrderValue = average,
            GeneratedAt = DateTime.Now
        };

        Directory.CreateDirectory(DataFiles.ReportsDirectory);
        var path = Path.Combine(DataFiles.ReportsDirectory, $"report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json");
        var json = JsonSerializer.Serialize(report, JsonConfig.Options);
        await File.WriteAllTextAsync(path, json, ct);

        Log.Success($"ReportGenerator -> Report saved to {Path.GetFileName(path)}");
        return report;
    }
}
