
namespace OrderProcessing.Phase4;

public class CustomerDashboard
{
    public Customer Customer { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public List<PaymentTransaction> Payments { get; set; } = new();
    public List<EmailMessage> Emails { get; set; } = new();
}
