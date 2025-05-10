using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using CinemaTest.Models.Admin;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaTest.Controllers.Admin
{
    public class ShowtimesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShowtimesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Showtimes
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var showtimes = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .ToListAsync();

            return View("~/Views/Admin/Showtime/Index.cshtml", showtimes);
        }

        // GET: Showtimes/Create
        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View("~/Views/Admin/Showtime/Create.cshtml");
        }

        // POST: Showtimes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Showtime showtime)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();

            var movie = await _context.Movies.FindAsync(showtime.MovieId);
            if (movie == null)
            {
                ModelState.AddModelError("", "Phim không tồn tại.");
                return View("~/Views/Admin/Showtime/Create.cshtml", showtime);
            }

            // ✅ Thêm đoạn này để kiểm tra giờ bắt đầu
            if (showtime.StartTime < DateTime.Now)
            {
                ModelState.AddModelError("", "Giờ bắt đầu phải là thời gian trong tương lai.");
                return View("~/Views/Admin/Showtime/Create.cshtml", showtime);
            }

            // ✅ Tính giờ kết thúc theo thời lượng phim
            showtime.EndTime = showtime.StartTime.AddMinutes(movie.Duration);

            // ✅ Kiểm tra trùng giờ chiếu trong cùng phòng
            bool isOverlapping = await _context.Showtimes.AnyAsync(s =>
                s.RoomId == showtime.RoomId &&
                ((showtime.StartTime >= s.StartTime && showtime.StartTime < s.EndTime) ||
                 (showtime.EndTime > s.StartTime && showtime.EndTime <= s.EndTime) ||
                 (showtime.StartTime <= s.StartTime && showtime.EndTime >= s.EndTime))
            );

            if (isOverlapping)
            {
                ModelState.AddModelError("", "Lịch chiếu trùng với một suất chiếu khác trong phòng này.");
                return View("~/Views/Admin/Showtime/Create.cshtml", showtime);
            }

            if (ModelState.IsValid)
            {
                _context.Showtimes.Add(showtime);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Admin/Showtime/Create.cshtml", showtime);
        }

        // GET: Showtimes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
                return NotFound();
            // Kiểm tra xem lịch chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeID == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa lịch chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View("~/Views/Admin/Showtime/Edit.cshtml", showtime);
        }

        // POST: Showtimes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Showtime showtime)
        {
            if (id != showtime.Id)
                return NotFound();

            // Kiểm tra xem lịch chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeID == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa lịch chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();

            var movie = await _context.Movies.FindAsync(showtime.MovieId);
            if (movie != null)
            {
                showtime.EndTime = showtime.StartTime.AddMinutes(movie.Duration);
            }

            // ✅ Kiểm tra trùng giờ khi sửa (trừ chính nó)
            bool isOverlapping = await _context.Showtimes.AnyAsync(s =>
                s.RoomId == showtime.RoomId &&
                s.Id != showtime.Id &&
                ((showtime.StartTime >= s.StartTime && showtime.StartTime < s.EndTime) ||
                 (showtime.EndTime > s.StartTime && showtime.EndTime <= s.EndTime) ||
                 (showtime.StartTime <= s.StartTime && showtime.EndTime >= s.EndTime))
            );

            if (isOverlapping)
            {
                ModelState.AddModelError("", "Lịch chiếu trùng với một suất chiếu khác trong phòng này.");
                return View("~/Views/Admin/Showtime/Edit.cshtml", showtime);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(showtime);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Showtimes.Any(e => e.Id == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Admin/Showtime/Edit.cshtml", showtime);
        }

        // GET: Showtimes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (showtime == null)
                return NotFound();
            // Kiểm tra xem lịch chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeID == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể xóa lịch chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Showtime/Delete.cshtml", showtime);
        }

        // POST: Showtimes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Kiểm tra xem lịch chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeID == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể xóa lịch chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }

            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime != null)
            {
                _context.Showtimes.Remove(showtime);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
