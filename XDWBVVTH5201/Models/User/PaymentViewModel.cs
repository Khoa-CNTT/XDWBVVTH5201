namespace CinemaTest.Models.User
{
    public class PaymentViewModel
    {
        public int ShowtimeId { get; set; }
        public List<string> SelectedSeats { get; set; } = new List<string>();
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "Online";
        public CinemaTest.Models.Admin.Showtime? Showtime { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
    }
}