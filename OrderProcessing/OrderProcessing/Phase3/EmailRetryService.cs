
namespace OrderProcessing.Phase3;

public class EmailRetryService
{
    private static readonly int[] RetryDelaysMs = { 500, 1000, 2000 };

    private readonly IEmailService _email;
    private readonly JsonFileStore _store = new();

    public EmailRetryService(IEmailService email)
    {
        _email = email;
    }

    public async Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct)
    {
        string lastError = "";

        for (int attempt = 1; attempt <= RetryDelaysMs.Length + 1; attempt++)
        {
            if (attempt > 1)
            {
                var wait = RetryDelaysMs[attempt - 2];
                Log.Info($"EmailRetry -> Order #{order.Id}: waiting {wait} ms before retry {attempt - 1}...");
                await Task.Delay(wait, ct);
            }

            try
            {
                await _email.SendConfirmationAsync(order, customer, ct);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex.Message;
                Log.Error($"EmailRetry -> Order #{order.Id}: attempt {attempt} failed: {ex.Message}");
            }
        }

        await QueueForLaterAsync(order, customer, RetryDelaysMs.Length + 1, lastError, ct);
        Log.Error($"Email for Order #{order.Id} queued for retry later");
    }

    private async Task QueueForLaterAsync(Order order, Customer customer, int attempts, string lastError, CancellationToken ct)
    {
        Log.Info("EmailRetry -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("EmailRetry -> Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info($"EmailRetry -> Saving Order #{order.Id} to the retry queue...");
        await Task.Delay(400, ct);

        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmed",
            Body = $"Hello {customer.Name}, your order #{order.Id} is confirmed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = attempts,
            LastError = lastError
        };

        await _store.UpdateAsync<EmailMessage, bool>(DataFiles.EmailRetry, queue =>
        {
            queue.Add(message);
            return true;
        }, ct);

        Log.Info("EmailRetry -> Committing transaction...");
        await Task.Delay(200, ct);
    }
}
