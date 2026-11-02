
namespace OrderProcessing.Phase3;

public interface IEmailService
{
    Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct);
}
