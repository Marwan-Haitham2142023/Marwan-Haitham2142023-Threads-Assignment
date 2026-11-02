
namespace OrderProcessing.Phase3;

public class BrokenOrderService
{
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public BrokenOrderService(OrderRepository orderRepository, IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

#pragma warning disable CS4014
    public async Task ProcessOrderAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order, CancellationToken.None);
        _emailService.SendConfirmationAsync(order, customer, CancellationToken.None);
        Console.WriteLine("Order processing finished");
    }
#pragma warning restore CS4014
}
