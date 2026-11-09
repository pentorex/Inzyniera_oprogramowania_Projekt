using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Biblioteka.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nazwa kategorii jest wymagana.")]
    [StringLength(100, ErrorMessage = "Nazwa kategorii nie może przekraczać 100 znaków.")]
    [Display(Name = "Nazwa kategorii")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Opis nie może przekraczać 500 znaków.")]
    [Display(Name = "Opis")]
    public string Description { get; set; } = string.Empty;

    [ValidateNever]
    public ICollection<Book> Books { get; set; } = new List<Book>();
}