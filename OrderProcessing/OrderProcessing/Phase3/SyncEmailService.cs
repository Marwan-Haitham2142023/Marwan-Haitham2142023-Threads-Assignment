
namespace OrderProcessing.Phase3;

public class SyncEmailService
{
    private readonly JsonFileStore _store = new();

    public void SendConfirmation(Order order, Customer customer)
    {
        Log.Info($"SyncEmailService -> Order #{order.Id}: rendering template...");
        Thread.Sleep(200);

        Log.Info($"SyncEmailService -> Order #{order.Id}: connecting to SMTP...");
        Thread.Sleep(300);

        Log.Info($"SyncEmailService -> Order #{order.Id}: sending...");
        Thread.Sleep(500);

        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmed",
            Body = $"Hello {customer.Name}, your order #{order.Id} is confirmed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = 1,
            LastError = null
        };

        _store.Update<EmailMessage, bool>(DataFiles.Emails, emails =>
        {
            emails.Add(message);
            return true;
        });

        Log.Success($"SyncEmailService -> Order #{order.Id}: email sent to {customer.Email}");
    }
}
