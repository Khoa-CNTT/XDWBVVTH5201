using System.ComponentModel.DataAnnotations;

namespace CinemaTest.Models.Staff
{
    public class TicketValidationViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã vé")]
        [Display(Name = "Mã vé")]
        public string? TicketCode { get; set; }
    }

    // Model hiển thị thông tin vé sau khi xác nhận
    public class TicketDetailsViewModel
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string MovieTitle { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public DateTime ShowtimeDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsCheckedIn { get; set; }
        public DateTime? CheckInTime { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }
}