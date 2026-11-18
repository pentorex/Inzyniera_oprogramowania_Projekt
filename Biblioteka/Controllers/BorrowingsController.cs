using Biblioteka.Constants;
using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.Controllers
{
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
    public class BorrowingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const decimal DailyFineRate = 0.50m;

        public BorrowingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Borrowings (Lista wypożyczeń z wyszukiwaniem i filtrowaniem AJAX)
        public async Task<IActionResult> Index(string? searchString, string? statusFilter)
        {
            var borrowingsQuery = _context.Borrowings
                .Include(b => b.BookCopy)
                    .ThenInclude(bc => bc!.Book)
                .Include(b => b.User)
                .AsQueryable();

            // 1. Wyszukiwanie tekstowe (tytuł, numer inwentarzowy lub e-mail czytelnika)
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var query = searchString.Trim().ToLower();
                borrowingsQuery = borrowingsQuery.Where(b =>
                    (b.BookCopy != null && b.BookCopy.Book != null && b.BookCopy.Book.Title.ToLower().Contains(query)) ||
                    (b.BookCopy != null && b.BookCopy.InventoryNumber.ToLower().Contains(query)) ||
                    (b.User != null && b.User.Email != null && b.User.Email.ToLower().Contains(query))
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

            var borrowings = await borrowingsQuery
                .OrderByDescending(b => b.BorrowDate)
                .ToListAsync();

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
            var availableCopies = await GetFilteredAvailableCopiesAsync(searchString, categoryId, authorId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_BookCopyListPartial", availableCopies);
            }

            ViewData["AvailableCopies"] = availableCopies;
            await PopulateSelectListsAsync(authorId, categoryId);

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
        public async Task<IActionResult> Create(
            int[]? selectedBookCopyIds,
            string userId,
            DateTime dueDate,
            string? searchString,
            int? categoryId,
            int? authorId)
        {
            if (selectedBookCopyIds == null || selectedBookCopyIds.Length == 0)
            {
                ModelState.AddModelError("BookCopyId", "Musisz zaznaczyć przynajmniej jeden egzemplarz książki.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                ModelState.AddModelError("UserId", "Wybierz czytelnika.");
            }
            else
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    ModelState.AddModelError("UserId", "Wybrany czytelnik nie istnieje.");
                }
                else if (!user.IsActive)
                {
                    ModelState.AddModelError("UserId", "Konto tego czytelnika jest nieaktywne.");
                }
            }

            if (dueDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("DueDate", "Termin zwrotu nie może być datą z przeszłości.");
            }

            var distinctCopyIds = selectedBookCopyIds?.Distinct().ToArray() ?? Array.Empty<int>();
            var copiesToBorrow = new List<BookCopy>();

            if (distinctCopyIds.Length > 0)
            {
                copiesToBorrow = await _context.BookCopies
                    .Where(bc => distinctCopyIds.Contains(bc.Id))
                    .ToListAsync();

                if (copiesToBorrow.Count != distinctCopyIds.Length || copiesToBorrow.Any(bc => bc.Status != BookStatus.Available))
                {
                    ModelState.AddModelError("BookCopyId", "Co najmniej jeden z wybranych egzemplarzy nie jest już dostępny.");
                }
            }

            if (ModelState.IsValid)
            {
                var now = DateTime.UtcNow;

                foreach (var bookCopy in copiesToBorrow)
                {
                    var borrowing = new Borrowing
                    {
                        BookCopyId = bookCopy.Id,
                        UserId = userId,
                        BorrowDate = now,
                        DueDate = dueDate,
                        ReturnDate = null,
                        FineAmount = 0m
                    };

                    bookCopy.Status = BookStatus.Borrowed;
                    _context.Borrowings.Add(borrowing);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Zarejestrowano wypożyczenia ({copiesToBorrow.Count} szt.).";
                return RedirectToAction(nameof(Index));
            }

            ViewData["AvailableCopies"] = await GetFilteredAvailableCopiesAsync(searchString, categoryId, authorId);
            await PopulateSelectListsAsync(authorId, categoryId, userId);

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.CurrentAuthorId = authorId;

            return View(new Borrowing
            {
                UserId = userId ?? string.Empty,
                DueDate = dueDate == default ? DateTime.UtcNow.AddDays(30) : dueDate
            });
        }

        // POST: Borrowings/ReturnBook/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnBook(int id)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.BookCopy)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (borrowing == null)
            {
                return NotFound();
            }

            if (borrowing.ReturnDate == null)
            {
                var now = DateTime.UtcNow;
                borrowing.ReturnDate = now;

                if (now.Date > borrowing.DueDate.Date)
                {
                    int overdueDays = (now.Date - borrowing.DueDate.Date).Days;
                    borrowing.FineAmount = overdueDays * DailyFineRate;
                }
                else
                {
                    borrowing.FineAmount = 0m;
                }

                if (borrowing.BookCopy != null)
                {
                    borrowing.BookCopy.Status = BookStatus.Available;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Książka została pomyślnie zwrócona. Naliczona kara: {borrowing.FineAmount:0.00} zł.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<BookCopy>> GetFilteredAvailableCopiesAsync(string? searchString, int? categoryId, int? authorId)
        {
            var copiesQuery = _context.BookCopies
                .Include(bc => bc.Book)
                    .ThenInclude(b => b!.Author)
                .Include(bc => bc.Book)
                    .ThenInclude(b => b!.Categories)
                .Where(bc => bc.Status == BookStatus.Available)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var query = searchString.Trim().ToLower();
                copiesQuery = copiesQuery.Where(bc =>
                    (bc.Book != null && bc.Book.Title.ToLower().Contains(query)) ||
                    (bc.Book != null && bc.Book.ISBN.Contains(query)) ||
                    bc.InventoryNumber.ToLower().Contains(query) ||
                    (bc.Book != null && bc.Book.Author != null && (
                        bc.Book.Author.FirstName.ToLower().Contains(query) ||
                        bc.Book.Author.LastName.ToLower().Contains(query) ||
                        (bc.Book.Author.FirstName + " " + bc.Book.Author.LastName).ToLower().Contains(query)
                    ))
                );
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                copiesQuery = copiesQuery.Where(bc => bc.Book != null && bc.Book.Categories.Any(c => c.Id == categoryId.Value));
            }

            if (authorId.HasValue && authorId.Value > 0)
            {
                copiesQuery = copiesQuery.Where(bc => bc.Book != null && bc.Book.AuthorId == authorId.Value);
            }

            return await copiesQuery
                .OrderBy(bc => bc.Book!.Title)
                .ThenBy(bc => bc.InventoryNumber)
                .ToListAsync();
        }

        private async Task PopulateSelectListsAsync(int? selectedAuthor = null, int? selectedCategory = null, string? selectedUser = null)
        {
            var authors = await _context.Authors
                .Select(a => new { a.Id, FullName = a.FirstName + " " + a.LastName })
                .OrderBy(a => a.FullName)
                .ToListAsync();

            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Email)
                .Select(u => new
                {
                    u.Id,
                    DisplayName = string.IsNullOrWhiteSpace(u.FirstName) && string.IsNullOrWhiteSpace(u.LastName)
                        ? u.Email
                        : $"{u.FirstName} {u.LastName} ({u.Email})"
                })
                .ToListAsync();

            ViewBag.AuthorId = new SelectList(authors, "Id", "FullName", selectedAuthor);
            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedCategory);
            ViewBag.UserId = new SelectList(users, "Id", "DisplayName", selectedUser);
        }
    }
}