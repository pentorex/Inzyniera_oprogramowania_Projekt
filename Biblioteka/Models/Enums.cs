using System.ComponentModel.DataAnnotations;

namespace Biblioteka.Models;

public enum BookStatus
{
    [Display(Name = "Dostępna")]
    Available,

    [Display(Name = "Zarezerwowana")]
    Reserved,

    [Display(Name = "Wypożyczona")]
    Borrowed,

    [Display(Name = "W konserwacji")]
    Maintenance,

    [Display(Name = "Zagubiona")]
    Lost
}

public enum BookCondition
{
    [Display(Name = "Idealna/Nowa")]
    New,

    [Display(Name = "Nosi ślady użytkowania")]
    Good,

    [Display(Name = "Uszkodzona")]
    Damaged,

    [Display(Name = "Zniszczona")]
    Destroyed
}

public enum ReservationStatus
{
    [Display(Name = "Oczekująca")]
    Pending,

    [Display(Name = "Zrealizowana")]
    Fulfilled,

    [Display(Name = "Anulowana")]
    Cancelled,

    [Display(Name = "Wygasła")]
    Expired
}