using System.Security.Claims;
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
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int ReservationValidityDays = 3;
        private const int DefaultLoanDays = 30;
        private const int MaxActiveReservationsPerUser = 5;

        public ReservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reservations/MyReservations (Panel rezerwacji zalogowanego czytelnika)
        public async Task<IActionResult> MyReservations()
        {
            await ExpireOutdatedReservationsAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var myReservations = await _context.Reservations
                .Include(r => r.BookCopy)
                    .ThenInclude(bc => bc.Book)
                        .ThenInclude(b => b!.Author)
                .Where(r => r.UserId == userId)
                .OrderBy(r => r.Status != ReservationStatus.Pending) // Najpierw oczekujące
                .ThenBy(r => r.ExpiryDate)
                .ToListAsync();

            return View(myReservations);
        }

        // GET: Reservations (Lista rezerwacji dla bibliotekarza i administratora)
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Index(string? searchString, string? statusFilter)
        {
            await ExpireOutdatedReservationsAsync();

            var reservationsQuery = _context.Reservations
                .Include(r => r.BookCopy)
                    .ThenInclude(bc => bc.Book)
                        .ThenInclude(b => b!.Author)
                .Include(r => r.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var query = searchString.Trim().ToLower();
                reservationsQuery = reservationsQuery.Where(r =>
                    (r.BookCopy.Book != null && r.BookCopy.Book.Title.ToLower().Contains(query)) ||
                    r.BookCopy.InventoryNumber.ToLower().Contains(query) ||
                    (r.User.Email != null && r.User.Email.ToLower().Contains(query))
                );
            }

            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<ReservationStatus>(statusFilter, true, out var parsedStatus))
            {
                reservationsQuery = reservationsQuery.Where(r => r.Status == parsedStatus);
            }

            var reservations = await reservationsQuery
                .OrderBy(r => r.Status != ReservationStatus.Pending)
                .ThenByDescending(r => r.ReservationDate)
                .ToListAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ReservationListPartial", reservations);
            }

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentStatus = statusFilter;

            return View(reservations);
        }

        // GET: Reservations/Create?bookId=5 lub Reservations/Create?bookCopyId=12
        public async Task<IActionResult> Create(int? bookId, int? bookCopyId)
        {
            await ExpireOutdatedReservationsAsync();

            int selectedCopyId = 0;

            if (bookCopyId.HasValue)
            {
                var copy = await _context.BookCopies
                    .FirstOrDefaultAsync(bc => bc.Id == bookCopyId.Value && bc.Status == BookStatus.Available);
                if (copy != null)
                {
                    selectedCopyId = copy.Id;
                }
            }
            else if (bookId.HasValue)
            {
                var firstAvailableCopy = await _context.BookCopies
                    .Where(bc => bc.BookId == bookId.Value && bc.Status == BookStatus.Available)
                    .OrderBy(bc => bc.InventoryNumber)
                    .FirstOrDefaultAsync();

                if (firstAvailableCopy != null)
                {
                    selectedCopyId = firstAvailableCopy.Id;
                }
                else
                {
                    TempData["ErrorMessage"] = "Brak dostępnych egzemplarzy tej książki do zarezerwowania.";
                    return RedirectToAction("Details", "Books", new { id = bookId.Value });
                }
            }

            await PopulateAvailableCopiesSelectListAsync(selectedCopyId, bookId);

            var reservation = new Reservation
            {
                BookCopyId = selectedCopyId,
                ReservationDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(ReservationValidityDays)
            };

            return View(reservation);
        }

        // POST: Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bookCopyId)
        {
            await ExpireOutdatedReservationsAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive)
            {
                TempData["ErrorMessage"] = "Twoje konto jest nieaktywne i nie może składać rezerwacji.";
                return RedirectToAction(nameof(MyReservations));
            }

            var activeUserReservationsCount = await _context.Reservations
                .CountAsync(r => r.UserId == userId && r.Status == ReservationStatus.Pending);

            if (activeUserReservationsCount >= MaxActiveReservationsPerUser)
            {
                ModelState.AddModelError("", $"Osiągnięto limit aktywnych rezerwacji ({MaxActiveReservationsPerUser}).");
            }

            var bookCopy = await _context.BookCopies
                .Include(bc => bc.Book)
                .FirstOrDefaultAsync(bc => bc.Id == bookCopyId);

            if (bookCopy == null)
            {
                ModelState.AddModelError("", "Wybrany egzemplarz książki nie istnieje.");
            }
            else if (bookCopy.Status != BookStatus.Available)
            {
                ModelState.AddModelError("", "Wybrany egzemplarz nie jest już dostępny do rezerwacji.");
            }
            else
            {
                bool alreadyReservedSameTitle = await _context.Reservations
                    .Include(r => r.BookCopy)
                    .AnyAsync(r => r.UserId == userId
                                && r.Status == ReservationStatus.Pending
                                && r.BookCopy.BookId == bookCopy.BookId);

                if (alreadyReservedSameTitle)
                {
                    ModelState.AddModelError("", "Masz już aktywną rezerwację na ten tytuł.");
                }
            }

            // Jeśli nie ma żadnych błędów biznesowych, zapisujemy rezerwację
            if (ViewData.ModelState.ErrorCount == 0 && bookCopy != null)
            {
                var now = DateTime.UtcNow;

                var newReservation = new Reservation
                {
                    BookCopyId = bookCopy.Id,
                    UserId = userId,
                    ReservationDate = now,
                    ExpiryDate = now.AddDays(ReservationValidityDays),
                    Status = ReservationStatus.Pending
                };

                bookCopy.Status = BookStatus.Reserved;

                _context.Reservations.Add(newReservation);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Zarezerwowano egzemplarz {bookCopy.InventoryNumber} ({bookCopy.Book?.Title}). Rezerwacja jest ważna do {newReservation.ExpiryDate.ToLocalTime():dd.MM.yyyy HH:mm}.";
                return RedirectToAction(nameof(MyReservations));
            }

            await PopulateAvailableCopiesSelectListAsync(bookCopyId);
            return View(new Reservation { BookCopyId = bookCopyId });
        }

        // POST: Reservations/Cancel/5 (Anulowanie rezerwacji przez czytelnika lub bibliotekarza)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.BookCopy)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isStaff = User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.Librarian);

            if (!isStaff && reservation.UserId != currentUserId)
            {
                return Forbid();
            }

            if (reservation.Status != ReservationStatus.Pending)
            {
                TempData["ErrorMessage"] = "Można anulować wyłącznie oczekującą rezerwację.";
            }
            else
            {
                reservation.Status = ReservationStatus.Cancelled;

                if (reservation.BookCopy != null && reservation.BookCopy.Status == BookStatus.Reserved)
                {
                    reservation.BookCopy.Status = BookStatus.Available;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Rezerwacja została anulowana, a egzemplarz wrócił do puli dostępnych.";
            }

            // Inteligentne przekierowanie: jeśli akcja wywołana była z "MyReservations", wróc tam
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("MyReservations"))
            {
                return RedirectToAction(nameof(MyReservations));
            }

            return isStaff ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(MyReservations));
        }

        // POST: Reservations/Fulfill/5 (Realizacja rezerwacji przez bibliotekarza -> utworzenie wypożyczenia)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
        public async Task<IActionResult> Fulfill(int id)
        {
            await ExpireOutdatedReservationsAsync();

            var reservation = await _context.Reservations
                .Include(r => r.BookCopy)
                    .ThenInclude(bc => bc.Book)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status != ReservationStatus.Pending)
            {
                TempData["ErrorMessage"] = "Ta rezerwacja nie jest już aktywna (mogła wygasnąć lub zostać anulowana).";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;

            reservation.Status = ReservationStatus.Fulfilled;
            reservation.BookCopy.Status = BookStatus.Borrowed;

            var borrowing = new Borrowing
            {
                BookCopyId = reservation.BookCopyId,
                UserId = reservation.UserId,
                BorrowDate = now,
                DueDate = now.AddDays(DefaultLoanDays),
                ReturnDate = null,
                FineAmount = 0m,
                Comments = $"Zrealizowano z rezerwacji #{reservation.Id}"
            };

            _context.Borrowings.Add(borrowing);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Zrealizowano rezerwację i wydano egzemplarz {reservation.BookCopy.InventoryNumber} ({reservation.BookCopy.Book?.Title}) czytelnikowi {reservation.User.Email}.";
            return RedirectToAction(nameof(Index));
        }

        // Automatyczne wygaszanie przeterminowanych rezerwacji i odblokowywanie egzemplarzy
        private async Task ExpireOutdatedReservationsAsync()
        {
            var now = DateTime.UtcNow;

            var expiredReservations = await _context.Reservations
                .Include(r => r.BookCopy)
                .Where(r => r.Status == ReservationStatus.Pending && r.ExpiryDate < now)
                .ToListAsync();

            if (expiredReservations.Count > 0)
            {
                foreach (var res in expiredReservations)
                {
                    res.Status = ReservationStatus.Expired;
                    if (res.BookCopy != null && res.BookCopy.Status == BookStatus.Reserved)
                    {
                        res.BookCopy.Status = BookStatus.Available;
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task PopulateAvailableCopiesSelectListAsync(int? selectedCopyId = null, int? filterBookId = null)
        {
            var query = _context.BookCopies
                .Include(bc => bc.Book)
                    .ThenInclude(b => b!.Author)
                .Where(bc => bc.Status == BookStatus.Available)
                .AsQueryable();

            if (filterBookId.HasValue)
            {
                query = query.Where(bc => bc.BookId == filterBookId.Value);
            }

            var copies = await query
                .OrderBy(bc => bc.Book!.Title)
                .ThenBy(bc => bc.InventoryNumber)
                .Select(bc => new
                {
                    bc.Id,
                    DisplayText = $"{bc.Book!.Title} — [{bc.InventoryNumber}]"
                })
                .ToListAsync();

            ViewBag.BookCopyId = new SelectList(copies, "Id", "DisplayText", selectedCopyId);
        }
    }
}