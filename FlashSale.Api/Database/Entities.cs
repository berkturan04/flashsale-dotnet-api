namespace FlashSale.Api.Database;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public int TotalStock { get; set; }
    public int AvailableStock { get; set; }
    public decimal Price { get; set; }
    public DateTime StartsAtUtc { get; set; }

    // 7. Günde aynı biletin 2 kişiye satılmasını önleyecek PostgreSQL satır sürümü (xmin)
    public uint Version { get; set; }
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer"; // "Admin" veya "Customer"
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled
}
