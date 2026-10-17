namespace Biblioteka.Models;

public class Borrowing
{
	public int Id { get; set; }
	public DateTime BorrowDate { get; set; } = DateTime.UtcNow;
	public DateTime DueDate { get; set; }
	public DateTime? ReturnDate { get; set; }
	public decimal FineAmount { get; set; }
	public bool IsProlonged { get; set; } = false;

	public string UserId { get; set; } = string.Empty;
	public ApplicationUser User { get; set; } = null!;

	public int BookCopyId { get; set; }
	public BookCopy BookCopy { get; set; } = null!;

	public decimal CalculateFine()
	{
		var actualReturnDate = ReturnDate ?? DateTime.UtcNow;
		if (actualReturnDate > DueDate)
		{
			var delayDays = (actualReturnDate.Date - DueDate.Date).Days;
			return delayDays * 0.50m;
		}
		return 0.00m;
	}
}