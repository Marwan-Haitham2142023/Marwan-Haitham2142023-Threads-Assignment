namespace OrderProcessing.Phase4;

public class FastShipCarrier : ShippingCarrier
{
    public FastShipCarrier(IEnumerable<int>? failForOrderIds = null) : base(failForOrderIds)
    {
    }

    public override string Name => "FastShip";

    protected override int MinResponseMs => 500;

    protected override int MaxResponseMs => 1500;

    protected override decimal PriceFactor => 1.00m;
}
