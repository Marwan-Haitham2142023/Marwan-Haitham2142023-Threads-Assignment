
namespace OrderProcessing.Phase3;

public class SyncPaymentGateway
{
    private readonly JsonFileStore _store = new();

    public PaymentResult ProcessPayment(int orderId, decimal amount)
    {
        Log.Info($"SyncPaymentGateway -> Authorizing payment for Order #{orderId}...");
        Thread.Sleep(800);

        Log.Info($"SyncPaymentGateway -> Fraud check for Order #{orderId}...");
        Thread.Sleep(500);

        Log.Info($"SyncPaymentGateway -> Capturing payment for Order #{orderId}...");
        Thread.Sleep(700);

        var transactionId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        _store.Update<PaymentTransaction, bool>(DataFiles.Payments, payments =>
        {
            payments.Add(new PaymentTransaction
            {
                OrderId = orderId,
                Amount = amount,
                Status = PaymentStatus.Captured,
                TransactionId = transactionId,
                CreatedAt = DateTime.Now
            });
            return true;
        });

        Log.Success($"SyncPaymentGateway -> Order #{orderId}: captured {amount:F2} ({transactionId})");
        return new PaymentResult
        {
            Success = true,
            TransactionId = transactionId,
            Status = PaymentStatus.Captured,
            Message = null
        };
    }
}
