
namespace OrderProcessing.Phase3;

public class CustomerService
{
    private readonly CustomerRepository _customers;

    public CustomerService(CustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(id, ct);
        if (customer is null)
            throw new CustomerNotFoundException($"Customer #{id} not found");

        return customer;
    }
}
