namespace OrderProcessing.Phase4;

public class ShippingQuote
{
    public string Carrier { get; set; } = "";
    public decimal Price { get; set; }
    public long ElapsedMs { get; set; }
    public List<string> CancelledCarriers { get; set; } = new();
}
