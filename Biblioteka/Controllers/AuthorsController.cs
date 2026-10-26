using Biblioteka.Constants;
using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.Controllers
{
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Librarian}")]
    public class AuthorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Authors
        public async Task<IActionResult> Index()
        {
            var authors = await _context.Authors
                .Include(a => a.Books)
                .OrderBy(a => a.LastName)
                .ThenBy(a => a.FirstName)
                .ToListAsync();

            return View(authors);
        }

        // GET: Authors/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Authors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FirstName,LastName")] Author author)
        {
            if (ModelState.IsValid)
            {
                _context.Add(author);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Dodano autora: {author.FirstName} {author.LastName}.";
                return RedirectToAction(nameof(Index));
            }
            return View(author);
        }

        // GET: Authors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var author = await _context.Authors.FindAsync(id);
            if (author == null) return NotFound();

            return View(author);
        }

        // POST: Authors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName")] Author author)
        {
            if (id != author.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(author);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Zaktualizowano dane autora: {author.FirstName} {author.LastName}.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Authors.Any(e => e.Id == author.Id)) return NotFound();
                    throw;
                }
            }
            return View(author);
        }

        // GET: Authors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var author = await _context.Authors
                .Include(a => a.Books)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (author == null) return NotFound();

            return View(author);
        }

        // POST: Authors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var author = await _context.Authors
                .Include(a => a.Books)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (author != null)
            {
                if (author.Books != null && author.Books.Any())
                {
                    TempData["Error"] = $"Nie można usunąć autora {author.FirstName} {author.LastName}, ponieważ posiada przypisane książki w katalogu.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Authors.Remove(author);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Autor został usunięty z bazy.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}