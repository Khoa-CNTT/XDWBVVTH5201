using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Data;
using CinemaTest.Models.User;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaTest.Controllers.Admin
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/User
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var users = await _context.Users.ToListAsync();
            return View("~/Views/Admin/User/Index.cshtml", users);
        }

        // GET: Admin/User/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (id == null)
                return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
                return NotFound();

            return View("~/Views/Admin/User/Details.cshtml", user);
        }

        // GET: Admin/User/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (id == null)
                return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            return View("~/Views/Admin/User/Edit.cshtml", user);
        }

        // POST: Admin/User/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CinemaTest.Models.User.User user)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (id != user.Id)
                return NotFound();

            // Kiểm tra nếu là admin duy nhất, không cho khóa hoặc đổi role
            if (user.Role == "Admin")
            {
                int adminCount = await _context.Users.CountAsync(u => u.Role == "Admin" && !u.IsLocked);
                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

                if (adminCount == 1 && currentUser.Role == "Admin" && (user.Role != "Admin" || user.IsLocked))
                {
                    TempData["ErrorMessage"] = "Không thể thay đổi quyền hoặc khóa admin duy nhất của hệ thống.";
                    return View("~/Views/Admin/User/Edit.cshtml", user);
                }
            }

            // Lấy và giữ lại mật khẩu cũ
            var existingUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);
            user.PasswordHash = existingUser.PasswordHash;

            if (ModelState.IsValid)
            {
                try
                {
                    user.UpdatedAt = DateTime.Now;
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    // Nếu admin đang chỉnh sửa tài khoản của chính mình
                    string currentUserId = HttpContext.Session.GetString("UserId");
                    if (currentUserId == id.ToString() && user.IsLocked)
                    {
                        // Đăng xuất nếu admin tự khóa tài khoản
                        HttpContext.Session.Clear();
                        return RedirectToAction("Login", "Account");
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/User/Edit.cshtml", user);
        }

        // GET: Admin/User/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (id == null)
                return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
                return NotFound();

            return View("~/Views/Admin/User/Delete.cshtml", user);
        }

        // POST: Admin/User/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var user = await _context.Users.FindAsync(id);

            // Kiểm tra xem đây có phải là admin duy nhất không
            if (user.Role == "Admin")
            {
                int adminCount = await _context.Users.CountAsync(u => u.Role == "Admin");
                if (adminCount == 1)
                {
                    TempData["ErrorMessage"] = "Không thể xóa admin duy nhất của hệ thống.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Kiểm tra xem người dùng đã đặt vé chưa
            bool hasTickets = await _context.Tickets.AnyAsync(t => t.UserID == id);
            if (hasTickets)
            {
                TempData["ErrorMessage"] = "Không thể xóa người dùng này vì đã có lịch sử đặt vé.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra nếu đang xóa chính mình
            string currentUserId = HttpContext.Session.GetString("UserId");
            bool isSelfDelete = currentUserId == id.ToString();

            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                if (isSelfDelete)
                {
                    // Đăng xuất nếu admin xóa chính mình
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Account");
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/User/ToggleLock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            // Kiểm tra nếu là admin duy nhất
            if (user.Role == "Admin")
            {
                int adminCount = await _context.Users.CountAsync(u => u.Role == "Admin" && !u.IsLocked);
                if (adminCount == 1 && !user.IsLocked)
                {
                    TempData["ErrorMessage"] = "Không thể khóa admin duy nhất của hệ thống.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Đảo trạng thái khóa
            user.IsLocked = !user.IsLocked;
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Nếu admin tự khóa mình
            string currentUserId = HttpContext.Session.GetString("UserId");
            if (currentUserId == id.ToString() && user.IsLocked)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}