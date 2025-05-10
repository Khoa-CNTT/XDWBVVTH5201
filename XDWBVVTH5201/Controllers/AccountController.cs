using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using CinemaTest.Models.User;

namespace CinemaTest.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            return View("~/Views/User/Account/Profile.cshtml", user);
        }

        // GET: /Account/EditProfile
        public async Task<IActionResult> EditProfile()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            return View("~/Views/User/Account/EditProfile.cshtml", user);
        }

        // POST: /Account/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(CinemaTest.Models.User.User user)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login");
            }

            if (userId != user.Id)
            {
                return NotFound();
            }

            // Giữ lại thông tin mật khẩu hiện tại
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            user.PasswordHash = currentUser.PasswordHash;
            user.Role = currentUser.Role; // Đảm bảo không thay đổi vai trò
            user.IsLocked = currentUser.IsLocked; // Giữ nguyên trạng thái khóa
            user.UpdatedAt = DateTime.Now;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    // Cập nhật thông tin phiên đăng nhập
                    HttpContext.Session.SetString("Username", user.Username);

                    TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
                    return RedirectToAction(nameof(Profile));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View("~/Views/User/Account/EditProfile.cshtml", user);
        }
        // GET: /Account/ChangePassword
        public IActionResult ChangePassword()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login");
            }

            return View("~/Views/User/Account/ChangePassword.cshtml", new PasswordChangeViewModel());
        }
        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(PasswordChangeViewModel model)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(model.CurrentPassword) || string.IsNullOrEmpty(model.NewPassword) || string.IsNullOrEmpty(model.ConfirmPassword))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin.";
                return View("~/Views/User/Account/ChangePassword.cshtml", model);
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["ErrorMessage"] = "Mật khẩu mới và xác nhận mật khẩu không khớp.";
                return View("~/Views/User/Account/ChangePassword.cshtml", model);
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Kiểm tra mật khẩu hiện tại
            if (ComputeSha256Hash(model.CurrentPassword) != user.PasswordHash)
            {
                TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                return View("~/Views/User/Account/ChangePassword.cshtml", model);
            }

            // Cập nhật mật khẩu mới
            user.PasswordHash = ComputeSha256Hash(model.NewPassword);
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // GET: /Account/OrderHistory
        public async Task<IActionResult> OrderHistory()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login");
            }

            // Lấy lịch sử giao dịch của người dùng
            var transactions = await _context.Transactions
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return View("~/Views/User/Account/OrderHistory.cshtml", transactions);
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View("~/Views/User/Account/Login.cshtml");
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string Username, string Password)
        {
            var passwordHash = ComputeSha256Hash(Password);
            var user = _context.Users.FirstOrDefault(u => u.Username == Username && u.PasswordHash == passwordHash);

            if (user == null)
            {
                ViewBag.Message = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return View("~/Views/User/Account/Login.cshtml");
            }

            if (user.IsLocked)
            {
                ViewBag.Message = "Tài khoản đã bị khóa.";
                return View("~/Views/User/Account/Login.cshtml");
            }

            // Save login info into Session
            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);

            // 🚀 Phân luồng sau khi đăng nhập
            if (user.Role == "Admin")
            {
                return RedirectToAction("Index", "AdminHome"); // Admin ➔ AdminHome
            }
            else if (user.Role == "Staff")
            {
                return RedirectToAction("Index", "StaffHome"); // Staff ➔ StaffHome
            }
            else
            {
                return RedirectToAction("Index", "Home"); // User ➔ Home
            }
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View("~/Views/User/Account/Register.cshtml");
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Models.User.User model, string ConfirmPassword)
        {
            if (model.PasswordHash != ConfirmPassword)
            {
                ViewBag.Message = "Mật khẩu xác nhận không khớp.";
                return View("~/Views/User/Account/Register.cshtml");
            }

            if (_context.Users.Any(u => u.Username == model.Username || u.Email == model.Email))
            {
                ViewBag.Message = "Tên đăng nhập hoặc email đã tồn tại.";
                return View("~/Views/User/Account/Register.cshtml");
            }

            model.PasswordHash = ComputeSha256Hash(model.PasswordHash);
            model.Role = "User"; // 👉 Khi đăng ký mặc định là User
            model.IsLocked = false; // 👉 Tài khoản không bị khóa
            _context.Users.Add(model);
            await _context.SaveChangesAsync();

            ViewBag.Message = "Đăng ký thành công. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        // GET: /Account/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // Helper methods
        private int GetUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return 0;
            }
            return int.Parse(userIdStr);
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}