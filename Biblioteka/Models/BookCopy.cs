using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Biblioteka.Models;

public class BookCopy
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Numer inwentarzowy jest wymagany.")]
    [StringLength(50, ErrorMessage = "Numer inwentarzowy nie może przekraczać 50 znaków.")]
    [Display(Name = "Numer inwentarzowy")]
    public string InventoryNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Status egzemplarza")]
    public BookStatus Status { get; set; } = BookStatus.Available;

    [Required]
    [Display(Name = "Stan fizyczny")]
    public BookCondition Condition { get; set; } = BookCondition.New;

    [Display(Name = "Data przyjęcia")]
    [DataType(DataType.Date)]
    public DateTime AcquisitionDate { get; set; } = DateTime.UtcNow;

    // Pole opcjonalne (może być null):
    [StringLength(500, ErrorMessage = "Uwagi nie mogą przekraczać 500 znaków.")]
    [Display(Name = "Uwagi / Opis stanu")]
    public string? Comments { get; set; }

    // Klucz obcy
    [Required]
    [Display(Name = "Książka")]
    public int BookId { get; set; }

    // Wykluczenie relacji z walidacji formularza:
    [ValidateNever]
    public Book Book { get; set; } = null!;

    [ValidateNever]
    public ICollection<Borrowing> Borrowings { get; set; } = new List<Borrowing>();

    [ValidateNever]
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}