using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using CinemaTest.Models.Staff;
using CinemaTest.Models.User;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace CinemaTest.Controllers.Staff
{
    public class TicketValidationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketValidationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Kiểm tra quyền truy cập
        private bool IsStaffAuthenticated()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Staff";
        }

        // GET: Staff/TicketValidation
        public IActionResult Index()
        {
            if (!IsStaffAuthenticated())
                return RedirectToAction("Login", "Account");

            var model = new TicketValidationViewModel();
            return View("~/Views/Staff/TicketValidation/Index.cshtml", model);
        }

        // GET: Staff/TicketValidation/ScanQR
        public IActionResult ScanQR()
        {
            if (!IsStaffAuthenticated())
                return RedirectToAction("Login", "Account");

            return View("~/Views/Staff/TicketValidation/ScanQR.cshtml");
        }

        // POST: Staff/TicketValidation/ValidateTicket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateTicket(TicketValidationViewModel model)
        {
            if (!IsStaffAuthenticated())
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                return View("~/Views/Staff/TicketValidation/Index.cshtml", model);
            }

            // Tìm vé theo mã
            var ticket = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Room)
                .Include(t => t.Transaction)
                .FirstOrDefaultAsync(t => t.TicketCode == model.TicketCode);

            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy vé với mã này.";
                return View("~/Views/Staff/TicketValidation/Index.cshtml", model);
            }

            // Kiểm tra xem vé có thuộc suất chiếu trong ngày không
            if (ticket.Showtime.StartTime.Date != DateTime.Today)
            {
                TempData["ErrorMessage"] = "Vé này không phải cho suất chiếu hôm nay.";
                return View("~/Views/Staff/TicketValidation/Index.cshtml", model);
            }

            // Kiểm tra xem vé đã được sử dụng chưa
            if (ticket.IsCheckedIn)
            {
                TempData["ErrorMessage"] = "Vé này đã được sử dụng vào lúc: " + ticket.CheckInTime?.ToString("HH:mm dd/MM/yyyy");
                return View("~/Views/Staff/TicketValidation/Index.cshtml", model);
            }

            var detailsModel = new TicketDetailsViewModel
            {
                TicketId = ticket.Id,
                TicketCode = ticket.TicketCode,
                MovieTitle = ticket.Showtime.Movie.Title,
                RoomName = ticket.Showtime.Room.Name,
                SeatNumber = ticket.SeatNumber,
                ShowtimeDate = ticket.Showtime.StartTime,
                Status = ticket.Status,
                CustomerName = ticket.User?.FullName ?? ticket.User?.Username ?? "Không có thông tin",
                Price = ticket.Price,
                IsCheckedIn = ticket.IsCheckedIn,
                CheckInTime = ticket.CheckInTime,
                PaymentStatus = ticket.Transaction?.Status ?? "Pending"
            };

            return View("~/Views/Staff/TicketValidation/TicketDetails.cshtml", detailsModel);
        }

        // GET: Staff/TicketValidation/ValidateTicket
        [HttpGet]
        public async Task<IActionResult> ValidateTicket(string ticketCode)
        {
            if (!IsStaffAuthenticated())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(ticketCode))
            {
                return RedirectToAction("Index");
            }

            var model = new TicketValidationViewModel { TicketCode = ticketCode };
            return await ValidateTicket(model);
        }

        // POST: Staff/TicketValidation/CheckInTicket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckInTicket(int ticketId)
        {
            if (!IsStaffAuthenticated())
                return RedirectToAction("Login", "Account");

            var ticket = await _context.Tickets
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Room)
                .Include(t => t.Transaction)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy vé.";
                return RedirectToAction("Index");
            }

            if (ticket.IsCheckedIn)
            {
                TempData["ErrorMessage"] = "Vé này đã được sử dụng.";
                return RedirectToAction("Index");
            }
            // Kiểm tra thanh toán
            if (ticket.Transaction?.Status != "Completed")
            {
                TempData["ErrorMessage"] = "Vé chưa được thanh toán. Vui lòng liên hệ Quản trị viên.";
                return RedirectToAction("Index");
            }

            // Đánh dấu vé đã được sử dụng
            ticket.IsCheckedIn = true;
            ticket.CheckInTime = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xác nhận vé thành công.";
            return RedirectToAction("Index");
        }
    }
}