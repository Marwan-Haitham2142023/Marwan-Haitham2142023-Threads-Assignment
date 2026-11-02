
namespace OrderProcessing.Shared.Storage;

public static class DataSeeder
{
    public static async Task ResetAsync(CancellationToken ct = default)
    {
        if (Directory.Exists(DataFiles.DataDirectory))
            Directory.Delete(DataFiles.DataDirectory, recursive: true);

        Directory.CreateDirectory(DataFiles.DataDirectory);
        Directory.CreateDirectory(DataFiles.InvoicesDirectory);
        Directory.CreateDirectory(DataFiles.ReportsDirectory);

        var store = new JsonFileStore();

        await SeedAsync(store, DataFiles.Customers, BuildCustomers(), ct);
        await SeedAsync(store, DataFiles.Products, BuildProducts(), ct);
        await SeedAsync(store, DataFiles.Coupons, BuildCoupons(), ct);
        await SeedAsync(store, DataFiles.IncomingOrders, BuildIncomingOrders(), ct);
        await SeedAsync(store, DataFiles.Orders, new List<Order>(), ct);
        await SeedAsync(store, DataFiles.Payments, new List<PaymentTransaction>(), ct);
        await SeedAsync(store, DataFiles.Emails, new List<EmailMessage>(), ct);
        await SeedAsync(store, DataFiles.EmailRetry, new List<EmailMessage>(), ct);
    }

    private static async Task SeedAsync<T>(JsonFileStore store, string fileName, List<T> items, CancellationToken ct)
    {
        await store.WriteAllAsync(fileName, items, ct);
        Console.WriteLine($"[Seeder] {fileName} -> {items.Count} records.");
    }

    private static Customer C(int id, string name, bool active, LoyaltyTier tier) => new()
    {
        Id = id,
        Name = name,
        Email = name.ToLowerInvariant().Replace(' ', '.') + "@example.com",
        IsActive = active,
        LoyaltyTier = tier
    };

    private static List<Customer> BuildCustomers() => new()
    {
        C(1,  "Ahmed Ali",      true,  LoyaltyTier.Gold),
        C(2,  "Sara Mohamed",   true,  LoyaltyTier.Silver),
        C(3,  "Omar Hassan",    true,  LoyaltyTier.Bronze),
        C(4,  "Mona Ibrahim",   false, LoyaltyTier.Silver),
        C(5,  "Youssef Adel",   true,  LoyaltyTier.Gold),
        C(6,  "Nour Khaled",    true,  LoyaltyTier.Bronze),
        C(7,  "Karim Samir",    true,  LoyaltyTier.Silver),
        C(8,  "Laila Mahmoud",  true,  LoyaltyTier.Gold),
        C(9,  "Hassan Tarek",   true,  LoyaltyTier.Bronze),
        C(10, "Dina Fathy",     true,  LoyaltyTier.Silver),
    };

    private static Product P(int id, string name, decimal price, int stock, decimal weight) => new()
    {
        Id = id, Name = name, Price = price, StockQuantity = stock, WeightKg = weight
    };

    private static List<Product> BuildProducts() => new()
    {
        P(1, "Laptop",              1200.00m, 10, 2.2m),
        P(2, "Smartphone",           800.00m, 15, 0.4m),
        P(3, "Wireless Mouse",        25.00m,  2, 0.1m),
        P(4, "Mechanical Keyboard",   90.00m,  1, 0.9m),
        P(5, "USB-C Hub",             45.00m, 30, 0.2m),
        P(6, "Headphones",           150.00m, 12, 0.3m),
        P(7, "27\" Monitor",         350.00m,  6, 5.0m),
        P(8, "Webcam",                70.00m, 20, 0.2m),
    };

    private static List<Coupon> BuildCoupons()
    {
        var today = DateTime.Today;
        return new()
        {
            new Coupon { Code = "SAVE10",    DiscountPercent = 10, ExpiryDate = today.AddDays(180) },
            new Coupon { Code = "WELCOME15", DiscountPercent = 15, ExpiryDate = today.AddDays(90) },
            new Coupon { Code = "FLASH20",   DiscountPercent = 20, ExpiryDate = today.AddDays(7) },
            new Coupon { Code = "OLD20",     DiscountPercent = 20, ExpiryDate = today.AddDays(-30) },
        };
    }

    private static Order O(int id, int customerId, string? coupon, params (int productId, int qty)[] items) => new()
    {
        Id = id,
        CustomerId = customerId,
        CouponCode = coupon,
        Items = items.Select(i => new OrderItem { ProductId = i.productId, Quantity = i.qty, UnitPrice = 0m }).ToList(),
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.Now,
        FailureReason = null
    };

    private static List<Order> BuildIncomingOrders() => new()
    {
        O(1,  1,  "SAVE10",    (1, 1), (3, 1)),
        O(2,  2,  null,        (2, 1)),
        O(3,  3,  "WELCOME15", (5, 2), (8, 1)),
        O(4,  4,  null,        (6, 1)),
        O(5,  5,  null,        (7, 1)),
        O(6,  6,  "FLASH20",   (5, 1)),
        O(7,  7,  null,        (6, 1), (8, 1)),
        O(8,  8,  "SAVE10",    (1, 1)),
        O(9,  9,  null,        (3, 5)),
        O(10, 10, null,        (4, 1)),
        O(11, 1,  null,        (4, 1)),
        O(12, 2,  "OLD20",     (2, 1)),
        O(13, 3,  null,        (6, 2)),
        O(14, 5,  "WELCOME15", (8, 2), (5, 1)),
        O(15, 99, null,        (2, 1)),
        O(16, 6,  null,        (7, 1), (5, 1)),
        O(17, 7,  "SAVE10",    (2, 1)),
        O(18, 8,  "FAKE50",    (6, 1)),
        O(19, 9,  null,        (8, 1)),
        O(20, 10, "FLASH20",   (1, 1), (6, 1)),
    };
}
