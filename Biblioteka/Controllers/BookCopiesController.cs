using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Biblioteka.Constants;
using Biblioteka.Data;
using Biblioteka.Models;

namespace Biblioteka.Controllers
{
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
    public class BookCopiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookCopiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: BookCopies/Create?bookId=5
        public async Task<IActionResult> Create(int? bookId)
        {
            if (bookId == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.Id == bookId);

            if (book == null)
            {
                return NotFound();
            }

            var copy = new BookCopy
            {
                BookId = book.Id,
                AcquisitionDate = DateTime.UtcNow,
                InventoryNumber = $"INV-{book.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}"
            };

            ViewData["BookTitle"] = $"{book.Title} ({book.Author?.FirstName} {book.Author?.LastName})";
            return View(copy);
        }

        // POST: BookCopies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BookId,InventoryNumber,Status,Condition,AcquisitionDate,Comments")] BookCopy bookCopy)
        {
            if (ModelState.IsValid)
            {
                _context.Add(bookCopy);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Egzemplarz o numerze {bookCopy.InventoryNumber} został pomyślnie zarejestrowany.";
                return RedirectToAction("Details", "Books", new { id = bookCopy.BookId });
            }

            var book = await _context.Books
                .Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.Id == bookCopy.BookId);

            ViewData["BookTitle"] = book != null ? $"{book.Title} ({book.Author?.FirstName} {book.Author?.LastName})" : "";
            return View(bookCopy);
        }

        // GET: BookCopies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bookCopy = await _context.BookCopies
                .Include(c => c.Book)
                .ThenInclude(b => b.Author)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (bookCopy == null)
            {
                return NotFound();
            }

            ViewData["BookTitle"] = $"{bookCopy.Book.Title} ({bookCopy.Book.Author?.FirstName} {bookCopy.Book.Author?.LastName})";
            return View(bookCopy);
        }

        // POST: BookCopies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BookId,InventoryNumber,Status,Condition,AcquisitionDate,Comments")] BookCopy bookCopy)
        {
            if (id != bookCopy.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bookCopy);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Zaktualizowano dane i status egzemplarza.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookCopyExists(bookCopy.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction("Details", "Books", new { id = bookCopy.BookId });
            }

            var book = await _context.Books
                .Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.Id == bookCopy.BookId);

            ViewData["BookTitle"] = book != null ? $"{book.Title} ({book.Author?.FirstName} {book.Author?.LastName})" : "";
            return View(bookCopy);
        }

        // POST: BookCopies/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var bookCopy = await _context.BookCopies.FindAsync(id);
            if (bookCopy == null)
            {
                return NotFound();
            }

            int bookId = bookCopy.BookId;
            _context.BookCopies.Remove(bookCopy);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Egzemplarz został usunięty z ewidencji.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        private bool BookCopyExists(int id)
        {
            return _context.BookCopies.Any(e => e.Id == id);
        }
    }
}