using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using CinemaTest.Models.Admin;
using CinemaTest.Models.User;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaTest.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Trang chủ - Hiển thị danh sách phim
        public async Task<IActionResult> Index()
        {
            ViewBag.Genres = await _context.Movies
                .Select(m => m.Genre)
                .Distinct()
                .ToListAsync();

            var movies = await _context.Movies.ToListAsync();
            return View("~/Views/Home/Index.cshtml", movies);
        }

        // Tìm kiếm phim theo từ khóa
        public async Task<IActionResult> Search(string keyword)
        {
            ViewBag.Genres = await _context.Movies
                .Select(m => m.Genre)
                .Distinct()
                .ToListAsync();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index");
            }

            var movies = await _context.Movies
                .Where(m => m.Title.ToLower().Contains(keyword.ToLower()))
                .ToListAsync();

            return View("Index", movies);
        }

        // API autocomplete
        [HttpGet("autocomplete")]
        public IActionResult Autocomplete(string term)
        {
            var titles = _context.Movies
                .Where(m => m.Title.ToLower().Contains(term.ToLower()))
                .Select(m => m.Title)
                .Take(5)
                .ToList();

            return Ok(titles);
        }

        // Lịch chiếu - có lọc theo phim và ngày
        public async Task<IActionResult> Showtimes(int? movieId, DateTime? date)
        {
            ViewBag.Genres = await _context.Movies
                .Select(m => m.Genre)
                .Distinct()
                .ToListAsync();

            ViewBag.Movies = await _context.Movies.ToListAsync();

            var query = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .AsQueryable();

            if (movieId.HasValue)
            {
                query = query.Where(s => s.MovieId == movieId.Value);
            }

            if (date.HasValue)
            {
                query = query.Where(s => s.StartTime.Date == date.Value.Date);
            }

            var showtimes = await query
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return View("~/Views/Home/Showtimes.cshtml", showtimes);
        }
        // Chi tiết phim
        public async Task<IActionResult> Details(int id)
        {
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            var showtimes = await _context.Showtimes
                .Include(s => s.Room)
                .Where(s => s.MovieId == id && s.StartTime >= DateTime.Now)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            var viewModel = new MovieDetailsViewModel
            {
                Movie = movie,
                Showtimes = showtimes
            };

            return View("~/Views/Home/Details.cshtml", viewModel);
        }
    }
}
