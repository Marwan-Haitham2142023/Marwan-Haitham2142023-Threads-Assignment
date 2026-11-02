namespace OrderProcessing.Phase4;

public class CairoExpressCarrier : ShippingCarrier
{
    public CairoExpressCarrier(IEnumerable<int>? failForOrderIds = null) : base(failForOrderIds)
    {
    }

    public override string Name => "CairoExpress";

    protected override int MinResponseMs => 300;

    protected override int MaxResponseMs => 2000;

    protected override decimal PriceFactor => 0.95m;

    protected override bool FailsByDefaultFor(int orderId) => orderId % 2 == 0;
}
