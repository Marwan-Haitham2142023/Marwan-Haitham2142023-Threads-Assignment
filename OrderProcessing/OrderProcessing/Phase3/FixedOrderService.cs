
namespace OrderProcessing.Phase3;

public class FixedOrderService
{
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public FixedOrderService(OrderRepository orderRepository, IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    public async Task ProcessOrderAsync(Order order, Customer customer, CancellationToken ct)
    {
        await _orderRepository.SaveAsync(order, ct);

        try
        {
            await _emailService.SendConfirmationAsync(order, customer, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error($"FixedOrderService -> Email for Order #{order.Id} failed: {ex.Message}");
        }

        Console.WriteLine("Order processing finished");
    }
}
