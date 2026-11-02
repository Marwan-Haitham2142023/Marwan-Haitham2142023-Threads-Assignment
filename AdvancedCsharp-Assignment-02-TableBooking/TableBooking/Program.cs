using TableBooking;

var booking = new FridayNightBooking();


async Task BookTable(string customerName)
{
    await booking.Book(customerName);
}

await Task.WhenAll(
    BookTable("Alice"),
    BookTable("Bob"),
    BookTable("Charlie"),
    BookTable("David"),
    BookTable("Eve"),
    BookTable("Frank"),
    BookTable("Grace")
);


Console.WriteLine();
Console.WriteLine($"Booked={booking.Booked}, TablesLeft={booking.TablesLeft}");
