namespace Biblioteka.Models;

public class BookCopy
{
    public int Id { get; set; }
    public string InventoryNumber { get; set; } = string.Empty;
    public BookStatus Status { get; set; } = BookStatus.Available;
    public DateTime AcquisitionDate { get; set; } = DateTime.UtcNow;
    public string ShelfLocation { get; set; } = string.Empty;

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public ICollection<Borrowing> Borrowings { get; set; } = new List<Borrowing>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}