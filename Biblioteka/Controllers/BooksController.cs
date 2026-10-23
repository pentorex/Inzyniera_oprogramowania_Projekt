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

        // GET: Books (Lista książek / Katalog)
        public async Task<IActionResult> Index(string searchString)
        {
            var booksQuery = _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                booksQuery = booksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    b.Author.FirstName.Contains(searchString) ||
                    b.Author.LastName.Contains(searchString) ||
                    b.ISBN.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await booksQuery.ToListAsync());
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

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Dane książki zostały pomyślnie zaktualizowane!";
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
                return RedirectToAction(nameof(Index));
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

        // Pomocnicza metoda do dołączania autorów i kategorii do dropdownów
        private void PopulateSelectLists(object? selectedAuthor = null, object? selectedCategory = null)
        {
            var authors = _context.Authors
                .Select(a => new { a.Id, FullName = a.FirstName + " " + a.LastName })
                .ToList();

            ViewBag.AuthorId = new SelectList(authors, "Id", "FullName", selectedAuthor);
            ViewBag.CategoryId = new SelectList(_context.Categories, "Id", "Name", selectedCategory);
        }
    }
}