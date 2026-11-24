using System.Diagnostics;
using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalBooksCount = await _context.Books.CountAsync();
            ViewBag.AvailableCopiesCount = await _context.BookCopies
                .CountAsync(bc => bc.Status == BookStatus.Available);
            ViewBag.CategoriesCount = await _context.Categories.CountAsync();
            ViewBag.AuthorsCount = await _context.Authors.CountAsync();

            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.Name)
                .Take(6)
                .ToListAsync();

            var latestBooks = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Categories)
                .Include(b => b.Copies)
                .OrderByDescending(b => b.Id)
                .Take(4)
                .ToListAsync();

            return View(latestBooks);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}