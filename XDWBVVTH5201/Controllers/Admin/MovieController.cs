using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using CinemaTest.Models.Admin;

namespace CinemaTest.Controllers.Admin
{
    public class MovieController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly string _posterFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/posters");

        public MovieController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Movie
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var movies = await _context.Movies.ToListAsync();
            return View("~/Views/Admin/Movie/Index.cshtml", movies);
        }

        // GET: Movie/Create
        public IActionResult Create()
        {
            return View("~/Views/Admin/Movie/Create.cshtml");
        }

        // POST: Movie/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Movie movie, IFormFile? posterFile)
        {
            ModelState.Remove("PosterUrl");

            if (posterFile != null && posterFile.Length > 0)
            {
                var extension = Path.GetExtension(posterFile.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("PosterUrl", "Chỉ cho phép ảnh JPG hoặc PNG.");
                    return View("~/Views/Admin/Movie/Create.cshtml", movie);
                }

                if (posterFile.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError("PosterUrl", "Kích thước ảnh tối đa là 2MB.");
                    return View("~/Views/Admin/Movie/Create.cshtml", movie);
                }

                var fileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(_posterFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await posterFile.CopyToAsync(stream);
                }

                movie.PosterUrl = fileName;
            }

            if (ModelState.IsValid)
            {
                _context.Add(movie);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Admin/Movie/Create.cshtml", movie);
        }

        // GET: Movie/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
                return NotFound();
            // Kiểm tra xem phim đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.MovieId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa phim này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Movie/Edit.cshtml", movie);
        }

        // POST: Movie/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Movie movie, IFormFile? posterFile)
        {
            if (id != movie.Id)
                return NotFound();

            // Kiểm tra xem phim đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.MovieId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa phim này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.Remove("PosterUrl");

            if (ModelState.IsValid)
            {
                try
                {
                    if (posterFile != null && posterFile.Length > 0)
                    {
                        var extension = Path.GetExtension(posterFile.FileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };

                        if (!allowedExtensions.Contains(extension))
                        {
                            ModelState.AddModelError("PosterUrl", "Chỉ cho phép ảnh JPG hoặc PNG.");
                            return View("~/Views/Admin/Movie/Edit.cshtml", movie);
                        }

                        if (posterFile.Length > 2 * 1024 * 1024)
                        {
                            ModelState.AddModelError("PosterUrl", "Kích thước ảnh tối đa là 2MB.");
                            return View("~/Views/Admin/Movie/Edit.cshtml", movie);
                        }

                        // Xóa ảnh cũ
                        var oldMovie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                        if (!string.IsNullOrEmpty(oldMovie?.PosterUrl))
                        {
                            var oldPath = Path.Combine(_posterFolder, oldMovie.PosterUrl);
                            if (System.IO.File.Exists(oldPath))
                            {
                                System.IO.File.Delete(oldPath);
                            }
                        }

                        var fileName = Guid.NewGuid().ToString() + extension;
                        var filePath = Path.Combine(_posterFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await posterFile.CopyToAsync(stream);
                        }

                        movie.PosterUrl = fileName;
                    }

                    _context.Update(movie);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Movies.Any(e => e.Id == id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Admin/Movie/Edit.cshtml", movie);
        }

        // GET: Movie/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
                return NotFound();
            // Kiểm tra xem phim đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.MovieId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể xóa phim này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Movie/Delete.cshtml", movie);
        }

        // POST: Movie/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Kiểm tra xem phim đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.MovieId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể xóa phim này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }

            var movie = await _context.Movies.FindAsync(id);
            if (movie != null)
            {
                // Xóa poster kèm theo
                if (!string.IsNullOrEmpty(movie.PosterUrl))
                {
                    var filePath = Path.Combine(_posterFolder, movie.PosterUrl);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
