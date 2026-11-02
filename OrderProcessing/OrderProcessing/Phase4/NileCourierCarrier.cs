namespace OrderProcessing.Phase4;

public class NileCourierCarrier : ShippingCarrier
{
    public NileCourierCarrier(IEnumerable<int>? failForOrderIds = null) : base(failForOrderIds)
    {
    }

    public override string Name => "NileCourier";

    protected override int MinResponseMs => 800;

    protected override int MaxResponseMs => 1200;

    protected override decimal PriceFactor => 1.10m;
}
