using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Session;
using CinemaTest.Data; // Thay bằng namespace thật của bạn
using CinemaTest.Models.User;
using CinemaTest.Models.Admin;
using System.Text;
using System.Security.Cryptography;

// Thêm vào đầu file Program.cs, trước phần khởi tạo builder
AppContext.SetSwitch("System.Drawing.EnableUnixSupport", true);
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // timeout 30 phút
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Thêm các dịch vụ thanh toán
builder.Services.AddScoped<CinemaTest.Services.Payment.VNPayService>();
builder.Services.AddScoped<CinemaTest.Services.Payment.MoMoService>();
builder.Services.AddScoped<CinemaTest.Services.Payment.ZaloPayService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Seed Admin account
    if (!db.Users.Any(u => u.Role == "Admin"))
    {
        var admin = new User
        {
            Username = "admin",
            Email = "admin@cinema.com",
            PasswordHash = ComputeSha256Hash("admin123"), // mật khẩu mặc định
            Role = "Admin",
            IsLocked = false,
            CreatedAt = DateTime.Now
        };
        db.Users.Add(admin);
        db.SaveChanges();
    }
    if (!db.Users.Any(u => u.Role == "Staff"))
    {
        var staffUser = new User
        {
            Username = "staff",
            Email = "staff@example.com",
            PasswordHash = ComputeSha256Hash("Staff@123"), // Hàm mã hóa như đã dùng cho các tài khoản khác
            Role = "Staff",
            IsLocked = false,
            CreatedAt = DateTime.Now
        };
        db.Users.Add(staffUser);
        db.SaveChanges();
    }

    // Seed Movies nếu cần
    if (!db.Movies.Any())
    {
        db.Movies.AddRange(
            new Movie { Title = "Avengers: Endgame", Genre = "Hành động", Description = "Cuộc chiến cuối cùng", Duration = 180, PosterUrl = "https://linkanh1.jpg", TrailerUrl = "https://youtube.com/abc" },
            new Movie { Title = "Frozen II", Genre = "Hoạt hình", Description = "Nữ hoàng băng giá trở lại", Duration = 110, PosterUrl = "https://linkanh2.jpg", TrailerUrl = "https://youtube.com/def" }
        );
        db.SaveChanges();
    }
}

// Hàm băm mật khẩu SHA256
string ComputeSha256Hash(string rawData)
{
    using (var sha256Hash = SHA256.Create())
    {
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new StringBuilder();
        foreach (var b in bytes)
            builder.Append(b.ToString("x2"));
        return builder.ToString();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // <-- BẮT BUỘC phải có dòng này trước app.UseAuthorization()

app.UseAuthorization();

// Map routes với hỗ trợ cho controller trong thư mục con
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.Run();
