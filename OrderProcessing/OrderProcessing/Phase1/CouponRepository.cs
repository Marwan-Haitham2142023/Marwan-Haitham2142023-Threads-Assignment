
namespace OrderProcessing.Phase1;

public class CouponRepository
{
    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("CouponRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"CouponRepository -> Executing query for Coupon '{code}'...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var coupons = await store.ReadAllAsync<Coupon>(DataFiles.Coupons, ct);
        var coupon = coupons.FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        if (coupon is null)
            Log.Info($"CouponRepository -> Coupon '{code}' not found");
        else
            Log.Success($"CouponRepository -> Coupon '{code}' found ({coupon.DiscountPercent}% off)");

        return coupon;
    }
}
