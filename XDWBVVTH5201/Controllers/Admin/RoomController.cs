using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using CinemaTest.Models.Admin;

namespace CinemaTest.Controllers.Admin
{
    public class RoomController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var rooms = await _context.Rooms.ToListAsync();
            return View("~/Views/Admin/Room/Index.cshtml", rooms);
        }

        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            return View("~/Views/Admin/Room/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Room room)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (ModelState.IsValid)
            {
                _context.Add(room);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Room/Create.cshtml");
        }

        // GET: Room/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");
            if (id == null) return NotFound();
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();
            // Kiểm tra xem phòng chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.RoomId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa phòng chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Room/Edit.cshtml", room);
        }

        // POST: Room/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Room room)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (id != room.Id) return NotFound();

            // Kiểm tra xem phòng chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.RoomId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa phòng chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            if (ModelState.IsValid)
            {
                _context.Update(room);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Room/Edit.cshtml", room);
        }

        // GET: Room/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");
            if (id == null) return NotFound();
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();
            // Kiểm tra xem phòng chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.RoomId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể xóa phòng chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Room/Delete.cshtml", room);
        }

        // POST: Room/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");
            // Kiểm tra xem phòng chiếu đã có vé được đặt chưa
            bool hasBookedTickets = await _context.Tickets
                .Include(t => t.Showtime)
                .AnyAsync(t => t.Showtime.RoomId == id);
            if (hasBookedTickets)
            {
                TempData["ErrorMessage"] = "Không thể xóa phòng chiếu này vì đã có vé được đặt.";
                return RedirectToAction(nameof(Index));
            }
            var room = await _context.Rooms.FindAsync(id);
            if (room != null)
            {
                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
