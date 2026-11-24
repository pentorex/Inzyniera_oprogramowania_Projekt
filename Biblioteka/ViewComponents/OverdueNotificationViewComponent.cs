using System.Security.Claims;
using Biblioteka.Constants;
using Biblioteka.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.ViewComponents
{
    public class OverdueNotificationViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private const decimal DailyFineRate = 0.50m;

        public OverdueNotificationViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            var claimsPrincipal = User as ClaimsPrincipal;
            var userId = claimsPrincipal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Content(string.Empty);
            }

            var now = DateTime.UtcNow;
            var warningThreshold = now.Date.AddDays(3); // Ostrzeżenie na 3 dni przed terminem

            // 1. Powiadomienia osobiste czytelnika (przetrzymane + zbliżający się termin)
            var activeUserBorrowings = await _context.Borrowings
                .Include(b => b.BookCopy)
                    .ThenInclude(bc => bc!.Book)
                .Where(b => b.UserId == userId && b.ReturnDate == null)
                .ToListAsync();

            var overdueBooks = activeUserBorrowings
                .Where(b => b.DueDate.Date < now.Date)
                .ToList();

            var dueSoonBooks = activeUserBorrowings
                .Where(b => b.DueDate.Date >= now.Date && b.DueDate.Date <= warningThreshold)
                .ToList();

            var model = new OverdueNotificationViewModel
            {
                OverdueCount = overdueBooks.Count,
                TotalFine = overdueBooks.Sum(b => b.CalculateFine(DailyFineRate)),
                OverdueTitles = overdueBooks
                    .Select(b => b.BookCopy?.Book?.Title ?? "Nieznany tytuł")
                    .ToList(),
                DueSoonCount = dueSoonBooks.Count,
                DueSoonTitles = dueSoonBooks
                    .Select(b => $"{b.BookCopy?.Book?.Title ?? "Książka"} (do {b.DueDate:dd.MM})")
                    .ToList()
            };

            // 2. Dodatkowa informacja dla Administratora i Bibliotekarza o zaległościach w całej bibliotece
            if (User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.Librarian))
            {
                model.IsStaff = true;
                model.GlobalOverdueCount = await _context.Borrowings
                    .CountAsync(b => b.ReturnDate == null && b.DueDate < now);
            }

            // Jeśli brak jakichkolwiek powiadomień, nie renderuj pustego paska
            if (model.OverdueCount == 0 && model.DueSoonCount == 0 && (!model.IsStaff || model.GlobalOverdueCount == 0))
            {
                return Content(string.Empty);
            }

            return View(model);
        }
    }

    public class OverdueNotificationViewModel
    {
        public int OverdueCount { get; set; }
        public decimal TotalFine { get; set; }
        public List<string> OverdueTitles { get; set; } = new();

        public int DueSoonCount { get; set; }
        public List<string> DueSoonTitles { get; set; } = new();

        public bool IsStaff { get; set; }
        public int GlobalOverdueCount { get; set; }
    }
}