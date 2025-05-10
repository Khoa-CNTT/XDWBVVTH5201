using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaTest.Controllers.Staff
{
    public class StaffHomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StaffHomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Kiểm tra quyền truy cập
        private bool IsStaffAuthenticated()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Staff";
        }
        
        public async Task<IActionResult> Index()
        {
            if (!IsStaffAuthenticated())
                return RedirectToAction("Login", "Account");

            var today = DateTime.Today;

            // Thống kê vé cần xác nhận và đã xác nhận trong ngày
            ViewBag.PendingTickets = await _context.Tickets
                .Where(t => t.Showtime.StartTime.Date == today && !t.IsCheckedIn)
                .CountAsync();

            ViewBag.CheckedInTickets = await _context.Tickets
                .Where(t => t.Showtime.StartTime.Date == today && t.IsCheckedIn)
                .CountAsync();

            // Thống kê tổng số phim đang chiếu hôm nay
            ViewBag.TodayMovies = await _context.Showtimes
                .Where(s => s.StartTime.Date == today)
                .Select(s => s.MovieId)
                .Distinct()
                .CountAsync();

            return View("~/Views/Staff/StaffHome/Index.cshtml");
        }
    }
}