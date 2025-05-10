using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using CinemaTest.Data;
using CinemaTest.Models.User;
using CinemaTest.Models.Admin;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using CinemaTest.Services.Payment;

// Lưu ý namespace phải đúng với cấu trúc thư mục
namespace CinemaTest.Controllers.User
{
    // Nếu controller nằm trong thư mục con, thêm [Area] attribute hoặc chỉ định view path
    public class TicketController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly VNPayService _vnpayService;
        private readonly MoMoService _momoService;
        private readonly ZaloPayService _zalopayService;

        public TicketController(
            ApplicationDbContext context,
            VNPayService vnpayService,
            MoMoService momoService,
            ZaloPayService zalopayService)
        {
            _context = context;
            _vnpayService = vnpayService;
            _momoService = momoService;
            _zalopayService = zalopayService;
        }

        // Kiểm tra quyền truy cập thủ công
        private bool IsUserAuthenticated()
        {
            var username = HttpContext.Session.GetString("Username");
            var role = HttpContext.Session.GetString("Role");
            return !string.IsNullOrEmpty(username) && role == "User";
        }

        private int GetUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                return userId;
            }
            return 0;
        }

        // GET: Ticket/Book?showtimeId=1
        public IActionResult Book(int showtimeId)
        {
            // Kiểm tra xác thực
            if (!IsUserAuthenticated())
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Book", "Ticket", new { showtimeId }) });
            }

            var showtime = _context.Showtimes
                .Include(s => s.Room)
                .Include(s => s.Movie)
                .FirstOrDefault(s => s.Id == showtimeId);

            if (showtime == null) return NotFound();

            // Kiểm tra thời gian - không cho đặt vé cho suất chiếu đã qua
            if (showtime.StartTime < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Không thể đặt vé cho suất chiếu đã bắt đầu.";
                return RedirectToAction("Details", "Home", new { id = showtime.MovieId });
            }

            var reservedSeats = _context.Tickets
                .Where(t => t.ShowtimeID == showtimeId)
                .Select(t => t.SeatNumber)
                .ToList();

            var viewModel = new BookTicketViewModel
            {
                Showtime = showtime,
                ReservedSeats = reservedSeats,
                SelectedSeats = new List<string>()
            };

            // Chỉ định đường dẫn view một cách rõ ràng
            return View("~/Views/User/Ticket/Book.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Book(int showtimeId, List<string> selectedSeats)
        {
            // Kiểm tra xác thực
            if (!IsUserAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (selectedSeats == null || !selectedSeats.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một ghế để đặt.";
                return RedirectToAction("Book", new { showtimeId });
            }

            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            // Kiểm tra lại để tránh đặt ghế đã có người đặt
            var alreadyBooked = _context.Tickets
                .Where(t => t.ShowtimeID == showtimeId && selectedSeats.Contains(t.SeatNumber))
                .Select(t => t.SeatNumber)
                .ToList();

            if (alreadyBooked.Any())
            {
                TempData["ErrorMessage"] = "Một số ghế đã được đặt. Vui lòng chọn lại.";
                return RedirectToAction("Book", new { showtimeId });
            }

            var showtime = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .FirstOrDefault(s => s.Id == showtimeId);

            if (showtime == null)
            {
                return NotFound();
            }

            // Tính tổng tiền
            decimal ticketPrice = 85000; // Giá mặc định
            decimal totalAmount = ticketPrice * selectedSeats.Count;

            // Tạo PaymentViewModel cho trang thanh toán
            var paymentViewModel = new PaymentViewModel
            {
                ShowtimeId = showtimeId,
                SelectedSeats = selectedSeats,
                TotalAmount = totalAmount,
                Showtime = showtime,
                UserName = HttpContext.Session.GetString("Username"),
                Email = _context.Users.FirstOrDefault(u => u.Id == userId)?.Email
            };

            TempData["SelectedSeats"] = string.Join(",", selectedSeats);

            // Chỉ định đường dẫn view một cách rõ ràng
            return View("~/Views/User/Ticket/Payment.cshtml", paymentViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentViewModel model, string paymentOption)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy thông tin suất chiếu từ database để tránh phụ thuộc vào dữ liệu form
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.Id == model.ShowtimeId);

            if (showtime == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin suất chiếu";
                return View("~/Views/User/Ticket/Payment.cshtml", model);
            }

            // Kiểm tra và đảm bảo danh sách ghế đã chọn
            if (model.SelectedSeats == null || !model.SelectedSeats.Any())
            {
                // Thử lấy từ TempData nếu không có trong model
                var selectedSeatsStr = TempData["SelectedSeats"] as string;
                if (!string.IsNullOrEmpty(selectedSeatsStr))
                {
                    model.SelectedSeats = selectedSeatsStr.Split(',').ToList();
                }
                else
                {
                    TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một ghế";
                    return RedirectToAction("Book", new { showtimeId = model.ShowtimeId });
                }
            }

            // Tính lại tổng tiền để đảm bảo chính xác
            decimal ticketPrice = 85000; // Giá mặc định
            model.TotalAmount = ticketPrice * model.SelectedSeats.Count;

            // Tạo transaction
            var transaction = new Transaction
            {
                UserID = userId,
                Amount = model.TotalAmount,
                TransactionDate = DateTime.Now,
                Status = "Pending",
                TransactionCode = "TR" + DateTime.Now.ToString("yyyyMMddHHmmss") + userId
            };

            // Xử lý dựa trên phương thức thanh toán
            if (model.PaymentMethod == "Counter")
            {
                // Thanh toán tại quầy
                transaction.PaymentMethod = "Counter";
                transaction.Status = "Pending"; // Chờ xác nhận từ quầy

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                // Tạo vé với trạng thái chờ thanh toán
                foreach (var seat in model.SelectedSeats)
                {
                    var ticket = new Ticket
                    {
                        UserID = userId,
                        ShowtimeID = showtime.Id,
                        SeatNumber = seat,
                        TicketCode = GenerateTicketCode(),
                        Price = ticketPrice,
                        Status = "Pending", // Chờ thanh toán
                        TransactionId = transaction.Id
                    };

                    _context.Tickets.Add(ticket);
                }

                await _context.SaveChangesAsync();

                return RedirectToAction("Confirmation", new { transactionId = transaction.Id });
            }
            else
            {
                // Thanh toán online
                transaction.PaymentMethod = paymentOption switch
                {
                    "opt1" => "MoMo",
                    "opt2" => "VNPay",
                    "opt3" => "ZaloPay",
                    _ => "Unknown"
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                string paymentUrl = "";

                try
                {
                    switch (paymentOption)
                    {
                        case "opt1": // MoMo
                            paymentUrl = await _momoService.CreatePaymentAsync(
                                transaction.TransactionCode,
                                transaction.Amount,
                                $"Thanh toán vé xem phim {showtime.Movie.Title}");
                            break;

                        case "opt2": // VNPay
                            paymentUrl = _vnpayService.CreatePaymentUrl(
                                transaction.TransactionCode,
                                transaction.Amount,
                                $"Thanh toán vé xem phim {showtime.Movie.Title}",
                                HttpContext);
                            break;

                        case "opt3": // ZaloPay
                            paymentUrl = await _zalopayService.CreatePaymentAsync(
                                transaction.TransactionCode,
                                transaction.Amount,
                                $"Thanh toán vé xem phim {showtime.Movie.Title}");
                            break;

                        default:
                            TempData["ErrorMessage"] = "Phương thức thanh toán không hợp lệ.";
                            model.Showtime = showtime; // Đảm bảo model có đầy đủ thông tin
                            return View("~/Views/User/Ticket/Payment.cshtml", model);
                    }

                    if (string.IsNullOrEmpty(paymentUrl))
                    {
                        TempData["ErrorMessage"] = "Không thể kết nối đến cổng thanh toán. Vui lòng thử lại sau hoặc chọn phương thức thanh toán khác.";
                        model.Showtime = showtime; // Đảm bảo model có đầy đủ thông tin
                        return View("~/Views/User/Ticket/Payment.cshtml", model);
                    }

                    // Lưu thông tin để xử lý sau khi thanh toán thành công
                    TempData["SelectedSeats"] = string.Join(",", model.SelectedSeats);
                    TempData["ShowtimeId"] = showtime.Id;
                    TempData["TransactionId"] = transaction.Id;

                    return Redirect(paymentUrl);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xử lý thanh toán: " + ex.Message;
                    model.Showtime = showtime; // Đảm bảo model có đầy đủ thông tin
                    return View("~/Views/User/Ticket/Payment.cshtml", model);
                }
            }
        }

        public async Task<IActionResult> Confirmation(int transactionId)
        {
            // Kiểm tra xác thực
            if (!IsUserAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = GetUserId();

            // Chỉ lấy những ticket có showtime không null
            var transaction = await _context.Transactions
                .Include(t => t.Tickets.Where(ticket => ticket.Showtime != null))
                .ThenInclude(t => t.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(t => t.Tickets.Where(ticket => ticket.Showtime != null))
                .ThenInclude(t => t.Showtime)
                .ThenInclude(s => s.Room)
                .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserID == userId);

            if (transaction == null)
            {
                return NotFound();
            }

            // Hiển thị trang xác nhận với thông tin giao dịch và vé
            return View("~/Views/User/Ticket/Confirmation.cshtml", transaction);
        }

        public async Task<ActionResult> MyTickets()
        {
            // Kiểm tra xác thực
            if (!IsUserAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = GetUserId();

            var tickets = await _context.Tickets
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Room)
                .Where(t => t.UserID == userId && t.Showtime != null) // Chỉ lấy vé có showtime không null
                .OrderByDescending(t => t.Showtime.StartTime)
                .ToListAsync();

            return View("~/Views/User/Ticket/MyTickets.cshtml", tickets);
        }

        // Thêm vào TicketController - Phương thức GetQrCode sử dụng Google Charts API
        public IActionResult GetQrCode(string ticketCode)
        {
            if (string.IsNullOrEmpty(ticketCode))
            {
                return NotFound();
            }

            var ticket = _context.Tickets
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Room)
                .Include(t => t.Showtime)
                .ThenInclude(s => s.Movie)
                .FirstOrDefault(t => t.TicketCode == ticketCode);

            if (ticket == null)
            {
                return NotFound();
            }

            try
            {
                // Tạo nội dung mã QR
                string qrText = $"Mã vé: {ticket.TicketCode}\nGhế: {ticket.SeatNumber}\nPhòng: {ticket.Showtime?.Room?.Name ?? "N/A"}\nSuất chiếu: {ticket.Showtime?.StartTime.ToString("HH:mm dd/MM/yyyy") ?? "N/A"}";

                // Sử dụng Google Charts API để tạo QR code
                string encodedText = Uri.EscapeDataString(qrText);
                string url = $"https://chart.googleapis.com/chart?cht=qr&chl={encodedText}&chs=300x300&chld=H|0";

                // Tải QR code từ Google Charts API
                using (var client = new WebClient())
                {
                    byte[] imageData = client.DownloadData(url);
                    return File(imageData, "image/png");
                }
            }
            catch (Exception ex)
            {
                return Content("Không thể tạo mã QR. Lỗi: " + ex.Message);
            }
        }
        public async Task<IActionResult> VNPayCallback()
        {
            var vnpayData = HttpContext.Request.Query;
            var isValidSignature = _vnpayService.ValidateCallback(vnpayData);

            if (isValidSignature)
            {
                var vnp_ResponseCode = vnpayData["vnp_ResponseCode"].ToString();
                var vnp_TxnRef = vnpayData["vnp_TxnRef"].ToString();

                var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionCode == vnp_TxnRef);

                if (transaction != null && vnp_ResponseCode == "00")
                {
                    transaction.Status = "Completed";
                    await _context.SaveChangesAsync();

                    // Chuyển hướng đến trang xác nhận
                    return RedirectToAction("Confirmation", new { transactionId = transaction.Id });
                }
                else
                {
                    if (transaction != null)
                    {
                        transaction.Status = "Failed";
                        await _context.SaveChangesAsync();
                    }

                    TempData["ErrorMessage"] = "Thanh toán thất bại: " + vnp_ResponseCode;
                    return RedirectToAction("PaymentFailed");
                }
            }

            TempData["ErrorMessage"] = "Chữ ký không hợp lệ hoặc dữ liệu đã bị thay đổi.";
            return RedirectToAction("PaymentFailed");
        }

        public async Task<IActionResult> MoMoCallback()
        {
            var response = HttpContext.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());
            var isValid = _momoService.ValidateCallback(response);

            if (isValid && response.ContainsKey("orderId"))
            {
                var orderId = response["orderId"];
                var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionCode == orderId);

                if (transaction != null)
                {
                    transaction.Status = "Completed";
                    await _context.SaveChangesAsync();

                    // Chuyển hướng đến trang xác nhận
                    return RedirectToAction("Confirmation", new { transactionId = transaction.Id });
                }
            }

            TempData["ErrorMessage"] = "Thanh toán thất bại hoặc dữ liệu không hợp lệ.";
            return RedirectToAction("PaymentFailed");
        }

        public async Task<IActionResult> ZaloPayCallback()
        {
            var response = HttpContext.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());
            var isValid = _zalopayService.ValidateCallback(response);

            if (isValid && response.ContainsKey("app_trans_id"))
            {
                var appTransId = response["app_trans_id"];
                var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionCode == appTransId);

                if (transaction != null)
                {
                    transaction.Status = "Completed";
                    await _context.SaveChangesAsync();

                    // Chuyển hướng đến trang xác nhận
                    return RedirectToAction("Confirmation", new { transactionId = transaction.Id });
                }
            }

            TempData["ErrorMessage"] = "Thanh toán thất bại hoặc dữ liệu không hợp lệ.";
            return RedirectToAction("PaymentFailed");
        }
        // Thêm action này vào TicketController
        public async Task<IActionResult> SimulatePaymentCallback(int transactionId, string paymentType)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction == null || transaction.UserID != userId)
            {
                return NotFound();
            }

            transaction.Status = "Completed";
            await _context.SaveChangesAsync();

            // Lấy thông tin đã lưu trong TempData
            var selectedSeatsStr = TempData["SelectedSeats"] as string;
            var showtimeIdStr = TempData["ShowtimeId"]?.ToString();

            if (!string.IsNullOrEmpty(selectedSeatsStr) && !string.IsNullOrEmpty(showtimeIdStr)
                && int.TryParse(showtimeIdStr, out int showtimeId))
            {
                var selectedSeats = selectedSeatsStr.Split(',');

                foreach (var seat in selectedSeats)
                {
                    var ticket = new Ticket
                    {
                        UserID = userId,
                        ShowtimeID = showtimeId,
                        SeatNumber = seat,
                        TicketCode = GenerateTicketCode(),
                        Price = 85000,
                        Status = "Paid",
                        TransactionId = transaction.Id
                    };

                    // Nếu có thuộc tính PurchaseDate
                     ticket.PurchaseDate = DateTime.Now;

                    _context.Tickets.Add(ticket);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Confirmation", new { transactionId = transaction.Id });
        }
        public async Task<IActionResult> SimulatePaymentSuccess(int showtimeId, string selectedSeats, decimal totalAmount)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(selectedSeats))
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin ghế đã chọn";
                return RedirectToAction("Book", new { showtimeId });
            }

            var seatList = selectedSeats.Split(',');

            // Lấy thông tin showtime
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .FirstOrDefaultAsync(s => s.Id == showtimeId);

            if (showtime == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch chiếu";
                return RedirectToAction("Index", "Home");
            }

            // Tạo transaction mới
            var transaction = new Transaction
            {
                UserID = userId,
                Amount = totalAmount > 0 ? totalAmount : 85000 * seatList.Length,
                TransactionDate = DateTime.Now,
                PaymentMethod = "Simulated", // Đánh dấu là thanh toán giả lập
                Status = "Completed",
                TransactionCode = "SIM" + DateTime.Now.ToString("yyyyMMddHHmmss") + userId
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            // Tạo vé
            foreach (var seat in seatList)
            {
                var ticket = new Ticket
                {
                    UserID = userId,
                    ShowtimeID = showtimeId,
                    SeatNumber = seat,
                    TicketCode = GenerateTicketCode(),
                    Price = 85000,
                    Status = "Paid",
                    TransactionId = transaction.Id
                };

                _context.Tickets.Add(ticket);
            }

            await _context.SaveChangesAsync();

            // Chuyển đến trang xác nhận
            return RedirectToAction("Confirmation", new { transactionId = transaction.Id });
        }

        public IActionResult PaymentFailed()
        {
            return View("~/Views/User/Ticket/PaymentFailed.cshtml");
        }

        // Hàm tạo mã giao dịch ngẫu nhiên
        private string GenerateTransactionCode()
        {
            return "TX" + DateTime.Now.ToString("yyyyMMddHHmm") + new Random().Next(1000, 9999).ToString();
        }

        // Hàm tạo mã vé ngẫu nhiên
        private string GenerateTicketCode()
        {
            return "TK" + DateTime.Now.ToString("yyyyMMdd") + new Random().Next(10000, 99999).ToString();
        }
    }
}