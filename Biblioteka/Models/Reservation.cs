namespace Biblioteka.Models;

public class Reservation
{
    public int Id { get; set; }
    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryDate { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int BookCopyId { get; set; }
    public BookCopy BookCopy { get; set; } = null!;
}