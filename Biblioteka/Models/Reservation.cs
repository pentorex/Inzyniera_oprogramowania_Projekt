using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Biblioteka.Models;

public class Reservation
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Data rezerwacji")]
    [DataType(DataType.DateTime)]
    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;

    [Required]
    [Display(Name = "Ważna do")]
    [DataType(DataType.DateTime)]
    public DateTime ExpiryDate { get; set; } = DateTime.UtcNow.AddDays(3);

    [Required]
    [Display(Name = "Status rezerwacji")]
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    [Required]
    [Display(Name = "Czytelnik")]
    public string UserId { get; set; } = string.Empty;

    [ValidateNever]
    public ApplicationUser User { get; set; } = null!;

    [Required(ErrorMessage = "Wybierz egzemplarz książki.")]
    [Display(Name = "Egzemplarz książki")]
    public int BookCopyId { get; set; }

    [ValidateNever]
    public BookCopy BookCopy { get; set; } = null!;

    [NotMapped]
    public bool IsExpired => Status == ReservationStatus.Pending && DateTime.UtcNow > ExpiryDate;

    [NotMapped]
    public bool IsActive => Status == ReservationStatus.Pending && DateTime.UtcNow <= ExpiryDate;
}