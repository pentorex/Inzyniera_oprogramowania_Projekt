using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Biblioteka.Constants;
using Biblioteka.Data;
using Biblioteka.Models;

namespace Biblioteka.Controllers
{
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
    public class BorrowingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BorrowingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Borrowings (Lista wypożyczeń z obsługą wyszukiwania i filtrów)
        public async Task<IActionResult> Index(string? searchString, string? statusFilter)
        {
            var borrowingsQuery = _context.Borrowings
                .Include(b => b.BookCopy)
                    .ThenInclude(bc => bc.Book)
                .Include(b => b.User)
                .AsQueryable();

            // 1. Wyszukiwanie tekstowe (po tytule, numerze inwentarzowym lub e-mailu)
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var query = searchString.Trim().ToLower();
                borrowingsQuery = borrowingsQuery.Where(b =>
                    b.BookCopy.Book.Title.ToLower().Contains(query) ||
                    b.BookCopy.InventoryNumber.ToLower().Contains(query) ||
                    b.User.Email.ToLower().Contains(query)
                );
            }

            // 2. Filtrowanie po statusie
            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "active")
                {
                    borrowingsQuery = borrowingsQuery.Where(b => b.ReturnDate == null);
                }
                else if (statusFilter == "returned")
                {
                    borrowingsQuery = borrowingsQuery.Where(b => b.ReturnDate != null);
                }
                else if (statusFilter == "overdue")
                {
                    borrowingsQuery = borrowingsQuery.Where(b => b.ReturnDate == null && DateTime.UtcNow > b.DueDate);
                }
            }

            var borrowings = await borrowingsQuery.OrderByDescending(b => b.BorrowDate).ToListAsync();

            // Obsługa zapytań AJAX dla dynamicznego odświeżania tabeli
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_BorrowingListPartial", borrowings);
            }

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentStatus = statusFilter;

            return View(borrowings);
        }

        // GET: Borrowings/Create
        public async Task<IActionResult> Create(string? searchString, int? categoryId, int? authorId)
        {
            var copiesQuery = _context.BookCopies
                .Include(bc => bc.Book)
                    .ThenInclude(b => b.Author)
                .Include(bc => bc.Book)
                    .ThenInclude(b => b.Categories)
                .Where(bc => bc.Status == BookStatus.Available)
                .AsQueryable();

            // Wyszukiwanie tekstowe egzemplarzy (fraza, tytuł, autor, ISBN, numer inwentarzowy)
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var query = searchString.Trim().ToLower();
                copiesQuery = copiesQuery.Where(bc =>
                    bc.Book.Title.ToLower().Contains(query) ||
                    bc.Book.ISBN.Contains(query) ||
                    bc.InventoryNumber.ToLower().Contains(query) ||
                    (bc.Book.Author != null && (
                        bc.Book.Author.FirstName.ToLower().Contains(query) ||
                        bc.Book.Author.LastName.ToLower().Contains(query) ||
                        (bc.Book.Author.FirstName + " " + bc.Book.Author.LastName).ToLower().Contains(query)
                    ))
                );
            }

            // Filtr po kategorii
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                copiesQuery = copiesQuery.Where(bc => bc.Book.Categories.Any(c => c.Id == categoryId.Value));
            }

            // Filtr po autorze
            if (authorId.HasValue && authorId.Value > 0)
            {
                copiesQuery = copiesQuery.Where(bc => bc.Book.AuthorId == authorId.Value);
            }

            var availableCopies = await copiesQuery.OrderBy(bc => bc.Book.Title).ToListAsync();

            // Jeśli zapytanie pochodzi z AJAX, zwracamy tylko widok częściowy tabeli z egzemplarzami
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_BookCopyListPartial", availableCopies);
            }

            await PopulateSelectListsAsync(authorId, categoryId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email");

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.CurrentAuthorId = authorId;

            var borrowing = new Borrowing
            {
                BorrowDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30)
            };

            return View(borrowing);
        }

        // POST: Borrowings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int[] selectedBookCopyIds, string userId, DateTime dueDate, string? searchString, int? categoryId, int? authorId)
        {
            if (selectedBookCopyIds == null || selectedBookCopyIds.Length == 0)
            {
                ModelState.AddModelError("", "Musisz zaznaczyć przynajmniej jeden egzemplarz książki.");
            }

            if (string.IsNullOrEmpty(userId))
            {
                ModelState.AddModelError("UserId", "Wybierz czytelnika.");
            }

            if (ModelState.IsValid && selectedBookCopyIds != null)
            {
                foreach (var copyId in selectedBookCopyIds)
                {
                    var borrowing = new Borrowing
                    {
                        BookCopyId = copyId,
                        UserId = userId,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = dueDate
                    };

                    var bookCopy = await _context.BookCopies.FindAsync(copyId);
                    if (bookCopy != null)
                    {
                        bookCopy.Status = BookStatus.Borrowed;
                        _context.Update(bookCopy);
                    }

                    _context.Add(borrowing);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Wypożyczenia zostały pomyślnie zarejestrowane.";
                return RedirectToAction(nameof(Index));
            }

            // W przypadku błędu przywracamy widok z listą egzemplarzy i danymi
            var copiesQuery = _context.BookCopies
                .Include(bc => bc.Book)
                    .ThenInclude(b => b.Author)
                .Where(bc => bc.Status == BookStatus.Available)
                .AsQueryable();

            ViewData["AvailableCopies"] = await copiesQuery.ToListAsync();
            await PopulateSelectListsAsync(authorId, categoryId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", userId);

            return View();
        }

        // POST: Borrowings/Return/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> ReturnBook(int id)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.BookCopy)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (borrowing == null)
            {
                return NotFound();
            }

            // Jeśli książka nie była jeszcze zwrócona
            if (borrowing.ReturnDate == null)
            {
                borrowing.ReturnDate = DateTime.UtcNow;
                borrowing.FineAmount = borrowing.CalculateFine(); // Wyliczenie kary[cite: 5]

                // Zmiana statusu egzemplarza na Dostępny
                if (borrowing.BookCopy != null)
                {
                    borrowing.BookCopy.Status = BookStatus.Available;
                    _context.Update(borrowing.BookCopy);
                }

                _context.Update(borrowing);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Książka została pomyślnie zwrócona. Naliczona kara: {borrowing.FineAmount} zł.";
            }

            return RedirectToAction(nameof(Index));
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
            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedCategories);
        }
    }
}