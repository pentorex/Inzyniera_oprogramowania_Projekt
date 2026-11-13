using Biblioteka.Constants;
using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.Controllers
{
    [Authorize]
    public class BorrowingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BorrowingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Index(bool onlyActive = false)
        {
            var query = _context.Borrowings
                .Include(b => b.User)
                .Include(b => b.BookCopy)
                    .ThenInclude(bc => bc!.Book)
                        .ThenInclude(book => book.Author)
                .AsQueryable();

            if (onlyActive)
            {
                query = query.Where(b => b.ReturnDate == null);
            }

            var borrowings = await query
                .OrderBy(b => b.ReturnDate != null) 
                .ThenByDescending(b => b.BorrowDate)
                .ToListAsync();

            ViewBag.OnlyActive = onlyActive;
            return View(borrowings);
        }

        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Create(int? bookCopyId)
        {
            await PopulateSelectListsAsync(selectedBookCopyId: bookCopyId);

            var model = new Borrowing
            {
                BorrowDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                BookCopyId = bookCopyId ?? 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Create([Bind("BookCopyId,UserId,BorrowDate,DueDate,Comments")] Borrowing borrowing)
        {
            ModelState.Remove("BookCopy");
            ModelState.Remove("User");

            if (borrowing.DueDate.Date < borrowing.BorrowDate.Date)
            {
                ModelState.AddModelError("DueDate", "Termin zwrotu nie może być wcześniejszy niż data wypożyczenia.");
            }

            var reader = await _context.Users.FindAsync(borrowing.UserId);
            if (reader == null)
            {
                ModelState.AddModelError("UserId", "Wybrany czytelnik nie istnieje.");
            }
            else if (!reader.IsActive)
            {
                ModelState.AddModelError("UserId", "Konto wybranego czytelnika jest zablokowane (nieaktywne).");
            }

            var bookCopy = await _context.BookCopies
                .Include(bc => bc.Book)
                .FirstOrDefaultAsync(bc => bc.Id == borrowing.BookCopyId);

            if (bookCopy == null)
            {
                ModelState.AddModelError("BookCopyId", "Wybrany egzemplarz nie istnieje w ewidencji.");
            }
            else if (bookCopy.Status != BookStatus.Available)
            {
                ModelState.AddModelError("BookCopyId", $"Egzemplarz {bookCopy.InventoryNumber} nie jest dostępny do wypożyczenia.");
            }

            if (ModelState.IsValid && bookCopy != null && reader != null)
            {
                borrowing.ReturnDate = null;
                borrowing.FineAmount = 0m;

                bookCopy.Status = BookStatus.Borrowed;

                _context.Borrowings.Add(borrowing);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Zarejestrowano wypożyczenie egzemplarza {bookCopy.InventoryNumber} ({bookCopy.Book.Title}) dla czytelnika {reader.FirstName} {reader.LastName}.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateSelectListsAsync(borrowing.UserId, borrowing.BookCopyId);
            return View(borrowing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Return(int id)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.BookCopy)
                    .ThenInclude(bc => bc!.Book)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (borrowing == null)
            {
                return NotFound();
            }

            if (borrowing.ReturnDate.HasValue)
            {
                TempData["ErrorMessage"] = "To wypożyczenie zostało już wcześniej zamknięte.";
                return RedirectToAction(nameof(Index));
            }

            borrowing.ReturnDate = DateTime.UtcNow;

            if (borrowing.BookCopy != null)
            {
                borrowing.BookCopy.Status = BookStatus.Available;
            }

            await _context.SaveChangesAsync();

            var inventoryNo = borrowing.BookCopy?.InventoryNumber ?? "nieznany";
            var title = borrowing.BookCopy?.Book?.Title ?? "książki";
            TempData["SuccessMessage"] = $"Zarejestrowano zwrot egzemplarza {inventoryNo} ({title}). Egzemplarz ponownie jest dostępny na półce.";

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateSelectListsAsync(string? selectedUserId = null, int? selectedBookCopyId = null)
        {
            var activeUsers = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Select(u => new
                {
                    u.Id,
                    DisplayName = $"{u.LastName} {u.FirstName} ({u.Email})"
                })
                .ToListAsync();

            var availableCopies = await _context.BookCopies
                .Include(bc => bc.Book)
                    .ThenInclude(b => b.Author)
                .Where(bc => bc.Status == BookStatus.Available)
                .OrderBy(bc => bc.Book.Title)
                .ThenBy(bc => bc.InventoryNumber)
                .Select(bc => new
                {
                    bc.Id,
                    DisplayText = $"{bc.Book.Title} — nr inw.: {bc.InventoryNumber}"
                })
                .ToListAsync();

            ViewBag.UserId = new SelectList(activeUsers, "Id", "DisplayName", selectedUserId);
            ViewBag.BookCopyId = new SelectList(availableCopies, "Id", "DisplayText", selectedBookCopyId);
        }
    }
}