using CinemaTest.Models.Admin;

namespace CinemaTest.Models.User
{
    public class MovieDetailsViewModel
    {
        public Movie Movie { get; set; } = null!;
        public List<Showtime> Showtimes { get; set; } = new List<Showtime>();
    }
}
