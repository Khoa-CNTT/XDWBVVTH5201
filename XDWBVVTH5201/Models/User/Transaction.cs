using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Sockets;

namespace CinemaTest.Models.User
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Required]
        public string PaymentMethod { get; set; } = "Online";  // Online, Counter, etc.

        [Required]
        public string Status { get; set; } = "Pending";  // Pending, Completed, Failed, Refunded

        public string? TransactionCode { get; set; }  // Mã giao dịch từ cổng thanh toán

        // Quan hệ với vé
        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}