using System.ComponentModel.DataAnnotations;

namespace Biblioteka.Models;

public class Book
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tytu³ ksi¹¿ki jest wymagany.")]
    [StringLength(200, ErrorMessage = "Tytu³ nie mo¿e byæ d³u¿szy ni¿ 200 znaków.")]
    [Display(Name = "Tytu³")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Numer ISBN jest wymagany.")]
    [RegularExpression(@"^(?=(?:\D*\d){10}(?:(?:\D*\d){3})?$)[\d-]+$", ErrorMessage = "WprowadŸ poprawny format ISBN (10 lub 13 cyfr).")]
    [Display(Name = "ISBN")]
    public string ISBN { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rok wydania jest wymagany.")]
    [Range(1000, 2100, ErrorMessage = "Podaj poprawny rok wydania.")]
    [Display(Name = "Rok wydania")]
    public int PublicationYear { get; set; }

    [Required(ErrorMessage = "Wydawca jest wymagany.")]
    [StringLength(100, ErrorMessage = "Nazwa wydawcy nie mo¿e przekraczaæ 100 znaków.")]
    [Display(Name = "Wydawca")]
    public string Publisher { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Opis nie mo¿e przekraczaæ 2000 znaków.")]
    [Display(Name = "Opis")]
    public string Description { get; set; } = string.Empty;

    [Url(ErrorMessage = "WprowadŸ poprawny adres URL do ok³adki.")]
    [Display(Name = "Adres URL ok³adki")]
    public string CoverImageUrl { get; set; } = string.Empty;

    // Klucze obce i relacje
    [Required(ErrorMessage = "Wybierz autora.")]
    [Display(Name = "Autor")]
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    [Required(ErrorMessage = "Wybierz kategoriê.")]
    [Display(Name = "Kategoria")]
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
}