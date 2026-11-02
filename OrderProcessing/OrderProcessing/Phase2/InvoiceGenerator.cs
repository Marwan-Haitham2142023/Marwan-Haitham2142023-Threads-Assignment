using System.Text;

namespace OrderProcessing.Phase2;

public class InvoiceGenerator
{
    public async Task GenerateAsync(Order order, CancellationToken ct)
    {
        Log.Info($"InvoiceGenerator -> Rendering invoice for Order #{order.Id}...");

        var checksum = await Task.Run(() =>
        {
            double value = 0;
            for (int i = 1; i <= 20_000_000; i++)
            {
                value += Math.Sqrt(i);
                if (i % 1_000_000 == 0)
                    ct.ThrowIfCancellationRequested();
            }
            return value;
        }, ct);

        var text = new StringBuilder();
        text.AppendLine($"INVOICE - Order #{order.Id}");
        text.AppendLine($"Customer: #{order.CustomerId}");
        text.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine();
        foreach (var item in order.Items)
            text.AppendLine($"Product #{item.ProductId} x {item.Quantity} @ {item.UnitPrice:F2}");
        text.AppendLine();
        text.AppendLine($"SubTotal: {order.SubTotal:F2}");
        text.AppendLine($"Discount: {order.Discount:F2}");
        text.AppendLine($"Shipping: {order.ShippingCost:F2}");
        text.AppendLine($"Total:    {order.TotalAmount:F2}");
        text.AppendLine();
        text.AppendLine($"Render checksum: {checksum:F0}");

        Directory.CreateDirectory(DataFiles.InvoicesDirectory);
        var path = Path.Combine(DataFiles.InvoicesDirectory, $"invoice_{order.Id}.txt");
        await File.WriteAllTextAsync(path, text.ToString(), ct);

        Log.Success($"InvoiceGenerator -> Invoice for Order #{order.Id} written");
    }
}
