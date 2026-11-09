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
                .Include(b => b.Categories)
                .Include(b => b.Copies)
                .AsQueryable();

            // 1. Wyszukiwanie tekstowe (tytuł, autor, kategoria, ISBN)
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
                    b.Categories.Any(c => c.Name.Contains(query)) ||
                    b.ISBN.Contains(query));
            }

            // 2. Filtr po kategorii
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                booksQuery = booksQuery.Where(b => b.Categories.Any(c => c.Id == categoryId.Value));
            }

            // 3. Filtr po autorze
            if (authorId.HasValue && authorId.Value > 0)
            {
                booksQuery = booksQuery.Where(b => b.AuthorId == authorId.Value);
            }

            // 4. Widełki czasowe
            if (yearFrom.HasValue)
            {
                booksQuery = booksQuery.Where(b => b.PublicationYear >= yearFrom.Value);
            }

            if (yearTo.HasValue)
            {
                booksQuery = booksQuery.Where(b => b.PublicationYear <= yearTo.Value);
            }

            var books = await booksQuery.OrderBy(b => b.Title).ToListAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_BookListPartial", books);
            }

            await PopulateSelectListsAsync(authorId, categoryId);

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
                .Include(b => b.Categories)
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
        public async Task<IActionResult> Create()
        {
            await PopulateSelectListsAsync();
            return View();
        }

        // POST: Books/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Create(
            [Bind("Id,Title,ISBN,PublicationYear,Publisher,Description,CoverImageUrl,AuthorId")] Book book,
            int[]? selectedCategoryIds)
        {
            ModelState.Remove("Author");
            ModelState.Remove("Categories");
            ModelState.Remove("Copies");

            if (string.IsNullOrWhiteSpace(book.CoverImageUrl))
            {
                ModelState.Remove("CoverImageUrl");
            }

            if (ModelState.IsValid)
            {
                if (selectedCategoryIds != null && selectedCategoryIds.Length > 0)
                {
                    book.Categories = await _context.Categories
                        .Where(c => selectedCategoryIds.Contains(c.Id))
                        .ToListAsync();
                }

                _context.Add(book);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Pomyślnie dodano nową książkę!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateSelectListsAsync(book.AuthorId, selectedCategoryIds);
            ViewBag.SelectedCategoryIds = selectedCategoryIds;
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

            var book = await _context.Books
                .Include(b => b.Categories)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            var selectedCategoryIds = book.Categories.Select(c => c.Id).ToArray();
            await PopulateSelectListsAsync(book.AuthorId, selectedCategoryIds);

            ViewBag.SelectedCategoryIds = selectedCategoryIds;
            return View(book);
        }

        // POST: Books/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Title,ISBN,PublicationYear,Publisher,Description,CoverImageUrl,AuthorId")] Book bookInput,
            int[]? selectedCategoryIds)
        {
            if (id != bookInput.Id)
            {
                return NotFound();
            }

            ModelState.Remove("Author");
            ModelState.Remove("Categories");
            ModelState.Remove("Copies");

            if (string.IsNullOrWhiteSpace(bookInput.CoverImageUrl))
            {
                ModelState.Remove("CoverImageUrl");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var bookToUpdate = await _context.Books
                        .Include(b => b.Categories)
                        .FirstOrDefaultAsync(b => b.Id == id);

                    if (bookToUpdate == null)
                    {
                        return NotFound();
                    }

                    bookToUpdate.Title = bookInput.Title;
                    bookToUpdate.ISBN = bookInput.ISBN;
                    bookToUpdate.PublicationYear = bookInput.PublicationYear;
                    bookToUpdate.Publisher = bookInput.Publisher;
                    bookToUpdate.Description = bookInput.Description ?? string.Empty;
                    bookToUpdate.CoverImageUrl = bookInput.CoverImageUrl ?? string.Empty;
                    bookToUpdate.AuthorId = bookInput.AuthorId;

                    // Czyszczenie i ponowne przypisanie wypróbowanych kategorii
                    bookToUpdate.Categories.Clear();
                    if (selectedCategoryIds != null && selectedCategoryIds.Length > 0)
                    {
                        var newCategories = await _context.Categories
                            .Where(c => selectedCategoryIds.Contains(c.Id))
                            .ToListAsync();

                        foreach (var cat in newCategories)
                        {
                            bookToUpdate.Categories.Add(cat);
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Pomyślnie zaktualizowano dane książki: \"{bookToUpdate.Title}\".";
                    return RedirectToAction(nameof(Details), new { id = bookToUpdate.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookExists(bookInput.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            await PopulateSelectListsAsync(bookInput.AuthorId, selectedCategoryIds);
            ViewBag.SelectedCategoryIds = selectedCategoryIds;
            return View(bookInput);
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
                .Include(b => b.Categories)
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
                TempData["SuccessMessage"] = "Książka została usunięta z systemu.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }

        private async Task PopulateSelectListsAsync(object? selectedAuthor = null, object? selectedCategories = null)
        {
            var authors = await _context.Authors
                .Select(a => new { a.Id, FullName = a.FirstName + " " + a.LastName })
                .OrderBy(a => a.FullName)
                .ToListAsync();

            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.AuthorId = new SelectList(authors, "Id", "FullName", selectedAuthor);
            ViewBag.Categories = new MultiSelectList(
                categories,
                "Id",
                "Name",
                selectedCategories as System.Collections.IEnumerable
            );
            ViewBag.CategoriesList = categories;
        }
    }
}