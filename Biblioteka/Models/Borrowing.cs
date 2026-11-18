using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Biblioteka.Models;

public class Borrowing
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Wybierz egzemplarz książki.")]
    [Display(Name = "Egzemplarz książki")]
    public int BookCopyId { get; set; }

    [ValidateNever]
    public BookCopy? BookCopy { get; set; }

    [Required(ErrorMessage = "Wybierz czytelnika.")]
    [Display(Name = "Czytelnik")]
    public string UserId { get; set; } = string.Empty;

    [ValidateNever]
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Required(ErrorMessage = "Data wypożyczenia jest wymagana.")]
    [DataType(DataType.Date)]
    [Display(Name = "Data wypożyczenia")]
    public DateTime BorrowDate { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "Termin zwrotu jest wymagany.")]
    [DataType(DataType.Date)]
    [Display(Name = "Termin zwrotu")]
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);

    [DataType(DataType.Date)]
    [Display(Name = "Data faktycznego zwrotu")]
    public DateTime? ReturnDate { get; set; }

    [Display(Name = "Naliczona kara")]
    public decimal FineAmount { get; set; } = 0m;

    [StringLength(500, ErrorMessage = "Uwagi nie mogą przekraczać 500 znaków.")]
    [Display(Name = "Uwagi")]
    public string? Comments { get; set; }

    [NotMapped]
    public bool IsReturned => ReturnDate.HasValue;
}