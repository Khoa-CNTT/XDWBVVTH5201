using System.Collections.Generic;
using CinemaTest.Models.Admin;

namespace CinemaTest.Models.User
{
    public class BookTicketViewModel
    {
        public CinemaTest.Models.Admin.Showtime? Showtime { get; set; } 
        public List<string> ReservedSeats { get; set; } = new List<string>();
        public List<string> SelectedSeats { get; set; } = new List<string>();
    }
}
