using Biblioteka.Constants;
using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BooksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Books (Lista książek / Katalog z filtrowaniem dynamicznym)
        public async Task<IActionResult> Index(
            string? searchString,
            int? categoryId,
            int? authorId,
            int? yearFrom,
            int? yearTo)
        {
            var booksQuery = _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Include(b => b.Copies)
                .AsQueryable();

            // 1. Wyszukiwanie tekstowe (tytuł, imię/nazwisko autora, kategoria, ISBN)
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var query = searchString.Trim();
                booksQuery = booksQuery.Where(b =>
                    b.Title.Contains(query) ||
                    (b.Author != null && (
                        b.Author.FirstName.Contains(query) ||
                        b.Author.LastName.Contains(query) ||
                        (b.Author.FirstName + " " + b.Author.LastName).Contains(query)
                    )) ||
                    (b.Category != null && b.Category.Name.Contains(query)) ||
                    b.ISBN.Contains(query));
            }

            // 2. Filtr po kategorii
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                booksQuery = booksQuery.Where(b => b.CategoryId == categoryId.Value);
            }

            // 3. Filtr po autorze
            if (authorId.HasValue && authorId.Value > 0)
            {
                booksQuery = booksQuery.Where(b => b.AuthorId == authorId.Value);
            }

            // 4. Widełki czasowe (Rok wydania od - do)
            if (yearFrom.HasValue)
            {
                booksQuery = booksQuery.Where(b => b.PublicationYear >= yearFrom.Value);
            }

            if (yearTo.HasValue)
            {
                booksQuery = booksQuery.Where(b => b.PublicationYear <= yearTo.Value);
            }

            var books = await booksQuery.OrderBy(b => b.Title).ToListAsync();

            // Jeśli zapytanie przesyła nagłówek AJAX (live search), zwracamy tylko samą tabelę
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_BookListPartial", books);
            }

            // Zasilenie list rozwijanych dla pierwszego pełnego załadowania widoku
            var authors = await _context.Authors
                .Select(a => new { a.Id, FullName = a.FirstName + " " + a.LastName })
                .OrderBy(a => a.FullName)
                .ToListAsync();

            ViewBag.Categories = new SelectList(
                await _context.Categories.OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name",
                categoryId);

            ViewBag.Authors = new SelectList(authors, "Id", "FullName", authorId);

            // Przekazanie aktualnych stanów filtrów
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.CurrentAuthorId = authorId;
            ViewBag.CurrentYearFrom = yearFrom;
            ViewBag.CurrentYearTo = yearTo;

            return View(books);
        }

        // GET: Books/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Include(b => b.Copies)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // GET: Books/Create
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public IActionResult Create()
        {
            PopulateSelectLists();
            return View();
        }

        // POST: Books/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Create([Bind("Id,Title,ISBN,PublicationYear,Publisher,Description,CoverImageUrl,AuthorId,CategoryId")] Book book)
        {
            // Pomijamy walidację obiektów nawigacyjnych, aby ModelState nie odrzucał formularza
            ModelState.Remove("Author");
            ModelState.Remove("Category");
            ModelState.Remove("Copies");

            if (ModelState.IsValid)
            {
                _context.Add(book);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Pomyślnie dodano nową książkę!";
                return RedirectToAction(nameof(Index));
            }

            PopulateSelectLists(book.AuthorId, book.CategoryId);
            return View(book);
        }

        // GET: Books/Edit/5
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            PopulateSelectLists(book.AuthorId, book.CategoryId);
            return View(book);
        }

        // POST: Books/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,ISBN,PublicationYear,Publisher,Description,CoverImageUrl,AuthorId,CategoryId")] Book book)
        {
            if (id != book.Id)
            {
                return NotFound();
            }

            // Pomijamy walidację obiektów nawigacyjnych
            ModelState.Remove("Author");
            ModelState.Remove("Category");
            ModelState.Remove("Copies");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Pomyślnie zaktualizowano dane książki: \"{book.Title}\".";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookExists(book.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            PopulateSelectLists(book.AuthorId, book.CategoryId);
            return View(book);
        }

        // GET: Books/Delete/5
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // POST: Books/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book != null)
            {
                _context.Books.Remove(book);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Książka została usunięta z systemu.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Pomocnicza metoda do sprawdzania czy książka istnieje
        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }

        // Pomocnicza metoda do zasilania list rozwijanych autorów i kategorii
        private void PopulateSelectLists(object? selectedAuthor = null, object? selectedCategory = null)
        {
            var authors = _context.Authors
                .Select(a => new { a.Id, FullName = a.FirstName + " " + a.LastName })
                .OrderBy(a => a.FullName)
                .ToList();

            ViewBag.AuthorId = new SelectList(authors, "Id", "FullName", selectedAuthor);
            ViewBag.CategoryId = new SelectList(_context.Categories.OrderBy(c => c.Name), "Id", "Name", selectedCategory);
        }
    }
}