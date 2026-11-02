
namespace OrderProcessing.Phase3;

public class PaymentGateway : IPaymentGateway
{
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;
    private readonly JsonFileStore _store = new();

    public PaymentGateway(IEnumerable<int> timeoutOrderIds, IEnumerable<int> declineOrderIds)
    {
        _timeoutOrderIds = new HashSet<int>(timeoutOrderIds);
        _declineOrderIds = new HashSet<int>(declineOrderIds);
    }

    public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct)
    {
        Log.Info($"PaymentGateway -> Authorizing payment for Order #{orderId}...");
        await Task.Delay(800, ct);

        Log.Info($"PaymentGateway -> Fraud check for Order #{orderId}...");
        await Task.Delay(500, ct);

        if (_declineOrderIds.Contains(orderId))
        {
            await AppendAsync(new PaymentTransaction
            {
                OrderId = orderId,
                Amount = amount,
                Status = PaymentStatus.Declined,
                TransactionId = null,
                CreatedAt = DateTime.Now
            }, ct);

            Log.Error($"PaymentGateway -> Order #{orderId}: Card declined by issuer");
            return new PaymentResult
            {
                Success = false,
                TransactionId = null,
                Status = PaymentStatus.Declined,
                Message = "Card declined by issuer"
            };
        }

        Log.Info($"PaymentGateway -> Capturing payment for Order #{orderId}...");
        await Task.Delay(_timeoutOrderIds.Contains(orderId) ? 5000 : 700, ct);

        var transactionId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        await AppendAsync(new PaymentTransaction
        {
            OrderId = orderId,
            Amount = amount,
            Status = PaymentStatus.Captured,
            TransactionId = transactionId,
            CreatedAt = DateTime.Now
        }, ct);

        Log.Success($"PaymentGateway -> Order #{orderId}: captured {amount:F2} ({transactionId})");
        return new PaymentResult
        {
            Success = true,
            TransactionId = transactionId,
            Status = PaymentStatus.Captured,
            Message = null
        };
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct)
    {
        Log.Info($"PaymentGateway -> Refund {transactionId}: DNS/handshake...");
        await Task.Delay(150, ct);

        Log.Info($"PaymentGateway -> Refund {transactionId}: sending request...");
        await Task.Delay(300, ct);

        Log.Info($"PaymentGateway -> Refund {transactionId}: waiting for response...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        await _store.UpdateAsync<PaymentTransaction, bool>(DataFiles.Payments, payments =>
        {
            var payment = payments.FirstOrDefault(p => p.TransactionId == transactionId);
            if (payment is null)
                return false;

            payment.Status = PaymentStatus.Refunded;
            return true;
        }, ct);

        Log.Success($"PaymentGateway -> {transactionId} refunded");
    }

    private Task AppendAsync(PaymentTransaction payment, CancellationToken ct) =>
        _store.UpdateAsync<PaymentTransaction, bool>(DataFiles.Payments, payments =>
        {
            payments.Add(payment);
            return true;
        }, ct);
}
