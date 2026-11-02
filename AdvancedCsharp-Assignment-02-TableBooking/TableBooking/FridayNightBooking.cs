namespace TableBooking;

public sealed class FridayNightBooking
{
    private readonly SemaphoreSlim _Gate = new(1, 1);
    private int _tablesLeft = 5;
    private int _booked;

    public int TablesLeft => _tablesLeft;
    public int Booked => _booked;

    public async Task Book(string customerName)
    {
        await _Gate.WaitAsync();
        try
        {
            if (_tablesLeft <= 0)
                return;

            await Task.Delay(20);

            _tablesLeft--;
            _booked++;
            Console.WriteLine($"{customerName} booked a table. Left={_tablesLeft}");
        }
        finally
        {
            _Gate.Release();

        }
    }
}
