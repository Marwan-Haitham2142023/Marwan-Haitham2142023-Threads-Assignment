
namespace OrderProcessing.Phase1;

public class SyncCouponRepository
{
    public Coupon? GetByCode(string code)
    {
        var store = new JsonFileStore();

        Log.Info("SyncCouponRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncCouponRepository -> Executing query for Coupon '{code}'...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        var coupons = store.ReadAll<Coupon>(DataFiles.Coupons);
        var coupon = coupons.FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        if (coupon is null)
            Log.Info($"SyncCouponRepository -> Coupon '{code}' not found");
        else
            Log.Success($"SyncCouponRepository -> Coupon '{code}' found ({coupon.DiscountPercent}% off)");

        return coupon;
    }
}
