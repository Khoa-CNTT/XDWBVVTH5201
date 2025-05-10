using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;

namespace CinemaTest.Controllers.Admin
{
    public class AdminHomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminHomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.MovieCount = await _context.Movies.CountAsync();
            ViewBag.RoomCount = await _context.Rooms.CountAsync();
            ViewBag.ShowtimeCount = await _context.Showtimes.CountAsync();

            return View("~/Views/Admin/AdminHome/Index.cshtml");
        }
    }
}
