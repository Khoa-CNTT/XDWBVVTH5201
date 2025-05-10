using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace CinemaTest.Models.User
{
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        public int ShowtimeID { get; set; }

        [Required]
        public string SeatNumber { get; set; } = string.Empty;  // Khởi tạo với chuỗi rỗng

        [Required]
        public string Status { get; set; } = "Booked";  // Khởi tạo giá trị mặc định

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } = 85000;  // Giá vé mặc định đã được khởi tạo

        public string? TicketCode { get; set; }  // Đã được đánh dấu nullable

        // Thêm thuộc tính PurchaseDate
        public DateTime PurchaseDate { get; set; } = DateTime.Now;  // Giá trị mặc định là thời điểm hiện tại

        // Thêm các trường để kiểm tra vé
        public bool IsCheckedIn { get; set; } = false;  // Mặc định vé chưa được check-in
        public DateTime? CheckInTime { get; set; }      // Thời gian check-in (null nếu chưa check-in)

        // Quan hệ
        public int? TransactionId { get; set; }

        [ForeignKey("TransactionId")]
        public Transaction? Transaction { get; set; }

        [ForeignKey("ShowtimeID")]
        public virtual CinemaTest.Models.Admin.Showtime? Showtime { get; set; }

        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
    }
}