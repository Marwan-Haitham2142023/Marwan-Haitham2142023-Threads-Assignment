using System.Collections.Concurrent;

namespace OrderProcessing.Phase3;

public class EmailService : IEmailService
{
    private readonly Dictionary<int, int> _failAttempts;
    private readonly ConcurrentDictionary<int, int> _attempts = new();
    private readonly JsonFileStore _store = new();

    public EmailService(Dictionary<int, int>? failAttempts = null)
    {
        _failAttempts = failAttempts ?? new Dictionary<int, int>();
    }

    public async Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct)
    {
        var attempt = _attempts.AddOrUpdate(order.Id, 1, (_, count) => count + 1);

        Log.Info($"EmailService -> Order #{order.Id}: rendering template (attempt {attempt})...");
        await Task.Delay(200, ct);

        Log.Info($"EmailService -> Order #{order.Id}: connecting to SMTP (attempt {attempt})...");
        await Task.Delay(300, ct);

        if (_failAttempts.TryGetValue(order.Id, out var failCount) && attempt <= failCount)
            throw new SmtpUnavailableException($"SMTP server unavailable (attempt {attempt})");

        Log.Info($"EmailService -> Order #{order.Id}: sending (attempt {attempt})...");
        await Task.Delay(500, ct);

        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmed",
            Body = $"Hello {customer.Name}, your order #{order.Id} is confirmed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = attempt,
            LastError = null
        };

        await _store.UpdateAsync<EmailMessage, bool>(DataFiles.Emails, emails =>
        {
            emails.Add(message);
            return true;
        }, ct);

        Log.Success($"EmailService -> Order #{order.Id}: email sent to {customer.Email} (attempt {attempt})");
    }
}
